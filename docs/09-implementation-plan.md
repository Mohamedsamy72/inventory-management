# 09 — Authoritative Implementation Plan

> **Document ID:** SPEC-09-PLAN
> **Topic:** Dependency-aware, phase-by-phase implementation plan derived from the reconciled specification
> **Status:** Authoritative — supersedes `docs/22-implementation-plan.md`
> **Gate:** **Implementation has not begun and must not begin without explicit approval.**

---

## 1. Purpose

To convert the reconciled specification into an executable sequence in which each unit of work has explicit prerequisites, a bounded scope, a verifiable definition of done, and a rollback story. The ordering is derived from dependency analysis, not from the order in which documents were written.

## 2. Scope

Covers the whole product from empty repository to release readiness. It does not restate business rules; it references the documents that own them.

## 3. Repository Baseline (Verified)

```
D:\سيستم المخازن\
├── docs\               37 markdown specifications
└── ui-ux-pro-max\      Design-intelligence skill (SKILL.md, data\, scripts\)
```

**There is no source tree, no solution file, no migration, no test, and no configuration.** Everything below is greenfield. There is consequently no pre-existing build or test command to preserve; Phase 1 establishes the command set that every later phase verifies against.

---

## 4. Ordering Rationale — Why This Differs From the Suggested Outline

The brief's suggested outline is sound in its broad strokes. Four deliberate departures are made, each on dependency grounds. They are stated here because an unexplained reordering is indistinguishable from an accidental one.

### 4.1 Audit Infrastructure Moves Early (from ~Phase 13 to Phase 4)

`docs/14 §1` requires every audit entry to be written **inside the same transaction** as its business change. That is not a feature bolted onto handlers; it is a property of how handlers are written. If receiving, supply, confirmation, and count handlers are all built first and audit is added at Phase 13, every one of them must be reopened and re-tested — and any handler that is missed silently loses its audit trail, which is a compliance failure that no test will catch unless someone thinks to write it.

The audit **infrastructure** (interceptor, sanitization, correlation, transactional guarantee) therefore lands in Phase 4, before the first business mutation. Only the Owner-facing **viewer** and activity monitor stay late (Phase 14), because they depend on accumulated data, not on handler design.

### 4.2 Transaction, Concurrency and Idempotency Infrastructure Moves Into the Stock Foundation (Phase 7)

`docs/30` specifies lock ordering, conditional atomic updates, `xmin` concurrency, and the idempotency protocol. The first stock mutation is written in Phase 8. If this infrastructure arrives later, Phase 8 is written against a different concurrency model and rewritten afterwards. It belongs with the stock engine, before the first writer.

### 4.3 Security Fundamentals Move Into Their Owning Phases (from ~Phase 21)

CSRF, session cookies, and rate limiting are properties of the authentication pipeline and land in Phase 3. Tenant isolation and role denial are properties of the authorization pipeline and land in Phases 2 and 4. What remains for the late security phase is what genuinely belongs there: adversarial regression testing, header hardening, dependency audit, and penetration-style verification of controls built earlier.

A control retrofitted into a working system is a control nobody trusts. Composite tenant foreign keys (ADR-016) in particular must exist in the **initial** migration — adding them later requires a full table rewrite on populated tables.

### 4.4 Frontend Design System Runs in Parallel, Frontend Workflows Do Not

`docs/11` and the UI/UX guide define the design system entirely without reference to any endpoint, so Phase F1 runs concurrently with backend Phases 1–4 at no risk. Frontend **workflow** screens (F3–F6) each block on their backend phase, because building a screen against an imagined API is precisely the integration drift this plan exists to prevent. There is no mock-API track.

### 4.5 What Is Unchanged

Master data → conversions → stock ledger → receiving → requests → fulfilment → confirmation is the brief's order and is correct: each stage's data model is a prerequisite of the next. Confirmation (Phase 11) is deliberately isolated as its own phase despite being small, because it is the only stock-deducting path in the product and deserves undivided attention.

---

## 5. Phase Map & Dependency Graph

```
P0  Documentation baseline & decision gate
     │
     ▼
P1  Solution architecture & infrastructure ──────────────┐
     │                                                   │
     ▼                                                   ▼
P2  Database foundation, tenancy, initial migration   F1  Frontend shell & design system
     │                                                   │  (parallel, no API dependency)
     ▼                                                   │
P3  Authentication, sessions, CSRF, rate limiting        │
     │                                                   ▼
     ▼                                              F2  Frontend auth & app shell
P4  Authorization + audit infrastructure  ───────────────┘
     │
     ▼
P5  Master data ──────────────────────────────────► F3  Frontend master-data workflows
     │
     ▼
P6  Unit conversion system
     │
     ▼
P7  Stock engine: ledger, balances, WAC,
    transactions, concurrency, idempotency
     │
     ▼
P8  Receiving & warehouse stock ledger ────────────► F4  Frontend receiving & inventory
     │
     ▼
P9  Multi-item supply requests ────────────────────┐
     │                                             │
     ▼                                             │
P10 Fulfilment & dispatch                          ├──► F5  Frontend supply workflows
     │                                             │
     ▼                                             │
P11 Restaurant receipt confirmation ───────────────┘
     │
     ▼
P12 Discrepancies & reconciliation
     │
     ▼
P13 Physical stock counts & adjustments ───────────► F6  Role-specific dashboards
     │
     ▼
P14 Audit viewer & activity monitor (Owner)
     │
     ├╌╌► P15 File storage & evidence   🚫 DEFERRED (ADR-029) — not scheduled
     └╌╌► P16 Reporting & analytics     🚫 DEFERRED (ADR-029) — not scheduled
     │
     ▼
S1  Security hardening & adversarial regression
     │
     ▼
T1  Full automated test sweep
     │
     ▼
T2  Browser E2E & acceptance verification
     │
     ▼
R1  Documentation & release readiness
```

### 5.1 Parallelisable Work

| May run concurrently | Condition |
| :--- | :--- |
| `F1` with `P1`–`P4` | F1 touches no endpoint. |
| `F2` with `P5`–`P6` | Auth API is frozen after P3. |
| `F3` with `P6`–`P7` | Master-data API is frozen after P5. |
| `F4` with `P9` | Receiving API is frozen after P8. |
| ~~`P15` with `P16`~~ | 🚫 Both deferred (ADR-029). Neither is scheduled. |
| Test authoring within a phase | Tests are written with their phase, never deferred to T1. |

### 5.2 Strictly Sequential — Never Parallelise

`P2 → P3 → P4` (identity depends on schema; authorization depends on identity), `P6 → P7` (the ledger normalizes through conversions), `P7 → P8` (the first writer needs the engine), `P9 → P10 → P11` (each consumes the previous document), and every `F` phase after its backend phase.

---

## 6. Decision Status & Remaining Blockers

### 6.1 Closed — All Product Decisions Resolved

| Decision | Resolution | Authority |
| :--- | :--- | :--- |
| **OD-008** — serving warehouse | One default serving warehouse per restaurant, **derived server-side**. The supervisor never selects it; the DTO has no `warehouseId`. | ADR-028 |
| **OD-009** — technology baseline | Stack confirmed and not negotiable downward. Toolchain verified on the development machine. | ADR-027, `docs/33` |
| **OD-010** — ChatGPT transcript | Unavailable historical source. No requirement inferred or invented. | ADR-030 |
| **OD-011** — files and reports | Both deferred out of MVP. Reportability of core data preserved; no fake reports, no placeholder KPIs. | ADR-029 |

**OD-007** (documentation numbering) remains open and is non-blocking.

### 6.2 The One Remaining Blocker Is Environmental, Not a Decision

| Blocker | Blocks | Requirement |
| :--- | :--- | :--- |
| **PostgreSQL 16+ is not installed** on the development machine (`docs/33 §4.3`). | **Phase 2 onward** | Install PostgreSQL 16 or later. **No substitute is permitted** — SQL Server, MySQL, and Oracle are present on the machine and are explicitly rejected (ADR-027). |

**Phase 1 is unblocked and may begin on approval**, because it creates no schema and touches no database. Phase 2 does not start until `psql --version` reports 16 or later.

---

## 7. Universal Definition of Done

A task is complete only when **every applicable** item holds. A phase is complete only when every one of its tasks is complete and its checkpoint passes.

