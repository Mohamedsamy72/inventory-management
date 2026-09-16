# 32 — Specification Conflict & Gap Register

> **Document ID:** SPEC-32
> **Topic:** Classified register of every contradiction, ambiguity, and missing requirement discovered during the Clean-Restart reconciliation pass
> **Status:** Authoritative — Living Register
> **Authority:** Subordinate to `docs/decision-log.md`. Items marked **REQUIRES DECISION** are mirrored in `docs/open-decisions.md`.

---

## 1. Purpose

This register is the permanent record of the specification reconciliation performed before implementation began. It exists so that:

1. No conflict is silently merged or silently resolved.
2. Every resolution is traceable to an ADR or to an open decision.
3. Future agents can prove that a rule was reviewed rather than invented.

## 2. Scope

Covers all documents in `docs/` as of the reconciliation pass, plus the final business rules supplied in the project re-initialisation brief.

## 3. Classification Taxonomy

| Class | Meaning | Handling |
| :--- | :--- | :--- |
| **A — Obsolete** | Requirement superseded by a later decision. | Removed or annotated; never silently deleted. |
| **B — Historical** | Implementation detail of a retired architecture. | Preserved for context, marked non-binding. |
| **C — Authoritative** | Current binding requirement; conflict was only apparent. | Clarified in place. |
| **D — Genuine Conflict** | Two current documents state incompatible rules. | Resolved by ADR, or escalated. |
| **E — Missing Specification** | A required rule has no authoritative home. | New spec written, or escalated. |

## 4. Terminology

Uses `docs/glossary.md` exclusively. `Supply` = dispatch document. `SupplyRequest` = restaurant requisition. `StockLedger` = append-only movement table.

---

## 5. Conflict Register

### 5.1 Documentation Architecture

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-001** | D | Two documents share number `04`: `04-inventory-stock-model.md` (SPEC-04) and `04-end-to-end-business-flow.md` (SPEC-FLOW-04). | Doc hierarchy | Both retained. `SPEC-FLOW-04` is the **master business-flow authority**; `SPEC-04` is the **stock-model authority**. Distinct `Document ID` values disambiguate them. Renumbering is deferred — see **OD-007**. |
| **CR-002** | D | The supply lifecycle is specified three times: `04-end-to-end-business-flow.md`, `05-business-workflows.md`, `system-master-flow.md`. Drift risk is high. | Doc hierarchy | `04-end-to-end-business-flow.md` is authoritative. `05` is a **subordinate state-machine summary**; `system-master-flow.md` is a **non-normative topology overview**. Declared in `docs/README.md` §2. |
| **CR-003** | E | The re-initialisation brief names `docs/08-frontend-ui-ux-implementation-guide.md` and `docs/09-implementation-plan.md`, but `08` and `09` are already occupied by authentication and authorization specs. | Doc hierarchy | Existing numbering preserved (it is referenced by `README`, `26`, and every ADR). Canonical **alias stubs** created at the requested paths; they contain zero specification content. See **OD-007**. |
| **CR-004** | D | `27-implementation-status.md` asserts `Total Inconsistent Business Rules: 0` and `100% READY`. This register disproves it. | `27` | `27` rewritten. The zero-missing gate is now a *forward* gate, not a *retrospective* claim. |

