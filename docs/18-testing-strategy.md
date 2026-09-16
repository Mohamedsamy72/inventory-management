# 18 — Comprehensive Testing Strategy

> **Document ID:** SPEC-18  
> **Topic:** Testing Pyramid, Domain Invariant Tests, Security & Scope Verification, Concurrency Tests, and Browser E2E

---

## 1. Testing Pyramid & Test Project Structure

```
                    ┌─────────────────────────┐
                    │      Browser E2E        │ (Playwright - Real HTTP & Browser)
                    ├─────────────────────────┤
                    │ API & Integration Tests │ (WebApplicationFactory + PostgreSQL Testcontainers)
                    ├─────────────────────────┤
                    │  Security & Arch Tests  │ (NetArchTest + Authorization Handlers)
                    ├─────────────────────────┤
                    │    Domain Unit Tests    │ (xUnit + FluentAssertions - Pure Math & Rules)
                    └─────────────────────────┘
```

### Solution Test Projects:
- `tests/Inventory.UnitTests/`: Domain entities, value objects, WAC calculations, conversion math.
- `tests/Inventory.IntegrationTests/`: Real database transactions, EF Core migrations, full API controllers.
- `tests/Inventory.ArchitectureTests/`: Enforces architectural boundaries (Domain has no EF Core dependency, tenant filters exist on all entities).
- ~~`tests/Inventory.E2E/`~~ — **REMOVED (ADR-031, CR-096).** E2E moved to the frontend; see below.
- `frontend/__tests__/`: **Vitest + React Testing Library** frontend unit and component tests (added — CR-080). Previously missing from this document despite being mandated.
- `frontend/e2e/`: **`@playwright/test`** end-to-end tests validating browser rendering, cookies, and Arabic RTL workflows (ADR-031). Run with `npm run test:e2e --prefix frontend`.

> **Assertion library — undecided (OD-012, CR-097).** This document mandates FluentAssertions, which is **commercially licensed from v8**. That choice is escalated to the project owner and is due before Phase 7. Until then, tests use **xUnit's built-in assertions**, which consume no licence and pre-commit no API.

---

## 2. Mandatory Test Suites & Scenarios

### 2.1 Inventory Invariant Tests (`Inventory.UnitTests`)
1. **Mathematical Sequence Verification:**
   - Opening Balance: `500`
   - Post Incoming: `100` $\rightarrow$ Stock becomes `600`.
   - Verify Reconciliation (`Expected 100`, `Actual 80`): Stock adjusts to `580` (Variance `-20`), **NOT** double-added.
   - Dispatch `20`: Stock remains `580`.
   - Confirm Receipt `18`: Stock decreases to `562` (Variance `2` logged to Discrepancies).
2. **Negative Stock Prevention:**
   - Attempting to deduct `25` from stock of `20` throws `InsufficientStockException`.
3. **WAC Costing Math:**
   - Stock `100 @ 10 EGP` + Incoming `50 @ 16 EGP` $\rightarrow$ New average cost = `12.0000 EGP`.

---

### 2.2 Security & Authorization Tests (`Inventory.IntegrationTests`)
1. **Admin Financial Isolation:**
   - `Admin` calling `GET /api/v1/items/{id}` receives item data with `averageUnitCost: null`.
   - `Admin` calling `GET /api/v1/audit` receives HTTP 403 Forbidden.
2. **Warehouse Scope Isolation:**
   - `Warehouse Staff` assigned to Warehouse A cannot view or post receiving orders for Warehouse B (HTTP 403 / 404).
3. **Restaurant Scope Isolation:**
   - `Restaurant Supervisor` for Branch A cannot confirm receipt of a supply dispatched to Branch B (HTTP 403 / 404).
4. **Tenant Isolation:**
   - Request from Company 1 attempting to query Company 2 entities yields HTTP 404 Not Found.