| # | Criterion |
| :--- | :--- |
| 1 | Domain rules and invariants exist in `Inventory.Domain`, expressed as behaviour, not anaemic setters. |
| 2 | EF Core configuration exists, including composite tenant keys and the specified indexes. |
| 3 | A migration exists and applies cleanly to an empty database **and** to the previous migration. |
| 4 | Request and response DTOs exist; no domain entity is ever bound to or serialized. |
| 5 | FluentValidation rules exist for every command. |
| 6 | An authorization policy exists and is applied. |
| 7 | Scope enforcement is applied server-side. |
| 8 | A transaction boundary exists per `docs/30 §4` where the operation is critical. |
| 9 | Concurrency protection exists per `docs/30 §5` where stock is touched. |
| 10 | Idempotency is enforced per `docs/30 §6.1` where required. |
| 11 | A transactional audit entry is written per `docs/14`. |
| 12 | Errors return RFC 7807 with an Arabic `messageAr` from the `docs/13` catalog. |
| 13 | Unit tests cover the domain rules; integration tests cover the endpoint including its authorization and scope failures. |
| 14 | Frontend screens implement all five API states (`UI/UX guide §6`). |
| 15 | Arabic and RTL conform to `docs/31`. |
| 16 | Documentation is updated: the owning spec, `26-traceability-matrix.md`, `27-implementation-status.md`. |
| 17 | **Zero** `TODO`, `NotImplementedException`, commented-out logic, mock data, or placeholder presented as complete. |

**A feature is not complete because a controller, page, migration, button, DTO, or passing mock exists.**

---

## 8. Verification Commands

Established in Phase 1 and used unchanged thereafter. No phase may invent its own.

| Purpose | Command |
| :--- | :--- |
| Backend build | `dotnet build InventorySystem.sln -warnaserror` |
| All backend tests | `dotnet test InventorySystem.sln` |
| Unit tests only | `dotnet test tests/Inventory.UnitTests` |
| Integration tests | `dotnet test tests/Inventory.IntegrationTests` |
| Architecture tests | `dotnet test tests/Inventory.ArchitectureTests` |
| **Phase 1 full verify (device)** | `build-verify.bat` — runs build, all tests, typecheck, lint, frontend build; writes `build-verification.log` |
| Apply migrations | `dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api` |
| Verify no pending model change | `dotnet ef migrations has-pending-model-changes --project src/Inventory.Infrastructure --startup-project src/Inventory.Api` |
| Generate migration SQL for review | `dotnet ef migrations script --idempotent --project src/Inventory.Infrastructure --startup-project src/Inventory.Api` |
| Frontend typecheck | `npm run typecheck --prefix frontend` |
| Frontend lint | `npm run lint --prefix frontend` |
| Frontend unit tests | `npm run test --prefix frontend` |
| Frontend build | `npm run build --prefix frontend` |
| Browser E2E | `npm run test:e2e --prefix frontend` *(the only E2E runner — ADR-031)* |
| Full local stack | `run-local.bat` |
| Layered diagnostics | `check-local.bat` |
| Graceful shutdown | `stop-local.bat` |

> `-warnaserror` is deliberate: a nullable-reference warning in an inventory system is a future null balance.

---

## 9. Phases

> Each phase lists: objective · prerequisites · components · tasks · acceptance criteria · verification · risks · rollback. Tasks are granular enough to be independently understandable and independently reviewable.

---

### Phase 0 — Documentation Baseline & Decision Gate

**Objective.** A reconciled, internally consistent specification with every conflict classified and every unresolved decision escalated.
**Prerequisites.** None.
**Status.** ✅ Complete (this pass).

**Delivered.** `32` conflict register; ADR-012…026 in `decision-log.md`; `open-decisions.md` OD-007…011; `28`, `29`, `30`, `31`; amendments to `04`, `README`, `glossary`, the UI/UX guide; `09-implementation-plan.md`; updated `26` and `27`.

**Acceptance criteria.**
- Every conflict is Class C/D/E with a resolution, or escalated.
- No resolution contradicts a FINAL business invariant.
- Every specification is reachable from `README.md` §1 and placed in the §2 hierarchy.

**Exit gate.** ✅ OD-008, OD-009, OD-010, OD-011 all closed (ADR-027 … ADR-030). Awaiting explicit approval to begin implementation.
**Risk.** *(Decisional risk closed.)* The remaining risk is environmental, not decisional: **PostgreSQL is not installed** (`docs/33 §4.3`), which blocks Phase 2 but not Phase 1.
**Rollback.** Documentation is version-controlled text; revert.

---

### Phase 1 — Solution Architecture & Infrastructure

**Objective.** An empty but correct, buildable, testable skeleton with architectural boundaries enforced by tests from the first commit.
**Prerequisites.** P0 complete; OD-009 **closed** (ADR-027). No database work occurs in this phase, so the missing PostgreSQL install does **not** block it.
**Components.** Solution, four source projects, four test projects, CI, local scripts.

| # | Task |
| :--- | :--- |
| 1.1 | **Execute** the verification commands of `docs/33 §6` and paste the literal output into `docs/33 §7`. This confirms the two values that filesystem inspection could not read: Node's exact version (inferred 22.x) and `PATH` resolution of `node`/`npm`/`dotnet-ef`. **Expected to fail:** `psql --version` — PostgreSQL is not installed. Record the failure; do **not** substitute another engine and do **not** write a downgrade ADR. |
| 1.1a | Create `global.json` pinning SDK `10.0.302` — two .NET 10 SDKs are installed (10.0.102, 10.0.302) and CI must resolve the same one as the developer machine. |
| 1.1b | If `node`/`npm` do not resolve as bare commands, add `D:\Nodejs` to `PATH`. This is a `PATH` fix, never a reinstall. |
| 1.1c | Check for a container runtime (`docker --version`). Its absence does not block Phase 1; it determines in Phase 2 whether integration tests use Testcontainers or a local PostgreSQL instance. |
| 1.2 | Create `InventorySystem.sln` with `Inventory.Domain`, `Inventory.Application`, `Inventory.Infrastructure`, `Inventory.Api`. |
| 1.3 | Wire dependencies: `Api → Application → Domain`, `Infrastructure → Application`, `Infrastructure → Domain`. `Domain` references nothing. |
| 1.4 | Enable `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>` in `Directory.Build.props`. |
| 1.5 | Add `.editorconfig` and a repository-root `.gitignore`. |
| 1.6 | Create the **three** .NET test projects — `Inventory.UnitTests`, `Inventory.IntegrationTests`, `Inventory.ArchitectureTests`. **`tests/Inventory.E2E/` is not created** (ADR-031, CR-096): E2E is `@playwright/test` in `frontend/e2e/`. Tests use **xUnit built-in assertions** — the FluentAssertions licence question is escalated as **OD-012** (CR-097) and is not pre-committed here. |
| 1.7 | **Architecture test:** `Inventory.Domain` references no EF Core, no ASP.NET, no Npgsql. |
| 1.8 | **Architecture test:** `Inventory.Api` never references `Inventory.Infrastructure` types directly outside composition root registration. |
| 1.9 | **Architecture test:** no type named `*Repository<T>` (generic repository ban, `docs/25`). |
| 1.10 | **Architecture test:** no `float`/`double` on any type in `Inventory.Domain` (`docs/25 §2.1`). |
| 1.11 | Configure Serilog structured JSON logging with `CorrelationId`, `CompanyId`, `UserId`, `ElapsedMs`, and the redaction list from `docs/21 §3`. |
| 1.12 | Add the global exception handler emitting RFC 7807 with `messageAr` (`docs/13 §3`); no stack trace or SQL text ever reaches a response. |
| 1.13 | Add `GET /health` (liveness) and `GET /health/ready` (DB readiness) — CR-064. Neither discloses version or schema detail. Readiness opens a raw **Npgsql** connection from configuration — no `DbContext`, no entity, no migration, so it is **not** Phase 2 work pulled forward (CR-099). It correctly reports `Unhealthy` until PostgreSQL is installed. |
| 1.14 | Configure **OpenAPI document generation** (`Microsoft.AspNetCore.OpenApi`) for Development only, served at `/openapi/v1.json`. The interactive **Swagger UI is added in Phase 3** with the first real endpoints — a UI over an empty document is decoration, not verification (CR-098). |
| 1.15 | Scaffold `frontend/` (Next.js App Router, TypeScript, Tailwind) with `typecheck`, `lint`, `test`, `build`, `test:e2e` scripts. |
| 1.16 | Author `run-local.bat`, `check-local.bat`, `stop-local.bat` per `docs/19`. **Guard test:** no script contains `DROP DATABASE` or `EnsureDeleted`. |
| 1.17 | CI pipeline running build, all four test projects, typecheck, lint, and frontend build. |

