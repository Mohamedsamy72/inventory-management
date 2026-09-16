# 17 — Reporting & Analytics Engine

> 🚫 **DEFERRED AS IMPLEMENTATION — REPORTABILITY PRESERVED (ADR-029).**
> No report endpoint, no export pipeline, no `/reports` route, and — emphatically — **no placeholder charts, no fake KPIs, no mock report screens** are built in the MVP. An unimplemented report is shown as nothing at all, never as an empty or sample surface (`docs/12 §2.3`).
>
> What **is** required now is that the core data remain reportable. See **§4 Reporting Extension Points**, which is binding on the core phases.
>
> The report catalog in §2 remains the authoritative future specification.

> **Document ID:** SPEC-17  
> **Topic:** Scoped Report Queries, Export Security, and Financial Guardrails

---

## 1. Reporting Engine Architecture

The reporting engine executes bounded, index-optimized aggregation queries.

### Core Security Rules:
1. **Scope Enforcement:** Reports must strictly respect warehouse and restaurant scopes. An actor cannot execute a report covering unauthorized locations.
2. **Financial Gating:** Valuation and cost analytics are accessible **exclusively to the Owner role**.
3. **Bounded Time Windows:** Default date range filtering is enforced to prevent table scans over multi-year datasets.

---

## 2. Standard Operational & Financial Reports

| Report Name (AR) | Report Code | Target Audience | Data Points Covered |
| :--- | :--- | :--- | :--- |
| **حركة المخزون بالمستودع** | `RPT_STOCK_MOVEMENT` | Owner, Admin, Warehouse Staff | Ledger entries per item, movement types, date ranges, actor IDs. |
| **تقييم المخزون المادي** | `RPT_STOCK_VALUATION` | **Owner ONLY** | Physical quantity $\times$ WAC per item, warehouse total value. |
| **سجل الاستلامات الواردة** | `RPT_RECEIVING_SUMMARY` | Owner, Admin, Warehouse Staff | Supplier receipts, expected vs. actual quantities, posting dates. |
| **توريدات الفروع المنفذة** | `RPT_SUPPLY_FULFILLMENT`| Owner, Admin, Restaurant Sup. | Supply requests, dispatched quantities, confirmed quantities, discrepancies. |
| **استهلاك فروع المطاعم** | `RPT_BRANCH_CONSUMPTION` | Owner, Admin, Restaurant Sup. | Daily kitchen consumption trends, items used, operational yield. |
| **تقرير الفروقات المعلقة** | `RPT_DISCREPANCY_ANALYSIS`| Owner, Admin | Open delivery variances, receiving discrepancies, count variances. |

---

## 3. Printing & Export Security

- **Export Formats:** Arabic-first formatted PDF and CSV / Excel.
- **RTL PDF Rendering:** Built using server-side PDF generators supporting UTF-8 Arabic glyph shaping and bidirectional layout.
- **Audit Logging:** Every report generation and export action writes an entry to `audit_logs`.

---

## 4. Reporting Extension Points — Binding on the Core Phases (ADR-029)

Reports are deferred, but the ability to build them later is **not** deferred. Data that is not captured during the core phases cannot be recovered afterwards: an unrecorded actor or an unrecorded cost at posting time is gone permanently. The following obligations therefore apply to the core phases even though no report is built.

### 4.1 Stock Ledger Must Stay Fully Reportable

Every `stock_ledger` row carries, at posting time and for the life of the row:

| Field | Why a future report needs it |
| :--- | :--- |
| `company_id`, `warehouse_id`, `item_id` | Every aggregation dimension. |
| `movement_type` | Separates receipts, reconciliations, issues, and adjustments. |
| `quantity`, `base_quantity`, `unit_id` | Normalized quantities; the entered unit is preserved for operational reporting. |
| `reference_type`, `reference_id` | Traceback from any figure to its source document. |
| `unit_cost`, `total_cost` | Valuation. Populated for **every** movement type (ADR-020), even where the WAC is unchanged — a cost not recorded at posting time cannot be reconstructed later. |
| `actor_user_id` | Accountability reporting. |
| `occurred_at`, `created_at` | Business time and system time kept distinct, so a backdated document reports correctly. |

The ledger is append-only (`docs/29 §4.4`), so historical report figures are permanently reproducible. **Any change reducing what a ledger row records requires a superseding ADR.**

### 4.2 Audit Data Must Stay Fully Reportable
`audit_logs` retains actor, role, action, entity, Arabic description, `old_values`/`new_values` diffs, correlation ID, and location context, monthly-partitioned with 7-year retention (`docs/29 §4.5`). Compliance reporting is a query over existing data, never a new capture.

### 4.3 Documents Must Stay Line-Traceable
The chain of `docs/04 §25` — `supply_items.supply_request_item_id` and `discrepancies.reference_line_id` — is what makes fulfilment-rate and variance reporting possible at line granularity. These links are built in the **core** phases (`docs/29` DB-06, DB-07), not deferred with the reports.

### 4.4 Index Coverage Already Exists
The aggregation paths future reports will use are already indexed by `docs/29 §5`: `ix_ledger_wh_item_time`, `ix_ledger_type_time`, `ix_ledger_reference`, `ix_audit_tenant_time`, `ix_cons_rest_date`, `ix_disc_status`. No new index is expected when the reports phase begins.

### 4.5 Financial Gating Is Already Enforced
`IFinancialProjection` (Phase 4) is the single masking path. When reports are built they route through it unchanged, so `RPT_STOCK_VALUATION` remains Owner-only and `RPT_STOCK_MOVEMENT` omits cost columns entirely — **absent, not null** — for non-Owners.

### 4.6 What Is Explicitly Forbidden While Reports Are Deferred
- No `/reports` route, page, or navigation target that renders anything.
- No placeholder chart, sample dataset, mock KPI card, or "coming soon" screen carrying fabricated figures.
- No hardcoded `0`, estimated total, or illustrative trend line.
- No report endpoint returning synthetic data for frontend development.

A number on screen is always a number the server actually computed (`docs/12 §2.3`). A deferred report shows nothing at all.