### 5.2 Roles, Authorization & Financial Lockdown

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-010** | D | `07 §1.3` masks financial fields for `Admin`, `WarehouseStaff`, `RestaurantSupervisor` — omitting `User`. `15 §2` masks for **every** non-Owner role including `User`. | `07`, `15`, `03` | **Resolved (ADR-012):** financial visibility is `Owner`-only, unconditionally. `07` is the incomplete statement and is corrected. |
| **CR-011** | D | `03 §3` publishes `costs:view` / `valuation:view` as grantable permissions, but ADR-012 makes them unreachable for any non-Owner. A grantable permission that can never take effect is a security trap. | `03` | **Resolved (ADR-012):** these two codes are **non-grantable**. They remain in the catalog as documentation of Owner capability only, flagged `NON_GRANTABLE`. Same for `audit:view` / `audit:export`. |
| **CR-012** | D | `14 §3` states the Activity Monitor is *"Owner Only (Admin restricted **unless granted**)"* — the grant path directly contradicts the FINAL rule "Admin has no Audit Log visibility". | `14` | **Resolved (ADR-013):** Activity Monitor is Owner-only with **no grant path**. `14` corrected. |
| **CR-013** | D | Every code sample treats a user's role as singular (`user.Role == Roles.Owner`), but `user_roles` is a many-to-many join table with a composite PK permitting N roles. Role Denial is undefined when a user holds both `Admin` and `Owner`. | `06`, `03`, `09` | **Resolved (ADR-014):** exactly **one** product role per user. Enforced by `UNIQUE (user_id)` on `user_roles`. Role Denial then has a single unambiguous subject. |
| **CR-014** | C | `05 §3` assigns stock-count initiation to a *"warehouse manager"* — not one of the five roles. | `05` | Clarified: `stock_counts:create` is held by `Owner` and `Admin` per `03`. Warehouse Staff **count** (`stock_counts:count`) but do not open or approve. |
| **CR-015** | E | `Admin` holds `receiving:reverse`. Reversal recomputes Weighted Average Cost, which is financial data. Whether an Admin may trigger a cost-bearing recomputation was never stated. | `03`, `15` | **Resolved (ADR-015):** Admin **may** trigger reversal; the server performs the cost recomputation and returns **no** cost field to the Admin. Authority over an operation is separable from visibility of its financial output. |
| **CR-016** | E | Nothing forbids a user from being scoped to a warehouse or restaurant belonging to a different company. | `06`, `09` | **Resolved (ADR-016):** composite tenant-consistent foreign keys — see `docs/29`. |

### 5.3 Supply Lifecycle & Document States

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-020** | D | `07` exposes **both** `POST /supply-requests/{id}/fulfill` (which "creates a Supply/Dispatch") **and** `POST /supplies/{id}/dispatch`. If fulfilment already produces a dispatched supply, the second endpoint is meaningless; if it does not, the supply has an undocumented intermediate state. | `07`, `05`, `06` | **Resolved (ADR-017):** fulfilment creates a `Supply` in state **`Prepared`** (`قيد التجهيز`). `dispatch` transitions `Prepared → Dispatched`. Two distinct, auditable business acts. |
| **CR-021** | D | Three different `Supply` status vocabularies: `06` DDL comment `'Dispatched','Confirmed','Discrepancy'`; `05` `Dispatched → Confirmed`; `04 §18.2` `Dispatched → Confirmed \| ConfirmedWithDiscrepancy \| RejectedAtDelivery`. | `04`, `05`, `06` | **Resolved:** `04 §18.2` governs, extended by ADR-017. Canonical set: `Prepared`, `Dispatched`, `Confirmed`, `ConfirmedWithDiscrepancy`, `RejectedAtDelivery`, `Cancelled`. `Discrepancy` is **not** a status. |
| **CR-022** | E | `SupplyRequest` has a `Draft` state in `04 §18.1` and in the `06` DDL, but `07` offers no `submit`, `cancel`, or line-editing endpoints. The brief mandates add / edit / remove lines. | `07`, `04` | **Resolved:** endpoints specified — `POST /supply-requests/{id}/submit`, `POST /supply-requests/{id}/cancel`, `PUT /supply-requests/{id}` (Draft only), `POST\|PUT\|DELETE /supply-requests/{id}/items[/{lineId}]` (Draft only). Documented in `04 §8`. |
| **CR-023** | E | No rule for a duplicate item appearing twice in one multi-item request. | `04`, `06` | **Resolved:** `UNIQUE (supply_request_id, item_id)` — one line per item per document. The UI merges a re-added item into the existing line. Same rule for `supply_items`, `receiving_order_items`, `stock_count_items`. |
| **CR-024** | E | `supply_items` carries no link to the originating `supply_request_items` row. Line-level traceability request → dispatch → receipt is therefore impossible. | `06` | **Resolved:** `supply_items.supply_request_item_id` added (nullable, to permit an unsolicited dispatch). See `docs/29`. |
| **CR-025** | E | `discrepancies` records only a header-level `reference_type`/`reference_id` plus `item_id`. The brief mandates **line-level** discrepancy. | `06` | **Resolved:** `discrepancies.reference_line_id` added. See `docs/29`. |
| **CR-026** | E | `supply_requests.warehouse_id` is `NOT NULL`, but it is unspecified whether the Restaurant Supervisor chooses the warehouse or the server assigns it. | `04`, `07`, `06`, `09`, `29` | **RESOLVED (ADR-028).** Each restaurant has exactly one `default_serving_warehouse_id` (`NOT NULL`). The supervisor never selects a warehouse; the backend derives it. The creation DTO does not declare `warehouseId`, so there is no client value to trust. Derived warehouse must be same-company (structural) and `Active`, else `409 SERVING_WAREHOUSE_UNAVAILABLE` with **no fallback**. OD-008 closed. |
| **CR-027** | C | `Complete Rejection` (received `0`) produces no ledger movement. Apparent conflict with "confirmation deducts stock". | `04 §11` | Not a conflict. Dispatch never deducted, so zero receipt correctly leaves the balance untouched. Goods return physically to the warehouse where they already are, in the ledger's view. Clarified in `04 §11`. |