**Acceptance criteria.** `dotnet build -warnaserror` succeeds. All architecture tests pass. `/health` returns 200. `npm run build` succeeds. `check-local.bat` reports every layer. CI is green.
**Verification.** `dotnet build InventorySystem.sln -warnaserror`; `dotnet test tests/Inventory.ArchitectureTests`; `npm run typecheck --prefix frontend`; `npm run build --prefix frontend`; `check-local.bat`.
**Risks.** The backend toolchain is verified present (`docs/33 §4.1`), so no version risk remains for this phase. Two residual items are closed by task 1.1: Node's exact version (inferred 22.x) and `PATH` resolution of `node`/`npm`. Architecture tests written loosely pass vacuously — each must be proven by a temporary deliberate violation.
**Rollback.** Delete the source tree; `docs/` is untouched.

---

### Phase 2 — Database Foundation, Tenancy & Initial Migration

**Objective.** The complete clean schema, with tenant isolation as a **database** invariant, in a single reviewed initial migration.
**Prerequisites.** P1 complete **and PostgreSQL 16+ installed and reachable on port 5432** (`docs/33 §4.3`). This phase cannot begin without it, and no other engine may be substituted (ADR-027).
**Components.** `Inventory.Domain` entities, `Inventory.Infrastructure` EF configuration, `InventoryDbContext`, migration `00000000000000_InitialCreate`.

| # | Task |
| :--- | :--- |
| 2.1 | Define domain entities for every table in `docs/06`, **as amended by `docs/29 §4.1`**. |
| 2.2 | Define `ITenantScopedEntity` and the `MovementType`, `ReferenceType`, and document status enums (`docs/31 §4.6` vocabulary). |
| 2.3 | One `IEntityTypeConfiguration<T>` per entity. No configuration in `OnModelCreating` bodies. |
| 2.4 | Map every money and quantity column as `numeric(18,4)`; conversion factors `numeric(18,6)`. **No `float`/`double` anywhere.** |
| 2.5 | Add `UNIQUE (company_id, id)` to every tenant-scoped table (`29` DB-02). |
| 2.6 | Convert every tenant-scoped foreign key to composite `(company_id, fk)` (`29` DB-03, ADR-016). **Security-critical; must be in the initial migration.** |
| 2.7 | Add the real composite FKs on `user_warehouse_scopes` and `user_restaurant_scopes` (`29` DB-04). |
| 2.8 | Configure `stock_balances` concurrency via `xmin` (`UseXminAsConcurrencyToken`). **No `version` column** (`29` DB-01, ADR-022). |
| 2.9 | Add `UNIQUE (user_id)` on `user_roles` (`29` DB-05, ADR-014). |
| 2.10 | Add `discrepancies.document_number` + unique, and `reference_line_id` (`29` DB-06). |
| 2.11 | Add `supply_items.supply_request_item_id` (`29` DB-07). |
| 2.12 | Widen the `supplies` status domain; add `prepared_by`, `prepared_at` (`29` DB-08, ADR-017). |
| 2.13 | Add `receiving_orders.reversed_by/reversed_at/reversal_reason`; `receiving_order_items.reconciled` (`29` DB-09/10). |
| 2.14 | Rename `warehouses.name`, `restaurants.name`, `suppliers.name` → `name_arabic` (`29` DB-11). |
| 2.15 | Add per-parent item uniqueness on all four line tables (`29` §4.3, CR-023). |
| 2.16 | Add `uq_ledger_posting` on `stock_ledger` (`29` §4.3, CR-036). |
| 2.17 | Add the `CHECK` constraints of `29` §4.3 (received ≤ dispatched, fulfilled ≤ requested, quantity ≥ 0, positive base quantities). |
| 2.18 | Add `users.access_failed_count`, `users.lockout_end_at` (`29` DB-16). |
| 2.18a | Add `restaurants.default_serving_warehouse_id UUID NOT NULL` with the composite tenant FK `(company_id, default_serving_warehouse_id) → warehouses(company_id, id)` and its index (`29` DB-17, ADR-028). **No `restaurant_serving_warehouses` join table** — v1.0 is one-to-one. |
| 2.19 | Create `document_sequences`, `password_reset_otps`, `company_settings` (`29` §4.2). **`stored_files` and `file_attachments` are specified but NOT migrated** — files are deferred (ADR-029). |
| 2.20 | Declare `audit_logs` `PARTITION BY RANGE (created_at)`; create current and next month partitions; add the maintenance task (`29` §4.5). |
| 2.21 | Add the append-only triggers on `stock_ledger` and `audit_logs`, and revoke `UPDATE`/`DELETE` from the app role (`29` §4.4). **Use the raising trigger, not `DO INSTEAD NOTHING`.** |
| 2.22 | Create every index in `29` §5, including `pg_trgm` on `items.name_arabic`. |
| 2.23 | Add `items.name_normalized` and the normalization pipeline (`docs/31 §4.2`); make uniqueness evaluate on it. |
| 2.24 | Narrow line-table cascades per `29` DB-15. |
| 2.25 | Implement `ICurrentUserService` and the EF global tenant query filter (`docs/09 §4`). Tenant identity comes only from the authenticated principal. |
| 2.26 | Generate `InitialCreate`. Review the produced SQL line by line before committing. |
| 2.27 | Seed the five roles and the permission catalog, marking `costs:view`, `valuation:view`, `audit:view`, `audit:export` as `NON_GRANTABLE` (ADR-012). **No `Accountant` row.** |
| 2.28 | **Architecture test:** the migration contains no `restaurant_%stock%`, `%in_transit%`, `barcode`, `name_en%`, `reorder_point`, `max_stock`, or `waste_record` identifier (`docs/24 §1`). |
| 2.29 | **Architecture test:** every `ITenantScopedEntity` has a global query filter. |
| 2.30 | **Architecture test:** every tenant-scoped FK in the model is composite. |
| 2.31 | Integration test: applying the migration to an empty Testcontainers PostgreSQL succeeds and produces every index. |

**Acceptance criteria.** `AC-29-1` … `AC-29-9` all pass. `has-pending-model-changes` reports none. No prohibited identifier exists.
**Verification.** `dotnet ef database update …`; `dotnet ef migrations has-pending-model-changes …`; `dotnet test tests/Inventory.ArchitectureTests`; `dotnet test tests/Inventory.IntegrationTests`.
**Risks.** Composite FKs omitted here require a full table rewrite later — **highest-consequence risk in the plan**. `xmin` misconfiguration silently disables lost-update detection; proven by an integration test, never by inspection.
**Rollback.** The schema is not yet populated. Drop the development database and regenerate; never edit a committed migration in place once shared.

---

### Phase 3 — Authentication, Sessions, CSRF & Rate Limiting

**Objective.** Mobile + password authentication over hardened cookie sessions, with OTP reset and brute-force resistance.
**Prerequisites.** P2.

| # | Task |
| :--- | :--- |
| 3.1 | Configure ASP.NET Core Identity over the `users` table with mobile-number normalization (`docs/08 §1`). |
| 3.2 | `POST /auth/login` issuing `__Host-InventorySession` — `HttpOnly`, `Secure`, `SameSite=Strict`, `Max-Age=28800`. |
| 3.3 | Reject inactive accounts with `403 ACCOUNT_INACTIVE`. |
| 3.4 | Increment `access_failed_count`; lock out via `lockout_end_at`; reset on success. |
| 3.5 | `POST /auth/logout` invalidating the session. |
| 3.6 | Security-stamp validation on every request; rotation on password change or role/scope change invalidates live cookies. |
| 3.7 | `GET /auth/csrf-token` (CR-063) and anti-forgery validation on every `POST`/`PUT`/`PATCH`/`DELETE`. |
| 3.8 | `ISmsSender` abstraction + development console implementation (OD-001). **OTP is never returned in an HTTP response.** |
| 3.9 | `POST /auth/forgot-password/otp` — cryptographically random 6 digits, hashed into `password_reset_otps`, 5-minute expiry, single use. |
| 3.10 | `POST /auth/forgot-password/verify` and `/reset`, rotating the security stamp. |
| 3.11 | Rate limiting per `docs/08 §5` **and** ADR-026: OTP 3/mobile/15 min **and** 5/mobile/hour, plus per-IP; login 5/IP/min; general 300/user/min. |
| 3.12 | `GET /account/me` returning identity, the single role, scopes, and granted permissions. |
| 3.13 | CORS restricted to the configured frontend origin; credentials allowed; no wildcard. |
| 3.14 | Integration tests: valid login, invalid login, inactive account, lockout, CSRF rejection, OTP expiry, OTP reuse, OTP rate limit (both windows), cookie flags, `localStorage` never used. |

