# 24 — Clean Start & Data Migration Rules

> **Document ID:** SPEC-24  
> **Topic:** Clean Start Baseline, Permanent Schema Purge, and Legacy Data Mapping Rules

---

## 1. Clean Start Schema Baseline

Because this project is executing a **Clean Restart** from first principles, the initial EF Core migration (`00000000000000_InitialCreate.cs`) is designed exclusively from the current clean specifications.

### Prohibited Legacy Artifacts (Must NOT Exist in New Schema):
- ❌ **No `restaurant_stocks` or `restaurant_inventories` tables.**
- ❌ **No `barcode` columns in `items`.**
- ❌ **No `name_english` or `name_en` columns in `items`, `categories`, `units`.**
- ❌ **No `Accountant` role in seed data or role enums.**
- ❌ **No `reorder_points`, `max_stock`, or `waste_records` tables.**
- ❌ **No `restaurant_serving_warehouses` join table** — v1.0 is one serving warehouse per restaurant (ADR-028).
- ❌ **No `version BYTEA` column on `stock_balances`** — the concurrency token is `xmin` (ADR-022).
- ⏸️ **`stored_files` and `file_attachments` are specified but NOT migrated** — files are deferred (ADR-029).

---

## 2. Legacy Data Mapping Rules (If Historical Data Import is Required)

If legacy operational records are imported into the clean system:
1. **Role Mapping:**
   - Legacy `Accountant` users must be mapped to `User` with explicitly configured permissions by the `Owner`.
2. **Item Code Mapping:**
   - Legacy alphanumeric codes are preserved in `generated_code` if unique per company; otherwise, new sequential codes (`ITM-000001+`) are assigned.
3. **Opening Stock Balances:**
   - Imported solely for **Warehouses** via formal `OPENING_BALANCE` entries in `StockLedger`.
   - Any historical "restaurant stock balances" are converted into historical `ConsumptionRecord` logs or discarded.

---

## 3. Reconciliation Amendment

### 3.1 Serving Warehouse Is Mandatory on Import (ADR-028)
`restaurants.default_serving_warehouse_id` is `NOT NULL`. Every imported restaurant must be mapped to exactly one active warehouse **in the same company** before it can accept a supply request. An import that cannot determine a serving warehouse for a restaurant **fails that row** — it never defaults to the first warehouse found, because a silently mis-routed requisition is far harder to detect than a failed import.

### 3.2 Deferred Tables Are Not Created (ADR-029)
The initial migration creates **no** file-storage tables and **no** reporting artifacts. `stored_files` and `file_attachments` remain specified in `docs/29 §4.2` and are migrated only when Phase 15 activates.

### 3.3 The Initial Migration Follows `docs/29`, Not `docs/06` Alone
`docs/06` is the table inventory; `docs/29` corrects it. The clean baseline must include composite tenant foreign keys, `xmin` concurrency, the uniqueness and check constraints, append-only enforcement, audit partitioning, the four added tables, `restaurants.default_serving_warehouse_id`, and the full index plan. Generating the migration from `docs/06` alone would faithfully reproduce every defect the reconciliation found.