### 5.4 Inventory, Costing & Concurrency

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-030** | E | **Dispatched-but-unconfirmed goods are invisible.** Because dispatch does not deduct, a warehouse physical count performed while a truck is in transit will find a shortfall equal to the goods on that truck, and the count approval would post a spurious `PHYSICAL_ADJUSTMENT`. This is the principal architectural consequence of ADR-004 and was entirely unspecified. | `04`, `05`, `29` | **Resolved (ADR-018):** a **derived, read-only** figure `قيد النقل` (in-transit) is computed on demand from `supplies` in state `Dispatched`. It is **never** persisted as a balance and is **never** attributed to a restaurant, so the no-restaurant-inventory invariant holds. Stock-count screens display it and the count workflow subtracts it from the expected physical figure. |
| **CR-031** | E | Dispatch performs no stock check, so a warehouse can dispatch more than it holds; the failure surfaces later at the restaurant as `INSUFFICIENT_STOCK`, stranding the supervisor with goods in hand and no way to confirm. | `04`, `05` | **Resolved (ADR-019):** dispatch performs a **non-blocking sufficiency advisory** against `available = quantity − inTransit` and warns the dispatcher; it never reserves. If confirmation later fails on stock, the supply enters `Confirmed` only after an `Owner`/`Admin` posts a corrective `PHYSICAL_ADJUSTMENT`. Failure path documented in `docs/30`. |
| **CR-032** | E | What happens to `average_unit_cost` on an **outgoing** movement (`RESTAURANT_RECEIPT_CONFIRMED`) or on a negative `PHYSICAL_ADJUSTMENT` was never stated. | `04`, `15` | **Resolved (ADR-020):** under Weighted Average Costing, issues and negative adjustments are valued **at the current WAC** and leave the WAC **unchanged**. Only `OPENING_BALANCE`, `INCOMING_POSTED`, and `INCOMING_RECONCILIATION` may alter it. |
| **CR-033** | E | Negative `INCOMING_RECONCILIATION`: is the removed value priced at the receipt's unit cost or at the current WAC? | `04`, `15` | **Resolved (ADR-020):** reconciliation reverses the **same receipt line's** `unit_cost`, because it corrects that specific posting. Any other choice leaks cost between receipts. |
| **CR-034** | E | `CHECK (quantity >= 0)` can be violated by a legitimate negative `INCOMING_RECONCILIATION`, a receiving `reverse`, or a negative `PHYSICAL_ADJUSTMENT` when the stock has since been issued. | `04`, `06` | **Resolved (ADR-021):** the non-negative constraint is absolute. Such an operation fails with `INSUFFICIENT_STOCK` and must be resolved by an approved stock count. Negative stock is never permitted, not even transiently. |
| **CR-035** | D | `stock_balances.version BYTEA NOT NULL` is not a functioning concurrency token on PostgreSQL. Npgsql/EF Core implement optimistic concurrency via the `xmin` system column. A `BYTEA` column would need manual maintenance and would silently fail to detect lost updates. | `06`, `04` | **Resolved (ADR-022):** drop the `version` column; map the `xmin` system column with `.IsRowVersion()`. See `docs/29`. |
| **CR-036** | E | `stock_ledger` has no guard preventing the same business document from being posted twice (a retry outside the idempotency window, or a bug). | `06` | **Resolved:** `UNIQUE (company_id, reference_type, reference_id, item_id, movement_type)`. See `docs/29`. |
| **CR-037** | E | An item's `base_unit_id`, or a conversion factor, could be edited after transactions exist — retroactively corrupting every normalized historical quantity. | `06`, `04` | **Resolved (ADR-023):** `items.base_unit_id` is **immutable** once any ledger row exists for the item. `item_unit_conversions` rows are **immutable once used**; a change is a new row and the old is deactivated. |
| **CR-038** | E | Nothing constrains `item_unit_conversions.to_base_unit_id` to equal `items.base_unit_id`. | `06` | **Resolved:** server-side validation plus a composite FK. See `docs/29`. |
| **CR-039** | E | `stock_balances.base_unit_id` denormalizes `items.base_unit_id`, with no rule keeping them consistent. | `06` | **Resolved:** redundant under ADR-023 (base unit immutable); retained for query convenience with a documented invariant that it always equals `items.base_unit_id`. |
| **CR-040** | E | `receiving_orders` lists a `Reversed` status but has no `reversed_by`, `reversed_at`, or `reversal_reason` columns, and no rule about reversing an already-`Verified` order. | `06`, `05` | **Resolved:** columns added; reversal permitted from `Submitted` **or** `Verified`, never from `Reversed`. See `docs/29`. |
| **CR-041** | E | No rule prevents a second `verify` on an already-verified receiving order (which would double-post reconciliation). | `05`, `04` | **Resolved:** state machine forbids it; `INVALID_STATE_TRANSITION`. Reinforced by CR-036's uniqueness guard. |