**Acceptance criteria.** `TEST-AUT-001`, `TEST-AUT-002` pass. No token is ever placed in browser storage. OTP never appears in any response body.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Auth`.
**Risks.** `SameSite=Strict` breaks cross-origin dev flows — resolved by same-site dev hosting, never by weakening the cookie. Rate limits tuned too tight lock out real warehouse staff during a shift — verified against the `docs/08 §5` table, not guessed.
**Rollback.** Feature-isolated; revert the auth module.

---

### Phase 4 — Authorization, Scopes, Role Denial & Audit Infrastructure

**Objective.** The complete authorization pipeline and a transactional audit trail that every later mutation inherits automatically.
**Prerequisites.** P3.
**Rationale.** See §4.1 and §4.3.

| # | Task |
| :--- | :--- |
| 4.1 | Implement the five-stage pipeline of `docs/04 §15`: authenticate → tenant → role denial → scope → execute. |
| 4.2 | Permission-based authorization policies (`<module>:<action>`). |
| 4.3 | **Role denial handler:** `Admin` + {`costs:view`, `valuation:view`, `audit:view`, `audit:export`} → `403`, overriding any grant (ADR-012). |
| 4.4 | Reject assignment of a `NON_GRANTABLE` permission with `400 NON_GRANTABLE_PERMISSION`. |
| 4.5 | Enforce exactly one role per user in the user service, with the DB constraint as backstop (ADR-014). |
| 4.6 | `IScopeGuard` filtering by `UserWarehouseScope` / `UserRestaurantScope`, server-side only. |
| 4.7 | Out-of-tenant resource → `404`; in-tenant but out-of-scope → `403` (`docs/09 §2.1`). The distinction is deliberate: `404` must not confirm existence across tenants. |
| 4.8 | Privilege-escalation guards: no self role or scope change; only `Owner` creates an `Owner`; `Admin` cannot grant beyond its own bounds (`docs/09 §2.3`). |
| 4.9 | Mass-assignment guard: command DTOs only; `companyId`, `userId`, `generatedCode`, `documentNumber`, `createdAt` are not bindable properties anywhere. |
| 4.10 | `IFinancialProjection` helper so every query projection masks cost fields identically for non-Owners (`docs/15`). |
| 4.11 | **Audit interceptor** writing `audit_logs` inside the same `SaveChanges` transaction as the business change (`docs/14 §1`). |
| 4.12 | Audit sanitization: passwords, OTPs, session tokens, connection strings never enter `old_values`/`new_values`. |
| 4.13 | Correlation-ID middleware propagating into logs and audit rows. |
| 4.14 | `/users`, `/users/{id}/scope`, `/users/{id}/permissions` endpoints with full audit coverage. |
| 4.15 | Integration tests: tenant isolation `404`; warehouse scope `403`; restaurant scope `403`; Admin cost masking; Admin audit `403`; privilege escalation blocked; over-posted `companyId` ignored; audit written in-transaction; **no audit row after a rolled-back operation**. |

**Acceptance criteria.** `SEC-001`…`SEC-004`, `TEST-SEC-001`, `TEST-SEC-002`, `TEST-AUD-001` pass. A rolled-back operation leaves no audit trace.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Security`.
**Risks.** Role denial implemented in a controller attribute only would be bypassed by a new controller that forgets it — it is therefore a pipeline-level handler plus an architecture test asserting every cost-bearing projection routes through `IFinancialProjection`.
**Rollback.** Authorization is cross-cutting; revert the whole phase rather than partially.

---

### Phase 5 — Master Data

**Objective.** Categories, units, suppliers, warehouses, restaurants, and items with server-generated codes.
**Prerequisites.** P4.

| # | Task |
| :--- | :--- |
| 5.1 | Implement `IDocumentSequenceService` with the transactional `UPDATE … RETURNING` allocation (`docs/28 §5.3`, ADR-025). |
| 5.2 | Tenant-timezone period derivation from `company_settings` (ADR-024). |
| 5.3 | `ItemCodeGenerator` producing `ITM-000001` via the sequence service. |
| 5.4 | Arabic normalization service (`docs/31 §4.2`) feeding `name_normalized`. |
| 5.5 | Categories CRUD with deactivate-not-delete. |
| 5.6 | Units CRUD with deactivate-not-delete. |
| 5.7 | Suppliers CRUD. |
| 5.8 | Warehouses CRUD. |
| 5.9 | Restaurants CRUD, **including the required `defaultServingWarehouseId`** (`restaurants:manage`, `Owner`/`Admin` only, audited — ADR-028 SW-6). Creating a restaurant without an active serving warehouse is rejected. |
| 5.10 | Items CRUD; `generatedCode` server-only; `400 GENERATED_FIELD_NOT_ACCEPTED` when supplied (`docs/28 §5.1`). |
| 5.11 | Enforce base-unit immutability once ledger rows exist (ADR-023). |
| 5.12 | Deactivation guard: an entity referenced by transactions can be deactivated but never deleted (`docs/04 §4`). |
| 5.13 | Keyset pagination helper (`company_id, created_at DESC, id DESC`), default 25, max 100 (`docs/21 §1`). |
| 5.14 | Arabic-normalized search on items. |
| 5.15 | Unit tests: code format, normalization, uniqueness on normalized names. |
| 5.16 | Integration tests: 1,000 concurrent item creations yield 1,000 distinct **gap-free** codes; rollback leaves `last_value` unchanged; month-boundary period; supplied code rejected; duplicate normalized name → `409`. |

