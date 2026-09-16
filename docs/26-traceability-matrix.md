# 26 — Master Traceability Matrix

> **Document ID:** SPEC-26  
> **Topic:** End-to-End Requirement Traceability from Master Specification to Code, Tests, and E2E

---

## 1. Traceability Matrix Table

| Req ID | Requirement Description | Primary Spec Doc | DB Entity / Table | Backend Service / API | Frontend Feature Area | Security / Scope Guard | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **REQ-01** | Warehouse-Only Inventory Ownership | `docs/02`, `04` | `stock_balances`, `stock_ledger` | `StockPostingService` | Warehouse Inventory Views | Global Tenant + WH Scope | `TEST-INV-001` |
| **REQ-02** | No Restaurant Stock Balances | `docs/02`, `06` | *(Table Prohibited)* | *(API Prohibited)* | *(UI Prohibited)* | Architecture Guard | `TEST-ARCH-001` |
| **REQ-03** | Supplier Receiving Stock Addition | `docs/02`, `05` | `receiving_orders` | `POST /api/v1/receiving-orders/{id}/submit` | Receiving Management | `receiving:submit` + WH Scope | `TEST-REC-001` |
| **REQ-04** | Receiving Physical Reconciliation | `docs/02`, `05` | `receiving_order_items`, `discrepancies` | `POST /api/v1/receiving-orders/{id}/verify` | Receiving Verification Modal | `receiving:verify` + WH Scope | `TEST-REC-002` |
| **REQ-05** | Supply Dispatch (Zero Stock Effect) | `docs/02`, `05` | `supplies` | `POST /api/v1/supplies/{id}/dispatch` | Supply Dispatch UI | `supplies:dispatch` + WH Scope | `TEST-SUP-001` |
| **REQ-06** | Receipt Confirmation Stock Deduction| `docs/02`, `05` | `supplies`, `stock_ledger` | `POST /api/v1/supplies/{id}/confirm` | Receipt Confirmation Form | `supplies:confirm` + Rest Scope| `TEST-SUP-002` |
| **REQ-07** | Restaurant Kitchen Consumption Log | `docs/02`, `05` | `consumption_records` | `POST /api/v1/consumption` | Daily Consumption Log | Rest Scope | `TEST-CON-001` |
| **REQ-08** | Physical Warehouse Stock Count | `docs/02`, `05` | `stock_counts` | `POST /api/v1/stock-counts/{id}/approve` | Stock Count Workflow | `stock_counts:approve` | `TEST-CNT-001` |
| **REQ-09** | Server Item Code Auto-Generation | `docs/02`, `06` | `items.generated_code` | `ItemCodeGenerator` | Item Create Form (Read-only) | Concurrency Lock | `TEST-ITM-001` |
| **REQ-10** | Item-Specific Unit Conversions | `docs/02`, `06` | `item_unit_conversions` | `UnitConversionResolver` | Item Unit Form | Server Validation | `TEST-UNT-001` |
| **REQ-11** | Weighted Average Costing (WAC) | `docs/04`, `15` | `stock_balances.average_unit_cost` | `CostingEngine` | Inventory Value Cards | Owner Role ONLY | `TEST-CST-001` |
| **REQ-12** | Admin Financial & Cost Lockdown | `docs/03`, `15` | *(Projection Masking)* | DTO Projection Filter | Admin Views (Masked) | Server Role Denial | `TEST-SEC-001` |
| **REQ-13** | Admin Audit Log Lockdown | `docs/03`, `14` | `audit_logs` | `GET /api/v1/audit` | Admin Nav (Hidden) | Server Role Denial (403) | `TEST-SEC-002` |
| **REQ-14** | Mobile + Password Authentication | `docs/08` | `users` | `POST /api/v1/auth/login` | Login Form | HttpOnly Cookies + CSRF | `TEST-AUT-001` |
| **REQ-15** | Rate-Limited OTP Password Reset | `docs/08` | `users` / Cache | `POST /api/v1/auth/forgot-password/*` | Password Reset Flow | Rate Limiting Middleware | `TEST-AUT-002` |
| **REQ-16** | Warehouse Logistics Navigation | `docs/12` | *(Metadata)* | `/api/v1/account/me` | Warehouse Staff Shell | Scope-Filtered Menus | `TEST-NAV-001` |
| **REQ-17** | Restaurant Supervisor Navigation | `docs/12` | *(Metadata)* | `/api/v1/account/me` | Restaurant Supervisor Shell | Scope-Filtered Menus | `TEST-NAV-002` |
| **REQ-18** | Quick-Add Master Data Modals | `docs/11` | `categories`, `units` | `POST /api/v1/categories` | QuickAddModal Component | In-Form Master Data Entry | `TEST-E2E-001` |
| **REQ-19** | Zero Negative Stock Concurrency | `docs/04`, `18` | `stock_balances` | PostgreSQL Atomic Update | Receipt Confirmation | Row Lock / DB Check Constraint| `TEST-CONC-001` |
| **REQ-20** | Transactional Audit Logging | `docs/14` | `audit_logs` | EF Core DbContext Interceptor | Owner Audit Log Viewer | Owner Role ONLY | `TEST-AUD-001` |