### 5.5 Identifiers & Sequences

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-050** | E | `04 §5` mandates a `tenant_sequences` counter table. **The table does not exist in `06-database-schema.md`.** | `06` | **Resolved:** `document_sequences` specified in `docs/28`. |
| **CR-051** | D | `04 §5` mandates a `DSC-{YYYYMM}-{0000}` discrepancy number, but the `discrepancies` table has **no `document_number` column**. | `06`, `04` | **Resolved:** column added. See `docs/28`, `docs/29`. |
| **CR-052** | E | Monthly sequence reset (`REC-YYYYMM-0000`) is unspecified as to timezone. `Africa/Cairo` and UTC disagree for three hours every day, which would produce out-of-order month boundaries. | `04`, `28` | **Resolved (ADR-024):** sequence periods are derived from the document's **business date in the tenant's configured timezone**, defaulting to `Africa/Cairo`. Storage remains UTC. |
| **CR-053** | E | Sequence behaviour on transaction rollback is unspecified: a PostgreSQL `SEQUENCE` does not roll back and would leave gaps; a counter row does roll back but serializes writers. | `04`, `28` | **Resolved (ADR-025):** counter-table allocation inside the business transaction. Gap-free numbering is a business requirement for audit; the serialization cost is per `(company, type, period)` and acceptable at this scale. |
| **CR-054** | C | `OD-004` allows a future barcode use for item codes, while ADR-007 removes `barcode` entirely. | `open-decisions`, ADR-007 | Not a conflict: `OD-004` describes the *format's* future suitability, not a `barcode` column. Wording tightened. |