**Acceptance criteria.** `AC-28-1`…`AC-28-5`, `TEST-ITM-001` pass.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=MasterData`.
**Risks.** Sequence contention under load — measured in task 5.16, not assumed. Over-aggressive normalization merging genuinely distinct products — normalization affects the search key only; the display value is untouched.
**Rollback.** Per-entity revert; no stock data exists yet.

---

### Phase 6 — Unit Conversion System

**Objective.** Server-authoritative conversion of every entered quantity to the item's base unit.
**Prerequisites.** P5.

| # | Task |
| :--- | :--- |
| 6.1 | `ItemUnitConversion` CRUD; factor `> 0`; `numeric(18,6)`. |
| 6.2 | Enforce `to_base_unit_id == items.base_unit_id` (ADR-023, `29` §4.3). |
| 6.3 | `IUnitConversionResolver`: given `(itemId, unitId, quantity)` return the base quantity. Identity factor when the unit is the base unit. |
| 6.4 | Reject any client-submitted factor or base quantity at the DTO level (`docs/25`). |
| 6.5 | Immutability of a used conversion; corrections create a new row and deactivate the old (ADR-023). |
| 6.6 | `409 CONVERSION_NOT_DEFINED` when no active conversion exists for the submitted unit. |
| 6.7 | Unit tests: `1 carton = 12 KG` → `20 cartons = 240 KG`; different factors per item; identity case; missing conversion; zero and negative factors rejected; precision at `18,6` with no drift. |
| 6.8 | Integration test: a request submitting `conversionFactor` has it discarded and the server's factor applied. |

**Acceptance criteria.** `TEST-UNT-001`, `TEST-UNT-CONV` pass. No client factor is ever honoured.
**Verification.** `dotnet test tests/Inventory.UnitTests --filter Category=Conversion`.
**Risks.** Rounding drift across repeated conversions — mitigated by `decimal` end to end and by never round-tripping through a display unit.
**Rollback.** Self-contained.

---

### Phase 7 — Stock Engine Foundation

**Objective.** The ledger, balances, costing, and the transactional/concurrency/idempotency machinery every later mutation depends on. **No business endpoint is exposed in this phase.**
**Prerequisites.** P6.
**Rationale.** See §4.2.

| # | Task |
| :--- | :--- |
| 7.1 | `StockLedger` aggregate with append-only invariants expressed in the domain type (no public mutation surface). |
| 7.2 | `StockBalance` aggregate with a non-negative invariant. |
| 7.3 | `IStockPostingService` — the **only** path that writes stock. |
| 7.4 | Conditional atomic deduction `UPDATE … WHERE quantity >= @qty` (`docs/30 §5.1`); zero rows → `INSUFFICIENT_STOCK`. |
| 7.5 | Optimistic concurrency on WAC recomputation via `xmin` → `409 CONCURRENCY_CONFLICT` (`docs/30 §5.2`). |
| 7.6 | **Deterministic lock ordering:** balance rows locked ascending by `item_id` (`docs/30 §5.3`). |
| 7.7 | `ICostingEngine` implementing ADR-020 exactly: inbound recomputes; `INCOMING_RECONCILIATION` reverses at the originating line's `unit_cost`; issues and adjustments use the current WAC and leave it unchanged. |
| 7.8 | `IIdempotencyService` implementing `docs/30 §6.2`, including the unique-constraint race path and `409 IDEMPOTENCY_KEY_REUSE`. |
| 7.9 | Idempotency middleware enforcing the `docs/30 §6.1` required/optional matrix; `400 IDEMPOTENCY_KEY_REQUIRED`. |
| 7.10 | Store the **role-projected** DTO in the idempotency record, never the domain object (`docs/30 §10`). |
| 7.11 | Scheduled cleanup of expired idempotency records and audit partition pre-creation. |
| 7.12 | `IInTransitCalculator` implementing ADR-018 — derived, warehouse-attributed, never persisted. |
| 7.13 | **Architecture test:** no type outside `IStockPostingService` writes `StockLedger` or `StockBalance`. |
| 7.14 | Unit tests: `500 + 100 = 600`; reconcile `−20 → 580`; confirm `−18 → 562`; deduct 25 from 20 throws; WAC `100@10 + 50@16 = 12.0000`; WAC unchanged on issue; reconciliation reverses at the line's own cost. |
| 7.15 | Integration tests: two concurrent deductions of 15 from 20 — exactly one succeeds, balance 5, never negative; forced `xmin` conflict returns `409`; interleaved multi-item deadlock probe reports zero deadlocks. |

**Acceptance criteria.** `AC-30-1`, `AC-30-6`, `AC-30-8`, `TEST-INV-001`, `TEST-CONC-001`, `TEST-CST-001` pass.
**Verification.** `dotnet test tests/Inventory.UnitTests --filter Category=Stock`; `dotnet test tests/Inventory.IntegrationTests --filter Category=Concurrency`.
**Risks.** Lock ordering omitted produces deadlocks that appear only under production concurrency — the probe test in 7.15 is mandatory, not optional. Idempotency caching an unprojected DTO would leak Owner costs to a non-Owner replay — task 7.10 is security-critical.
**Rollback.** No endpoint is exposed; revert freely.

---

### Phase 8 — Receiving & Warehouse Stock Ledger

**Objective.** The first stock-increasing workflow, complete through reconciliation and reversal.
**Prerequisites.** P7.

| # | Task |
| :--- | :--- |
| 8.1 | `ReceivingOrder` aggregate with the `Draft → Submitted → Verified` / `Reversed` state machine (`docs/04 §18.3`). |
| 8.2 | `POST /receiving-orders` — draft, `REC-` number allocated at creation. |
| 8.3 | Draft line management; per-document item uniqueness (CR-023). |
| 8.4 | `POST /receiving-orders/{id}/submit` — transaction **T1**: `INCOMING_POSTED` per line, balance upsert, WAC recomputation, status, audit. Idempotency required. |
| 8.5 | `POST /receiving-orders/{id}/verify` — transaction **T2**: `INCOMING_RECONCILIATION` for the **delta only**, reversal at the line's own `unit_cost`, `Discrepancy` with a `DSC-` number, `reconciled` flag, status, audit. |
| 8.6 | Guard against a second verify (`reconciled` flag + `uq_ledger_posting`) — CR-041. |
| 8.7 | `POST /receiving-orders/{id}/reverse` — transaction **T3**; permitted from `Submitted` or `Verified`, never from `Reversed`; records actor, time, reason. |
| 8.8 | `INSUFFICIENT_STOCK` when reversal or negative reconciliation would drive the balance below zero (ADR-021). |
| 8.9 | Warehouse scope enforcement on every receiving endpoint. |
| 8.10 | Cost fields masked for every non-Owner in every receiving response. |
| 8.11 | `GET /warehouses/{id}/stock` returning `balance`, `inTransit`, `available`; costs Owner-only. |
| 8.12 | Unit tests: reconciliation posts the delta and **never** the full actual quantity. |
| 8.13 | Integration tests: Scenario 1 (`500+100=600`), Scenario 2 (`→580`, never 680 or 660), reversal, double-verify rejected, out-of-scope warehouse `403`, Admin sees `unitCost: null`, idempotent resubmit posts once. |

**Acceptance criteria.** `docs/23` Scenarios 1–2, `TEST-REC-001`, `TEST-REC-002`, `SEC-003` pass.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Receiving`.
**Risks.** The double-add bug in verification is the single most likely defect in the product — `docs/23 §2` exists specifically to catch it and its test is mandatory.
**Rollback.** Revert the receiving module; the ledger engine remains intact.

---

### Phase 9 — Multi-Item Supply Requests

**Objective.** One restaurant requisition carrying many item lines, fully editable while draft, addressed to a **server-derived** serving warehouse.
**Prerequisites.** P8. OD-008 **closed** (ADR-028) — no longer blocked.

| # | Task |
| :--- | :--- |
| 9.1 | `SupplyRequest` aggregate with `1..N` lines and the `docs/04 §18.1` state machine. |
| 9.2 | `POST /supply-requests` — draft with `REQ-` number; lines optional at creation. |
| 9.3 | `POST\|PUT\|DELETE /supply-requests/{id}/items[/{lineId}]` — **Draft only** (`docs/04 §8.1`). |
| 9.4 | Duplicate-item merge behaviour; `UNIQUE (supply_request_id, item_id)` (CR-023). |
| 9.5 | Per-line unit with server-resolved `base_quantity` (Phase 6). |
| 9.6 | `POST /supply-requests/{id}/submit` — transaction **T4**; requires ≥ 1 line, all quantities `> 0`, all items active. **Zero stock effect.** |
| 9.7 | `POST /supply-requests/{id}/cancel` per `docs/04 §8.1` rules. |
| 9.8 | **Serving-warehouse derivation (ADR-028).** `CreateSupplyRequestCommand` declares **no** `WarehouseId` property. The handler reads `restaurants.default_serving_warehouse_id`, verifies the warehouse is `Active`, and stores it on the request. Same-company-ness is structural via the composite FK (ADR-016), never an application assertion alone. |
| 9.8a | `409 SERVING_WAREHOUSE_UNAVAILABLE` when the restaurant has no active serving warehouse. **No fallback to another warehouse under any circumstance.** |
| 9.8b | The warehouse is resolved **once**, at draft creation. A later change to the restaurant's default does not retroactively redirect existing requests (SW-8). |
| 9.9 | Restaurant scope on create/edit/submit; warehouse scope on the fulfilment queue view. |
| 9.10 | `INVALID_STATE_TRANSITION` on any edit outside `Draft`. |
| 9.11 | Integration tests: multi-line create; duplicate item rejected; edit blocked when submitted; empty submit rejected; inactive item rejected; **ledger row count unchanged across the whole flow**; cross-restaurant `403`. |
| 9.12 | **Security test (ADR-028):** a supervisor posting `"warehouseId": "<another warehouse in the same company>"` has the value **discarded by the model binder**, and the created request still targets their restaurant's configured serving warehouse. Assert on the persisted `warehouse_id`, not merely on the response. |
| 9.13 | Integration test: a restaurant whose serving warehouse is deactivated returns `409 SERVING_WAREHOUSE_UNAVAILABLE` and creates nothing. |
| 9.14 | Integration test: changing a restaurant's default serving warehouse leaves existing requests pointing at the original warehouse (SW-8). |

**Acceptance criteria.** `TEST-REQ-MULTI`, `TEST-SW-DERIVED`, `TEST-SW-UNAVAILABLE` pass; `docs/23` Scenario 3 first half (request has zero stock effect); an over-posted `warehouseId` provably has no effect on the persisted row.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=SupplyRequest`.
**Risks.** An implementer "helpfully" adding a `warehouseId` property to the command DTO to make it "more flexible" would reintroduce the exact authorization hazard ADR-028 removes — task 9.12 asserts on the persisted value specifically so that such a change fails the test rather than passing it.
**Rollback.** Self-contained.

---

### Phase 10 — Fulfilment & Dispatch

**Objective.** Warehouse fulfilment producing a `Prepared` supply, then a separate dispatch act. Neither touches stock.
**Prerequisites.** P9.

| # | Task |
| :--- | :--- |
| 10.1 | `Supply` aggregate with the ADR-017 state machine (`Prepared → Dispatched → Confirmed \| ConfirmedWithDiscrepancy \| RejectedAtDelivery`; `Cancelled` only from `Prepared`). |
| 10.2 | `POST /supply-requests/{id}/fulfill` — transaction **T5**: `SUP-` number, `Supply` in `Prepared`, `supply_items` linked to `supply_request_items` (CR-024), `fulfilled_quantity` updated, request → `PartiallyFulfilled`/`Fulfilled`. **Zero stock effect.** |
| 10.3 | Validate `fulfilled ≤ requested` per line; `0` permitted (out of stock). |
| 10.4 | `POST /supplies/{id}/dispatch` — transaction **T6**: `Prepared → Dispatched`, actor and timestamp. **Zero stock effect.** |
| 10.5 | `POST /supplies/{id}/cancel` — `Prepared` only. |
| 10.6 | Sufficiency **advisory** at fulfilment and dispatch against `available = balance − inTransit` — warns, never blocks, never reserves (ADR-019). |
| 10.7 | Warehouse scope on both endpoints. |
| 10.8 | Integration tests: **`SELECT COUNT(*) FROM stock_ledger` is identical before and after fulfilment and dispatch**; balance byte-identical; partial fulfilment; zero-fulfilled line; advisory raised without blocking; `Cancelled` refused after dispatch; cross-warehouse `403`. |

**Acceptance criteria.** `docs/23` Scenario 3 passes: stock remains **exactly** 580 and **no ledger row is written**. `TEST-SUP-001`, `TEST-SUP-DISPATCH` pass.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Dispatch`.
**Risks.** A well-meaning implementer "reserving" stock at dispatch would reintroduce the phantom balance ADR-003 exists to prevent. The ledger-row-count assertion in 10.8 is the guard, and it must be written as an absolute count, not a delta tolerance.
**Rollback.** Self-contained.