---

## 2. Extension — Requirements Added by the Reconciliation Pass

> Added to close CR-083. Numbering continues from REQ-20.

| Req ID | Requirement Description | Primary Spec Doc | DB Entity / Table | Backend Service / API | Frontend Feature Area | Security / Scope Guard | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **REQ-21** | Multi-item supply request (header + `1..N` lines) | `docs/04 §8` | `supply_requests`, `supply_request_items` | `POST /api/v1/supply-requests` | Multi-item request editor | `supply_requests:create` + Restaurant Scope | `TEST-REQ-MULTI` |
| **REQ-22** | Draft-only line add / edit / remove | `docs/04 §8.1` | `supply_request_items` | `POST\|PUT\|DELETE /supply-requests/{id}/items` | Line editor | Draft state guard + Restaurant Scope | `TEST-REQ-LINEEDIT` |
| **REQ-23** | One line per item per document | `docs/32` CR-023 | `uq_sri_item` and siblings | Application validation | Duplicate-merge UX | DB unique constraint | `TEST-REQ-DUPITEM` |
| **REQ-24** | Line-level traceability request → dispatch → receipt | `docs/04 §25` | `supply_items.supply_request_item_id` | `SupplyFulfilmentService` | Document drill-through | — | `TEST-TRACE-LINE` |
| **REQ-25** | Line-level discrepancy attribution | `docs/04 §25` | `discrepancies.reference_line_id` | `DiscrepancyService` | Discrepancy detail | Scoped visibility | `TEST-DSC-LINE` |
| **REQ-26** | Gap-free server-generated document numbers | `docs/28` | `document_sequences` | `IDocumentSequenceService` | Read-only code field | Transactional counter (ADR-025) | `TEST-SEQ-GAPFREE` |
| **REQ-27** | Fulfilment and dispatch as distinct acts | `docs/04 §9` (ADR-017) | `supplies.status`, `prepared_by` | `/fulfill`, `/dispatch` | Fulfilment queue, dispatch screen | `supply_requests:fulfill`, `supplies:dispatch` + WH Scope | `TEST-SUP-PREPARED` |
| **REQ-28** | In-transit derived, never stored | `docs/04 §21` (ADR-018) | *(no table — derived)* | `IInTransitCalculator` | Balance / in-transit / available | Warehouse-attributed only | `TEST-INV-INTRANSIT` |
| **REQ-29** | Idempotent critical mutations | `docs/30 §6` | `idempotency_records` | Idempotency middleware | Key stable per form instance | Scoped to `(company, user)` | `TEST-IDEM-001` |
| **REQ-30** | Deterministic lock ordering, no deadlock | `docs/30 §5.3` | `stock_balances` | `IStockPostingService` | — | — | `TEST-CONC-DEADLOCK` |
| **REQ-31** | WAC changes only on inbound movements | `docs/04 §22` (ADR-020) | `stock_balances.average_unit_cost` | `ICostingEngine` | Valuation cards (Owner) | Owner only | `TEST-CST-ISSUE` |
| **REQ-32** | Absolute non-negative stock | `docs/04 §23` (ADR-021) | `CHECK (quantity >= 0)` | Conditional atomic `UPDATE` | Arabic `INSUFFICIENT_STOCK` | DB constraint + predicate | `TEST-INV-NONNEG` |
| **REQ-33** | Structural tenant isolation | `docs/29` DB-02/03 (ADR-016) | Composite `(company_id, id)` FKs | EF configuration | — | Database-enforced | `TEST-ARCH-TENANTFK` |
| **REQ-34** | Exactly one product role per user | `docs/29` DB-05 (ADR-014) | `uq_user_single_role` | `UserService` | Single-select role field | DB constraint | `TEST-SEC-SINGLEROLE` |
| **REQ-35** | Non-grantable financial and audit permissions | ADR-012 | `permissions.is_grantable` | Authorization handler | Codes never listed in the UI | Role denial | `TEST-SEC-NONGRANT` |
| **REQ-36** | Append-only ledger and audit log | `docs/29 §4.4` | Triggers + revoked grants | — | — | Database-enforced | `TEST-ARCH-APPENDONLY` |
| **REQ-37** | Base units and used conversions immutable | `docs/04 §24` (ADR-023) | `items.base_unit_id` | `ItemService` | Disabled field with explanation | Server validation | `TEST-UNT-IMMUTABLE` |
| **REQ-38** | Blind counting enforced server-side | `docs/04 §13` | `stock_counts.is_blind_count` | `StockCountService` | Counter UI | Payload suppression | `TEST-CNT-BLIND` |
| **REQ-39** | Stock count correct during transit | `docs/04 §21.3` | *(derived)* | `StockCountService` | Count screen | — | `TEST-CNT-INTRANSIT` |
| **REQ-40** | Arabic-only user-facing content | `docs/31 §4.1` | `name_arabic`, `name_normalized` | `messageAr` catalog | Every screen | `messageEn` suppressed in Production | `TEST-I18N-ARABIC` |
| **REQ-41** | Native RTL layout and bidi isolation | `docs/31 §4.3–4.4` | — | — | Global shell, all components | ESLint direction rule | `TEST-I18N-RTL` |
| **REQ-42** | Arabic normalization for search and uniqueness | `docs/31 §4.2` | `items.name_normalized`, `pg_trgm` | `INormalizationService` | Search box | — | `TEST-I18N-SEARCH` |
| **REQ-43** | Arabic collation for sorting | `docs/31 §4.8` | `COLLATE "ar-x-icu"` | Query layer | Table headers | — | `TEST-I18N-SORT` |
| **REQ-44** | Arabic-correct print and export | `docs/31 §4.9` | — | PDF / CSV generators | Export buttons | Role projection applied | `TEST-EXPORT-ARABIC` |
| **REQ-45** | File evidence linked to its document — 🚫 **DEFERRED (ADR-029)** | `docs/16`, `docs/29 §4.2` | `stored_files`, `file_attachments` *(specified, not migrated)* | `IFileStorageService` | *(no attachment UI)* | Tenant + document scope | `TEST-FILE-SCOPE` *(future)* |
| **REQ-46** | Scoped reporting with financial gating — 🚫 **DEFERRED (ADR-029)** | `docs/17` | *(query projections)* | Report services | *(route not registered)* | Owner-only valuation | `TEST-RPT-SCOPE` *(future)* |
| **REQ-47** | Full index coverage for stated latency budgets | `docs/29 §5` | All indexes | — | — | — | `TEST-DB-INDEXES` |
| **REQ-48** | Zero fake data on any surface | `docs/12 §2.3` | — | — | All dashboards and widgets | — | `TEST-E2E-NOFAKE` |
| **REQ-49** | Rate-limited OTP with both windows | ADR-026 | `password_reset_otps` | Rate-limiting middleware | Reset flow | 3/15 min **and** 5/hour | `TEST-AUT-OTPRATE` |
| **REQ-50** | Impossible-confirmation resolution path | `docs/30 §7.1` | `discrepancies` | Confirmation handler | Arabic explanation + escalate action | No auto-adjustment | `TEST-SUP-IMPOSSIBLE` |