### 5.6 Authentication & Security

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-060** | D | OTP rate limit stated twice, incompatibly: `08 §2.4` "3 per mobile number per **hour**"; `08 §5` table "3 per IP / Mobile per **15 minutes**". | `08` | **Resolved (ADR-026):** both limits apply simultaneously — **3 per mobile per 15 minutes AND 5 per mobile per hour**. The stricter window governs. |
| **CR-061** | E | `08 §2` requires OTPs stored hashed "in cache/database", but **no OTP table exists** in `06`. | `06`, `08` | **Resolved:** `password_reset_otps` specified in `docs/29`. |
| **CR-062** | E | `08 §1` diagram references "Reset Failed Counter", but `users` has no `access_failed_count` / `lockout_end_at`. | `06`, `08` | **Resolved:** columns added. See `docs/29`. |
| **CR-063** | E | `08 §4` and `10 §2` require `GET /api/v1/auth/csrf-token`; it is absent from the `07` endpoint catalog. | `07` | **Resolved:** added to `07` §2.1 in the API reconciliation. |
| **CR-064** | E | `19` polls a `/health` endpoint that `07` never defines. | `07`, `19` | **Resolved:** `GET /health` (liveness) and `GET /health/ready` (DB readiness) defined; both unauthenticated, both leaking no version or schema detail. |
| **CR-065** | E | Every foreign key is single-column, so nothing prevents a `supply_request` in Company A referencing a `restaurant` in Company B. EF global query filters mitigate but do not *enforce* this at the database. | `06`, `09` | **Resolved (ADR-016):** composite `(company_id, id)` foreign keys on all tenant-scoped relationships. See `docs/29`. |
| **CR-066** | E | `stored_files` has no link to the business entity it evidences, so an uploaded delivery note cannot be attached to a receiving order. | `06`, `16` | **Resolved:** `file_attachments` join table specified in `docs/29`. |
| **CR-067** | E | `ON DELETE CASCADE` on `receiving_order_items`, `supply_request_items`, `supply_items`, `stock_count_items`, `item_unit_conversions` would silently destroy audit-relevant lines if a parent were ever deleted. | `06` | **Resolved:** business documents are never deleted. Cascades narrowed to `RESTRICT` for posted documents; `CASCADE` retained only for `Draft` parents. |
| **CR-068** | E | `06` contains **no index definitions at all**, while `21` mandates keyset pagination "over indexed columns" and P95 budgets of 150–300 ms. | `06`, `21` | **Resolved:** full index specification in `docs/29`. |
| **CR-069** | E | `06` states `audit_logs` and `stock_ledger` "do not support update/delete" but specifies no enforcement mechanism. | `06`, `14` | **Resolved:** PostgreSQL `RULE`/trigger-based rejection plus revoked `UPDATE`/`DELETE` grants for the application role. See `docs/29`. |
| **CR-070** | E | `21` requires 7-year retention in "immutable PostgreSQL partitions"; `06` declares `audit_logs` as an ordinary table with no partition key. | `06`, `21` | **Resolved:** `audit_logs` declared `PARTITION BY RANGE (created_at)`, monthly. See `docs/29`. |
| **CR-071** | E | `04 §16.3` says state-mutating requests *accept* `X-Idempotency-Key`; `07` shows it only on `confirm`. Which endpoints **require** it is undefined, as is the behaviour when the same key arrives with a different payload. | `07`, `04` | **Resolved:** required / optional matrix and `IDEMPOTENCY_KEY_REUSE` (409) semantics in `docs/30`. |
| **CR-072** | E | `07` includes a developer-oriented `messageEn` in every error, while the product is Arabic-only user-facing. | `07`, `13` | **Resolved:** `messageEn` is a developer diagnostic, never rendered. It is omitted entirely in the `Production` environment. See `docs/31`. |