---

### Phase 11 — Restaurant Receipt Confirmation

**Objective.** The **only** stock-deducting path in the product.
**Prerequisites.** P10.

| # | Task |
| :--- | :--- |
| 11.1 | `POST /supplies/{id}/confirm` — transaction **T7**, idempotency **required**. |
| 11.2 | Validate `0 ≤ received ≤ dispatched` per line. |
| 11.3 | Lock balance rows ascending by `item_id` (`docs/30 §5.3`). |
| 11.4 | Append `RESTAURANT_RECEIPT_CONFIRMED` per line for the **actual received** quantity, valued at the current WAC, leaving the WAC unchanged (ADR-020). |
| 11.5 | Conditional atomic deduction; any short line rolls back the **entire** confirmation. |
| 11.6 | Create a line-level `Discrepancy` (`reference_line_id`, `DSC-` number) for every non-zero variance. |
| 11.7 | Terminal status: all-full → `Confirmed`; any variance → `ConfirmedWithDiscrepancy`; all zero → `RejectedAtDelivery` with **no ledger row** (`docs/04 §11`). |
| 11.8 | Restaurant scope enforcement — a supervisor may confirm only their own branch's supply. |
| 11.9 | `409 ALREADY_CONFIRMED` on a second confirmation outside the idempotency window. |
| 11.10 | Implement the impossible-confirmation path of `docs/30 §7.1`: `INSUFFICIENT_STOCK`, supply stays `Dispatched`, `StockUnavailableAtConfirmation` discrepancy raised. **No auto-adjustment.** |
| 11.11 | Integration tests: Scenario 4 (`580 → 560`), Scenario 5 (`580 → 562`, variance `−2` logged), Scenario 6 (concurrent 15+15 from 20), full rejection writes no ledger row, idempotent replay deducts once, cross-branch `403`, five-line confirmation failing on one line writes **nothing**. |

**Acceptance criteria.** `docs/23` Scenarios 4, 5, 6 and `SEC-004`, `TEST-SUP-002`, `AC-30-1`, `AC-30-2`, `AC-30-5` pass.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Confirmation`.
**Risks.** Partial-line commit would corrupt inventory silently; the all-or-nothing assertion in 11.11 is mandatory. A `RejectedAtDelivery` that writes a compensating ledger pair would violate `docs/04 §11`.
**Rollback.** Highest-consequence module. Revert as a whole; never partially.

---

### Phase 12 — Discrepancies & Reconciliation

**Objective.** A single lifecycle for every variance the system produces.
**Prerequisites.** P11.

| # | Task |
| :--- | :--- |
| 12.1 | `Discrepancy` aggregate with `Open → Investigating → Resolved`. |
| 12.2 | Types: `ReceivingVariance`, `SupplyReceiptVariance`, `StockCountVariance`, `StockUnavailableAtConfirmation`. |
| 12.3 | `GET /discrepancies` — scoped: warehouse staff see warehouse variances; supervisors see their branch's receipt variances. |
| 12.4 | `POST /discrepancies/{id}/resolve` with a required Arabic reason; audited. |
| 12.5 | Resolution never posts a stock movement — reconciliation of stock happens only through a stock count (ADR-021). |
| 12.6 | Line-level drill-through via `reference_line_id`. |
| 12.7 | Integration tests: variance auto-created by receiving verify and by confirmation; scope isolation; resolve writes audit; **resolve writes no ledger row**. |

**Acceptance criteria.** `TEST-DSC-LOG` passes; every discrepancy traces to its source line.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Discrepancy`.
**Risks.** Allowing "resolve" to adjust stock would create a second, unaudited stock-mutation path — explicitly forbidden by task 12.5 and asserted in 12.7.
**Rollback.** Self-contained.

---

### Phase 13 — Physical Stock Counts & Adjustments

**Objective.** Reconciling ledger balances with physical reality, correctly in the presence of in-transit goods.
**Prerequisites.** P12.

| # | Task |
| :--- | :--- |
| 13.1 | `StockCount` aggregate with the `docs/04 §18.4` state machine. |
| 13.2 | `POST /stock-counts` — `CNT-` number; snapshot system quantities; `is_blind_count` flag. |
| 13.3 | Expected physical = ledger balance **− in transit** (ADR-018). This is the reason Phase 13 follows Phase 10. |
| 13.4 | `POST /stock-counts/{id}/record` — per-line physical quantities; blind mode suppresses system figures server-side, not merely in the UI. |
| 13.5 | Variance computation `physical − expected`. |
| 13.6 | `POST /stock-counts/{id}/approve` — transaction **T8**, `Owner`/`Admin` only, idempotency required: `PHYSICAL_ADJUSTMENT` per non-zero variance, balance update, `Discrepancy`, status, audit. |
| 13.7 | `POST /stock-counts/{id}/reject` returning to `InProgress` with **no** stock effect. |
| 13.8 | Adjustments valued at the current WAC, leaving the WAC unchanged (ADR-020). |
| 13.9 | `INSUFFICIENT_STOCK` if a negative adjustment would breach zero (ADR-021). |
| 13.10 | Count lines immutable after approval. |
| 13.11 | Integration tests: variance posts an adjustment; zero variance posts nothing; rejection posts nothing; **blind mode never transmits the system quantity over the wire**; warehouse staff cannot approve (`403`); in-transit correctly excluded from the expected figure. |

**Acceptance criteria.** `TEST-CNT-001`, `TEST-CNT-ADJUST` pass; a count during transit produces **no** spurious adjustment.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=StockCount`.
**Risks.** Blind counting implemented only in the UI leaks the system quantity in the API payload and defeats the entire control — task 13.4 and its test are the guard. Ignoring in-transit here reintroduces CR-030 as a live data-destruction bug.
**Rollback.** Self-contained.

---

### Phase 14 — Audit Viewer & Activity Monitor (Owner)

**Objective.** Owner-facing surfaces over the audit trail built in Phase 4.
**Prerequisites.** P13.

| # | Task |
| :--- | :--- |
| 14.1 | `GET /audit` with keyset pagination and filters (actor, entity type, date range). **Owner only.** |
| 14.2 | `GET /audit/activity` — Arabic human-readable stream. **Owner only, no grant path** (ADR-013). |
| 14.3 | Bounded default date window (`docs/17 §1.3`). |
| 14.4 | Export writes its own audit entry. |
| 14.5 | Integration tests: Admin `403` on both endpoints; a granted `audit:view` still yields `403`; no secret appears in any `old_values`/`new_values`; every phase's mutations are present in the trail. |

**Acceptance criteria.** `SEC-002`, `TEST-SEC-002`, `TEST-AUD-OWNER` pass.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Audit`.
**Risks.** A granted permission slipping past denial — asserted directly in 14.5.
**Rollback.** Read-only surfaces; safe to revert.

---

### Phase 15 — File Storage & Evidence *(🚫 DEFERRED — ADR-029)*

> **Not in MVP scope.** No generic file-management subsystem is built. This phase activates **only** when an already-approved workflow explicitly requires an attachment. None currently does. `stored_files` and `file_attachments` are specified in `docs/29 §4.2` but are **not migrated** in Phase 2.
>
> The task list below is preserved as the future baseline and is **not scheduled**.

**Prerequisites.** An approved workflow that requires an attachment.

| # | Task |
| :--- | :--- |
| 15.1 | `IFileStorageService` with a local-disk implementation (OD-002). |
| 15.2 | Storage key `tenants/{companyId}/{yyyy}/{MM}/{uuid}.{ext}`; client filenames never touch the filesystem. |
| 15.3 | MIME sniffing by magic number; whitelist `image/jpeg`, `image/png`, `image/webp`, `application/pdf`; executables rejected. |
| 15.4 | SHA-256 computation and `stored_files` persistence. |
| 15.5 | `file_attachments` linking a file to its document (CR-066). |
| 15.6 | Download authorization by tenant **and** by the parent document's scope. |
| 15.7 | Size limit and per-user upload rate limit. |
| 15.8 | Integration tests: path traversal rejected; renamed executable rejected by sniffing; cross-tenant download `403`/`404`; oversized upload rejected. |