## 3. Traceability Rules

1. Every requirement has at least one verification test ID. A requirement with no test is not implemented.
2. Every test ID must exist as a real, named test by Phase T1.
3. Adding a requirement means adding a row here in the same change — never afterwards.
4. `docs/09-implementation-plan.md` §9 maps each requirement to the phase that delivers it.

---

## 4. Decision-Closure Extension (REQ-51 … REQ-55)

> Added when OD-008 … OD-011 were closed. Numbering continues from REQ-50.

| Req ID | Requirement Description | Primary Spec Doc | DB Entity / Table | Backend Service / API | Frontend Feature Area | Security / Scope Guard | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **REQ-51** | Each restaurant has exactly one default serving warehouse | `docs/04 §8.2` (ADR-028) | `restaurants.default_serving_warehouse_id` (`NOT NULL`) | `RestaurantService` | Restaurant form (`Owner`/`Admin`) | `restaurants:manage`; composite tenant FK | `TEST-SW-CONFIG` |
| **REQ-52** | Supply-request warehouse is derived server-side, never client-supplied | `docs/04 §8.2`, `docs/09 §2.3` | `supply_requests.warehouse_id` | `CreateSupplyRequestHandler` | **No warehouse selector**; derived value shown read-only | DTO declares no `warehouseId`; over-post discarded by the model binder | `TEST-SW-DERIVED` |
| **REQ-53** | No active serving warehouse fails loudly with no fallback | `docs/04 §8.2` SW-5, `docs/13` | — | `409 SERVING_WAREHOUSE_UNAVAILABLE` | Arabic error banner | — | `TEST-SW-UNAVAILABLE` |
| **REQ-54** | Core data remains reportable while reports are deferred | `docs/17 §4` (ADR-029) | `stock_ledger`, `audit_logs`, line links | `IStockPostingService`, audit interceptor | — | `IFinancialProjection` ready for reuse | `TEST-RPT-READY` |
| **REQ-55** | Verified environment matrix; no silent architecture downgrade | `docs/33` (ADR-027) | — | — | — | PostgreSQL 16+ mandatory; no substitute engine | `TEST-ENV-MATRIX` |

### 4.1 Notes

- **REQ-52** is verified by asserting on the **persisted** `warehouse_id` after an over-posted request, not on the response body. A handler that read and then discarded a client value would pass a response-only assertion.
- **REQ-54** has no endpoint because no report is built. Its test asserts that ledger rows carry `unit_cost`, `actor_user_id`, `reference_type`/`reference_id`, and distinct `occurred_at`/`created_at` on **every** movement type — the metadata a future report cannot reconstruct if it was never captured.
- **REQ-45** and **REQ-46** stay in the matrix as deferred rows rather than being deleted, so the future phases start from a written baseline.