### 5.7 Frontend, Testing & Naming

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-080** | E | `18` defines four .NET test projects plus Playwright, but **no frontend unit/component test project**, though the brief mandates frontend tests. | `18` | **Resolved:** `frontend/__tests__` with Vitest + React Testing Library added to the testing model. |
| **CR-081** | E | `warehouses.name`, `restaurants.name`, `suppliers.name` are plain `name`, while `categories`, `units`, `items` use `name_arabic`. Arabic-only content is not enforced or even signalled. | `06`, `31` | **Resolved:** naming unified to `name_arabic`; script-validation rules in `docs/31`. |
| **CR-082** | E | The brief mandates coverage of Arabic dates, phone numbers, pagination, breadcrumbs, sorting, filtering, print/export, and directional icons. `10`/`11`/`frontend-ui-ux-implementation-guide` cover only part. | `10`, `11`, UI/UX guide | **Resolved:** `docs/31-arabic-rtl-localization-spec.md` created. |
| **CR-083** | E | `26-traceability-matrix.md` has no rows for multi-item request structure, document-number generation, idempotency, file storage, reporting, Arabic/RTL, or line-level discrepancy. | `26` | **Resolved:** matrix extended to `REQ-32`. |
| **CR-084** | C | Version targets `.NET 10`, `EF Core 10`, `Next.js 16`, `React 19` are forward-looking. | `06`, `10`, `19`, `33` | **RESOLVED (ADR-027).** Toolchain verified on `koshary`: .NET SDK 10.0.302, runtimes 10.0.10, EF Core 10.0.11, Npgsql 10.0.3, `dotnet-ef` 10.0.3 all present; Node present (22.x inferred). **PostgreSQL is NOT installed** — Phase 2 blocked until it is. No downgrade; SQL Server / MySQL / Oracle explicitly rejected. Matrix: `docs/33`. OD-009 closed. |
| **CR-085** | B | The external ChatGPT transcript supplied in the brief could not be retrieved (client-rendered page; no server content). | Context | **RESOLVED (ADR-030).** Treated as an unavailable historical source: no requirement inferred from it, none invented. If supplied later as text or a file it becomes new source material and triggers a fresh reconciliation pass. OD-010 closed. |

---

## 6. Edge Cases Registered For Implementation

1. Receiving reversal after the received goods have already been issued to a restaurant → `INSUFFICIENT_STOCK`; resolve by stock count (CR-034).
2. Confirmation of a supply whose warehouse balance has since been depleted (CR-031).
3. Physical count opened while shipments are in transit (CR-030).
4. Same item added twice to one supply request (CR-023).
5. Idempotency key replayed with a different payload (CR-071).
6. Month boundary crossing between UTC and `Africa/Cairo` during sequence allocation (CR-052).
7. A user's role changed mid-session → security stamp rotation invalidates the cookie (`08 §3`).
8. Item deactivated while it appears on an open supply request → the request remains valid; new lines are rejected.

## 7. Security Implications

CR-013, CR-016, CR-035, CR-036, CR-065, CR-067, CR-069, and CR-070 are **security-relevant**. None may be deferred past their assigned phase in `docs/09-implementation-plan.md`.

## 8. Dependencies

`docs/decision-log.md` (ADR-012 … ADR-030), `docs/open-decisions.md` (OD-007 … OD-011), `docs/28`, `docs/29`, `docs/30`, `docs/31`, `docs/33`.

## 9. Acceptance Criteria

- Every register row is Class C, D, or E with a stated resolution, or is escalated to `open-decisions.md`.
- No row is marked resolved without a corresponding ADR or specification section.
- No business rule listed as FINAL in the re-initialisation brief is contradicted by any resolution here.

## 10. Related Documents

`docs/README.md`, `docs/decision-log.md`, `docs/open-decisions.md`, `docs/04-end-to-end-business-flow.md`, `docs/06-database-schema.md`, `docs/28`, `docs/29`, `docs/30`, `docs/31`, `docs/09-implementation-plan.md`.

---

## 11. Closure Amendment — Decisions Resolved

| Register ID | Was | Now | Authority |
| :--- | :--- | :--- | :--- |
| **CR-026** | REQUIRES DECISION (OD-008) | **RESOLVED** — server-derived single serving warehouse | ADR-028 |
| **CR-084** | Delivery risk (OD-009) | **RESOLVED** — toolchain verified; PostgreSQL missing and reported, not worked around | ADR-027, `docs/33` |
| **CR-085** | Recorded (OD-010) | **RESOLVED** — unavailable historical source, closed | ADR-030 |