**Acceptance criteria.** No unauthenticated or cross-tenant file access is possible.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Files`.
**Risks.** MIME sniffing by extension alone is a remote-code-execution vector on any host that later serves the directory.
**Rollback.** Self-contained; delete the storage root.

---

### Phase 16 — Reporting & Analytics *(🚫 DEFERRED — ADR-029)*

> **Deferred as implementation; reportability preserved.** No report endpoint, no export pipeline, no `/reports` route, and **no placeholder charts or fake KPIs**. An unimplemented report is shown as nothing at all.
>
> **Binding on the core phases regardless:** `docs/17 §4` Reporting Extension Points. Data not captured during the core phases cannot be recovered later — every ledger row carries full actor, cost, reference, and timing metadata (ADR-020) even where the WAC is unchanged; audit retains full diff context; documents stay line-traceable; the `docs/29 §5` indexes already cover future aggregation paths.
>
> The task list below is preserved as the future baseline and is **not scheduled**.

**Prerequisites.** Core inventory and transactional workflows complete (P1–P14).

| # | Task |
| :--- | :--- |
| 16.1 | The six reports of `docs/17 §2`. |
| 16.2 | Scope enforcement inside every report query, not as a post-filter. |
| 16.3 | `RPT_STOCK_VALUATION` **Owner only**; `403` for every other role. |
| 16.4 | `RPT_STOCK_MOVEMENT` masks `unit_cost`/`total_cost` for non-Owners (`docs/17` × `docs/15`). |
| 16.5 | Mandatory bounded date window; keyset or explicit `Take`. |
| 16.6 | Arabic RTL PDF export with verified glyph shaping; CSV with UTF-8 BOM (`docs/31 §4.9`). |
| 16.7 | Every generation and export writes an audit entry. |
| 16.8 | Integration tests: Admin valuation `403`; Admin movement report contains no cost column at all; out-of-scope warehouse excluded; unbounded date range rejected; CSV BOM present. |

**Acceptance criteria.** No cost figure reaches a non-Owner through any report or export.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Reporting`.
**Risks.** A report is the easiest place to accidentally bypass DTO projection by selecting raw entities — task 16.4's test asserts column **absence**, not a null value.
**Rollback.** Self-contained.

---

## 10. Frontend Phases

> Every frontend phase is bound by `docs/31` (Arabic/RTL), the UI/UX guide (including its §8 amendment), and `docs/11`. `ui-ux-pro-max/SKILL.md` is read before the first component is authored.

### Phase F1 — Application Shell & Design System *(parallel with P1–P4)*

| # | Task |
| :--- | :--- |
| F1.1 | Read `ui-ux-pro-max/SKILL.md` and its `data/stacks/nextjs.csv`, `shadcn.csv`, `colors.csv`, `typography.csv`, `ux-guidelines.csv`. Record reconciled decisions in the UI/UX guide. |
| F1.2 | `<html dir="rtl" lang="ar">`; self-host `Noto Sans Arabic` and `Plus Jakarta Sans` under `public/fonts`. |
| F1.3 | Tailwind theme from the UI/UX guide §4 token set. |
| F1.4 | ESLint rule banning physical direction utilities (`docs/31 §4.3`). |
| F1.5 | `src/lib/formatters.ts` — the sole owner of numbers, currency, dates, quantities, and phone formatting (`docs/31 §4.5`). |
| F1.6 | Status-badge component driven by the `docs/31 §4.6` mapping. |
| F1.7 | `src/components/ui`: Button, Input, Select, Dialog, Table, Badge, Card. |
| F1.8 | `src/components/feedback`: LoadingSkeleton, EmptyState, ErrorBanner, ForbiddenState. |
| F1.9 | RTL app shell: right-hand sidebar, header, breadcrumbs, mobile drawer. |
| F1.10 | `DataTable` with sticky header, keyset pagination controls, and `< 768px` card collapse. |
| F1.11 | `src/lib/api-client.ts`: `credentials: 'include'`, automatic `X-CSRF-TOKEN`, RFC 7807 parsing surfacing `messageAr`, 401 → `/login`. **No browser storage of any token.** |
| F1.12 | Vitest + RTL harness; tests for formatters and badge mapping. |

**Acceptance.** `AC-31-2`, `AC-31-3`, `AC-31-7` pass. Storybook-equivalent review of every component in RTL.
**Verification.** `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build` — all `--prefix frontend`.
**Risk.** Building against a generic LTR admin template — explicitly forbidden by the UI/UX guide §1.1.

### Phase F2 — Auth & Authenticated Shell *(after P3)*
Login, forgot-password/OTP/reset, session bootstrap via `/account/me`, permission-driven navigation (`docs/12`), role badge and scope display, three distinguishable unauthorized states (UI/UX guide §8.6), `/audit` route unregistered for non-Owners, **`/reports` route not registered at all** (deferred — ADR-029).
**Acceptance.** `TEST-NAV-001`, `TEST-NAV-002`. **Verification.** `npm run test`, `npm run test:e2e` (auth spec).

### Phase F3 — Master-Data Workflows *(after P5)*
Items list and form with read-only generated code; categories, units, suppliers, warehouses, restaurants; quick-add modal (`UI/UX guide §5.3`) with auto-select and zero form-state loss; Arabic-normalized search; all five API states everywhere.
**Acceptance.** `TEST-E2E-001`, `REQ-18`. **Verification.** `npm run test`, `npm run test:e2e`.

### Phase F4 — Receiving & Inventory Workflows *(after P8)*
Receiving list, draft editor, post, verification with variance entry, reversal; warehouse stock view showing balance / in-transit / available; cost columns rendered only for Owner.
**Acceptance.** `TEST-REC-001`, `TEST-REC-002` at the browser level.

### Phase F5 — Supply Workflows *(after P11)*
The multi-item request editor (UI/UX guide §8.3) — **with no warehouse selector**, showing the server-derived serving warehouse read-only on the created request (ADR-028); fulfilment queue with per-line quantities and the sufficiency advisory; dispatch; the confirmation screen (§8.4) with per-line received quantities, required variance reasons, a pre-submit summary dialog, and an idempotency key generated once per form instance; discrepancy views.
**Acceptance.** `TEST-SUP-CONFIRM`, `TEST-REQ-MULTI` at the browser level. This is the highest-value E2E surface in the product.

### Phase F6 — Role-Specific Dashboards *(after P13)*
Warehouse-staff and restaurant-supervisor action dashboards (`docs/12 §2`); Owner and Admin overviews. **No report widgets, no charts, no KPI cards backed by anything but a real count** (ADR-029). **Every figure is a real API count.** Failed widgets show an Arabic error banner with retry; empty widgets show an Arabic empty state.
**Acceptance.** A repository scan finds no hardcoded `0`, mock array, placeholder chart, or deferred-feature stub screen (`docs/12 §2.3`, `docs/17 §4.6`).

---

## 11. Verification Phases