5. **Idempotency & Double Confirmation:**
   - Repeating `POST /api/v1/supplies/{id}/confirm` with the same `X-Idempotency-Key` returns the cached original result without deducting stock a second time.
6. **Concurrent Stock Confirmation:**
   - Two concurrent threads attempting to deduct `15` from remaining stock of `20`: exactly one succeeds; the other safely fails with `INSUFFICIENT_STOCK`.

---

### 2.3 Browser E2E Tests (Playwright)
- Tests actual Arabic RTL UI flows, quick-add category modals, supply request submissions, and receipt confirmations.

---

## 3. Reconciliation Amendment — Additional Mandatory Suites

> Added to close CR-080 and to cover the invariants established by ADR-012 … ADR-026. Every entry below must exist as a named test by Phase T1 (`docs/09-implementation-plan.md`).

### 3.1 Frontend Tests (`frontend/__tests__`, Vitest + RTL)
Formatter module (`docs/31 §4.5`); status-badge mapping (`docs/31 §4.6`); permission-driven navigation; the five API states per screen family; multi-item editor duplicate-merge behaviour; idempotency-key stability across retries; zero-fake-data scan.

### 3.2 Additional Domain Unit Tests
WAC unchanged on issue and on negative adjustment (ADR-020); reconciliation reverses at the originating line's own `unit_cost`, not the current WAC (ADR-020); conversion identity, missing-conversion, and precision cases; Arabic normalization.

### 3.3 Additional Integration Tests
- **Structural tenancy** — every tenant-scoped FK is composite (ADR-016); a hand-crafted cross-tenant insert is rejected by the database, not merely filtered.
- **Concurrency** — forced `xmin` conflict returns `409` (ADR-022); interleaved multi-item confirmations produce **zero** deadlocks (`docs/30 §5.3`).
- **Idempotency** — replay deducts once; key reuse with a different payload → `409`; missing required key → `400`; a cached response is the **role-projected** DTO, never an unprojected one.
- **Sequences** — 1,000 concurrent creations yield gap-free distinct codes; rollback leaves `last_value` unchanged; month boundary in `Africa/Cairo`.
- **Dispatch invariance** — `SELECT COUNT(*) FROM stock_ledger` is **identical** before and after fulfilment and dispatch. An absolute count, not a tolerance.
- **In-transit** — a stock count opened while shipments are dispatched produces **no** spurious `PHYSICAL_ADJUSTMENT` (ADR-018).
- **Blind counting** — the system quantity never appears in the API payload in blind mode.
- **Non-grantable permissions** — a user explicitly granted `costs:view` still receives `null` costs; granted `audit:view` still receives `403`.
- **Single role** — assigning a second role fails.
- **Append-only** — an `UPDATE` or `DELETE` against `stock_ledger` or `audit_logs` fails loudly.
- **Immutability** — changing an item's base unit after a ledger row exists is rejected; editing a used conversion is rejected.
- **All-or-nothing confirmation** — a five-line confirmation failing on one line writes **nothing**.
- **Impossible confirmation** — the `docs/30 §7.1` path produces a `StockUnavailableAtConfirmation` discrepancy and **no** auto-adjustment.
- **Audit transactionality** — a rolled-back operation leaves **no** audit row.

### 3.4 Additional Architecture Tests
No type outside `IStockPostingService` writes `StockLedger` or `StockBalance`; no `float`/`double` in `Inventory.Domain`; no generic repository; the migration contains no prohibited identifier (`docs/24 §1`); every `ITenantScopedEntity` has a global query filter; every cost-bearing projection routes through `IFinancialProjection`; no physical direction utility in `frontend/src`.

### 3.5 Additional E2E Tests
The full `docs/23` numerical walkthrough driven through the browser; Arabic PDF glyph shaping verified **visually**; CSV UTF-8 BOM verified in Excel; mobile `< 768px` card collapse; keyboard-only navigation; the three distinguishable unauthorized states.