### 11.1 New Findings From the Closure Pass

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-090** | E | `docs/29 §4.2` deliberately withheld the serving-warehouse mapping pending OD-008, leaving the schema unable to express the relationship. | `29`, `06` | **Resolved (ADR-028):** `restaurants.default_serving_warehouse_id UUID NOT NULL` with a composite tenant FK, plus its supporting index. Declared in `docs/29 §4.1` DB-17. **No `restaurant_serving_warehouses` join table is created** — v1.0 is one-to-one. |
| **CR-091** | D | With `warehouseId` removed from the request DTO (ADR-028), `docs/07 §2.5`'s documented request body — which listed `warehouseId` — became actively wrong and would have been implemented as a trusted client field. | `07` | **Resolved:** body corrected to `{ restaurantId, items[] }`; `warehouseId` explicitly listed among the server-generated fields. |
| **CR-092** | E | ADR-028 introduces a new failure mode — a restaurant with no active serving warehouse — that had no error code. | `13` | **Resolved:** `SERVING_WAREHOUSE_UNAVAILABLE` (409) added to the `docs/13` Arabic error catalog. |
| **CR-093** | E | PostgreSQL is not installed, yet `docs/19 §2.1` assumes `run-local.bat` can start a portable instance from `.local_postgres`, which does not exist. | `19`, `33` | **Resolved:** `docs/19` records the prerequisite explicitly; `run-local.bat` must **fail loudly** with an Arabic-free operator message when PostgreSQL is absent, never silently continue or substitute another engine. |
| **CR-094** | C | `docs/12 §1.1` lists `التقارير المالية والمخزنية` in Owner navigation, while ADR-029 defers reports. | `12` | **Resolved:** the entry is retained as a **future** item and is **not rendered** until the reports phase ships. An unimplemented route is never shown as an empty or placeholder screen (`docs/12 §2.3`). |
| **CR-095** | C | `docs/16` and `docs/17` remain fully specified but are now out of MVP scope. | `16`, `17` | **Resolved:** both marked **DEFERRED — NOT IN MVP SCOPE** at the top and preserved intact. Deferral is not deletion; the later phases start from a written baseline. |

---

## 12. Phase 1 Implementation Findings

> Discovered while authoring Phase 1. Recorded and resolved **before** any project file was created, per the documentation-first change-control rule.

| ID | Class | Finding | Affected | Resolution |
| :--- | :---: | :--- | :--- | :--- |
| **CR-096** | D | **The E2E suite has two contradictory homes.** `docs/18 §1` lists `tests/Inventory.E2E/` — a **.NET** Playwright project. `docs/09 §8` lists the verification command `npm run test:e2e --prefix frontend` — a **Node** Playwright project. The same suite cannot live in both, and building both would produce two half-maintained E2E suites. | `18`, `09`, `26` | **Resolved (ADR-031):** E2E lives in the frontend as `@playwright/test` at `frontend/e2e/`. `tests/Inventory.E2E/` is removed from the test-project list. |
| **CR-097** | E | **FluentAssertions licensing was never considered.** `docs/18 §1` mandates FluentAssertions. From **v8 it is commercially licensed** — free only for open-source and non-commercial use. This is a commercial enterprise product, so v8+ would require paid per-developer licences. The version cached on the development machine is **8.10.0**. | `18` | **Escalated — OD-012.** Not a decision for an implementation agent to take on the owner's behalf. Phase 1 uses **xUnit's built-in assertions only** (architecture tests need nothing more), so no licence is consumed and nothing is pre-committed. The choice is due before Phase 7, which writes the first domain tests. |
| **CR-098** | C | `docs/09` task 1.14 says "Configure Swagger for Development only", but in Phase 1 the API has no business endpoints — a Swagger **UI** would render an empty document. | `09` | Clarified: Phase 1 configures **OpenAPI document generation** (`Microsoft.AspNetCore.OpenApi`, Development-only, served at `/openapi/v1.json`). The interactive UI is added in Phase 3 with the first real endpoints. Documented in the Phase 1 task list, not silently skipped. |
| **CR-099** | C | `docs/09` task 1.13 requires `GET /health/ready` to report **database** readiness, but no `DbContext` exists until Phase 2. | `09` | Clarified: the readiness probe opens a raw **Npgsql** connection using the configured connection string. It needs no `DbContext`, no entity, and no migration, so it is **not** Phase 2 work pulled forward. It correctly reports `Unhealthy` until PostgreSQL is installed — which is the truthful answer today. |