### Phase S1 — Security Hardening & Adversarial Regression
CSP, HSTS, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`; TLS configuration (`docs/20 §2`); dependency vulnerability audit (`dotnet list package --vulnerable`, `npm audit`); secret scan proving zero plaintext secrets in git; strip `U+202E` from all input (`docs/31 §7`); suppress `messageEn` in Production (CR-072); adversarial regression suite for IDOR, mass assignment, privilege escalation, tenant traversal, financial leakage across **every** endpoint including reports and exports.
**Acceptance.** `SEC-001`…`SEC-006` pass; `PATCH /stock-balances` and `POST /stock-ledger` return `404`/`405`.
**Verification.** `dotnet test tests/Inventory.IntegrationTests --filter Category=Security`.

### Phase T1 — Full Automated Test Sweep
Every suite green; coverage reviewed against `docs/26` so that **every** requirement has at least one test; architecture tests re-verified; the `docs/23` numerical scenarios run as one continuous sequence.
**Acceptance.** `dotnet test InventorySystem.sln` and `npm run test --prefix frontend` both fully green, zero skipped tests.

### Phase T2 — Browser E2E & Acceptance Verification
Playwright over real HTTP and real cookies: warehouse-staff journey and restaurant-supervisor journey (`docs/04 §19`); the full `docs/23` numerical walkthrough driven through the UI; Arabic RTL rendering; PDF and CSV export inspection (`AC-31-5`, `AC-31-6`); mobile viewport at `< 768px`; keyboard-only navigation and WCAG 2.1 AA checks.
**Acceptance.** All `docs/23` scenarios reproduce end-to-end through the browser.
**Verification.** `npm run test:e2e --prefix frontend`.

### Phase R1 — Documentation & Release Readiness
Reconcile every specification against what was built; complete `26-traceability-matrix.md` with real file paths and test IDs; close `27-implementation-status.md`; close or carry forward every open decision; verify `run-local.bat` / `check-local.bat` / `stop-local.bat` on a clean machine; production deployment rehearsal (`docs/20`); backup and PITR verification.
**Acceptance.** `MISSING_FEATURES = 0`, `PARTIAL_IMPLEMENTATIONS = 0`, `SPECIFICATION_CONTRADICTIONS = 0` — **verified**, not asserted.

---

## 12. Checkpoints

At each checkpoint the listed work is complete, tested, and documented, and the listed work is **explicitly not yet built**. A checkpoint is a stop-and-review point, not a formality.

| # | Name | Must Be Working | Must Be Tested | Must Be Documented | Must **NOT** Exist Yet |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | Architecture & Database | P1–P2. Build green, migration applies, tenancy structural. | Architecture tests; migration integrity; prohibited-identifier scan. | `29` reconciled with the real migration. | Any business endpoint. Any stock logic. |
| **2** | Identity & Authorization | P3–P4, F1–F2. Login, sessions, CSRF, scopes, role denial, audit infrastructure. | Full security suite; audit-in-transaction. | `03`, `08`, `09`, `14` reconciled. | Any master data. Any stock movement. |
| **3** | Master Data & Conversions | P5–P6, F3. Catalog, codes, conversions. | Concurrent code generation; conversion math. | `28` reconciled. | Any ledger row. |
| **4** | Stock Engine & Receiving | P7–P8, F4. Ledger, balances, WAC, receiving. | `docs/23` Scenarios 1–2; concurrency; deadlock probe. | `04`, `30` reconciled. | Supply requests. Dispatch. Confirmation. |
| **5** | Supply Workflow | P9–P11, F5. Requests, fulfilment, dispatch, confirmation. | `docs/23` Scenarios 3–6; idempotency; scope isolation. | `04 §8–§11` reconciled. | Stock counts. Reports. Audit viewer. |
| **6** | Full Operational Product | P12–P14, F6. Discrepancies, counts, audit viewer, dashboards. | Count-during-transit; blind counting; Owner-only audit. | `26` complete for all core requirements. | Files and Reports (deferred — ADR-029). Production hardening. |
| **7** | Security & Release | S1, T1, T2, R1. **P15 and P16 are deferred and are not part of this checkpoint** (ADR-029). | Full adversarial suite; complete E2E. | Every specification reconciled with the code. | Any report surface. Any file-management subsystem. |

---

## 13. Risk Register

| Risk | Likelihood | Impact | Mitigation |
| :--- | :---: | :---: | :--- |
| Composite tenant FKs omitted from the initial migration | Medium | **Critical** | Phase 2 tasks 2.6, 2.30; architecture test; Checkpoint 1 gate. |
| Reconciliation double-adds the actual quantity | **High** | Critical | `docs/23` Scenario 2; unit test 8.12; called out explicitly in three documents. |
| Dispatch implemented as a stock deduction or a reservation | Medium | Critical | Absolute ledger-row-count assertion in 10.8; ADR-003/004/019. |
| `xmin` misconfigured, silently disabling lost-update detection | Medium | **Critical** | Integration test 7.15 forcing a real conflict. |
| Stock count during transit destroys inventory value | Medium | **Critical** | ADR-018; task 13.3; test 13.11. |
| Financial data leaking through a report or export | Medium | High | `IFinancialProjection`; column-absence assertions; S1 sweep. |
| Audit retrofitted late and silently missing on some handlers | Was High | High | **Mitigated by reordering** — audit infrastructure lands in Phase 4. |
| Idempotency caching an unprojected DTO leaks Owner costs | Low | High | Task 7.10; `docs/30 §10`. |
| Deadlock under concurrent multi-item confirmation | Medium | High | Deterministic lock ordering (7.6); deadlock probe (7.15). |
| ~~Framework versions unavailable~~ | — | — | **Closed.** Backend toolchain verified present (`docs/33 §4.1`). Node's exact version and `PATH` resolution confirmed by task 1.1. |
| Arabic PDF renders as disconnected reversed glyphs | **High** | Medium | `docs/31 §4.9`; visual verification, not error-free generation. |
| Blind counting leaked in the API payload | Medium | Medium | Task 13.4 enforces server-side suppression; test 13.11. |
| **PostgreSQL not installed when Phase 2 starts** | **High** | **Critical** | `docs/33 §4.3`; hard block on Phase 2; ADR-027 forbids substituting SQL Server / MySQL / Oracle, all of which are present on the machine and would look convenient. |
| An implementer adds `warehouseId` to the supply-request DTO "for flexibility" | Medium | High | ADR-028; task 9.12 asserts on the **persisted** warehouse, so such a change fails the test rather than passing it. |
| A placeholder report or KPI ships because reports are "coming later" | Medium | Medium | ADR-029; `docs/17 §4.6` forbids it explicitly; `docs/12 §2.3` scan in F6. |
| Ledger metadata trimmed as "unused" because reports are deferred | Low | **High** | `docs/17 §4.1` — a cost or actor not recorded at posting time is unrecoverable. Any reduction requires a superseding ADR. |
| Frontend built against an imagined API | Medium | Medium | No mock-API track; each F phase blocks on its backend phase. |

---

## 14. Rollback Considerations

| Layer | Strategy |
| :--- | :--- |
| Migrations | Forward-only after Checkpoint 1. Never edit a shared migration; write a corrective one. Every migration is reviewed as generated SQL before commit. |
| Stock data | Never mutated directly. A wrong ledger row is corrected by a reversing row, never by an `UPDATE` — which the database physically prevents. |
| Feature revert | Each phase is a reviewable unit. Cross-cutting phases (P4, P7) are reverted whole, never partially. |
| Frontend | Independently deployable; a frontend revert never affects data. |
| Configuration | All secrets in environment variables; a rollback never requires a secret change. |

---

## 15. Acceptance Criteria for This Plan

- Every phase states prerequisites, tasks, acceptance criteria, verification commands, risks, and rollback.
- Every task is independently understandable and independently reviewable.
- The dependency graph is acyclic and every parallel track states its condition.
- Every ordering departure from the brief is justified in §4.
- Every blocking open decision is bound to the phase it blocks.
- No task requires inventing a business rule.

## 16. Related Documents

`docs/README.md`, `docs/decision-log.md`, `docs/open-decisions.md`, `docs/32-specification-conflict-register.md`, `docs/04-end-to-end-business-flow.md`, `docs/06-database-schema.md`, `docs/28`, `docs/29`, `docs/30`, `docs/31`, `docs/18-testing-strategy.md`, `docs/23-acceptance-criteria.md`, `docs/26-traceability-matrix.md`, `docs/27-implementation-status.md`.

---

## 17. Decision-Closure Amendment (2026-09-10)

| Change | Source |
| :--- | :--- |
| §6 rewritten: all four blocking decisions closed; the sole remaining blocker is environmental (PostgreSQL not installed). | ADR-027 … ADR-030 |
| Phase 1 unblocked; Task 1.1 rewritten to **execute** verification and record literal output in `docs/33 §7`; tasks 1.1a–1.1c added (`global.json` pin, `PATH`, container runtime). | ADR-027 |
| Phase 2 prerequisite now includes a working PostgreSQL 16+; task 2.18a adds the serving-warehouse column; task 2.19 no longer migrates the file tables. | ADR-027, ADR-028, ADR-029 |
| Phase 5 task 5.9 requires `defaultServingWarehouseId` on restaurants. | ADR-028 |
| Phase 9 unblocked; tasks 9.8, 9.8a, 9.8b, 9.12, 9.13, 9.14 added for derivation, the unavailable case, resolve-once semantics, and the over-post security test. | ADR-028 |
| Phases 15 and 16 marked **DEFERRED — not scheduled**; their task lists preserved as future baselines. | ADR-029 |
| Checkpoint 6 and 7 scopes corrected; F2, F5, F6 updated for the removed warehouse selector and the absent reports route. | ADR-028, ADR-029 |
| Four risks added, including PostgreSQL absence and ledger-metadata trimming. | ADR-027, ADR-029 |

### 17.1 Why Deferring Reports Does Not Reduce Core Work

`docs/17 §4` is binding on Phases 7–13 even though no report is built. Data not captured at posting time cannot be recovered afterwards: `stock_ledger.unit_cost` is populated for **every** movement type (ADR-020) including issues where the WAC does not move, `actor_user_id` is recorded on every row, `occurred_at` and `created_at` stay distinct, and the line-level links of `docs/04 §25` are built in the core phases. Trimming any of it as "unused because reports are deferred" is a permanent, silent data loss and requires a superseding ADR.
