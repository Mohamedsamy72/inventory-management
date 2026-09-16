# 23 — Acceptance Criteria & Numerical Test Scenarios

> **Document ID:** SPEC-23  
> **Topic:** Deterministic Acceptance Criteria, Mathematical Test Cases, and Role Verifications

---

## 1. Master Numerical Inventory Verification Scenarios

All implementation agents and QA pipelines must validate these exact mathematical scenarios:

### Scenario 1: Standard Supplier Receiving Posting
- **Initial State:** Warehouse Stock = `500 KG`.
- **Action:** Post supplier receiving order for `100 KG @ 12.00 EGP`.
- **Expected Result:**
  - `StockBalance.Quantity` = `600 KG`.
  - `StockLedger` contains `INCOMING_POSTED` movement with `+100 KG`.

### Scenario 2: Post-Posting Receiving Reconciliation (Actual < Expected)
- **Initial State:** Warehouse Stock = `600 KG` (from Scenario 1).
- **Action:** Physical verification records `Actual = 80 KG` (Expected `100 KG`).
- **Expected Result:**
  - `StockBalance.Quantity` = `580 KG` (Adjusted by `-20 KG`).
  - `StockLedger` contains `INCOMING_RECONCILIATION` movement with `-20 KG`.
  - **CRITICAL:** Stock must NOT become `680 KG` or `660 KG`.

### Scenario 3: Supply Fulfillment & Dispatch (Zero Stock Effect)
- **Initial State:** Warehouse Stock = `580 KG`.
- **Action:** Restaurant requests `20 KG`. Warehouse fulfills and dispatches `20 KG`.
- **Expected Result:**
  - Warehouse Stock remains **EXACTLY 580 KG**.
  - Supply status transitions to `Dispatched`.
  - No stock ledger movement is written.

### Scenario 4: Full Restaurant Receipt Confirmation
- **Initial State:** Warehouse Stock = `580 KG`. Supply Dispatched = `20 KG`.
- **Action:** Restaurant Supervisor confirms receipt of `20 KG`.
- **Expected Result:**
  - Warehouse Stock decreases to **560 KG**.
  - `StockLedger` contains `RESTAURANT_RECEIPT_CONFIRMED` movement with `-20 KG`.

### Scenario 5: Partial Restaurant Receipt Confirmation with Discrepancy
- **Initial State:** Warehouse Stock = `580 KG`. Supply Dispatched = `20 KG`.
- **Action:** Restaurant Supervisor confirms receipt of `18 KG` (2 KG damaged).
- **Expected Result:**
  - Warehouse Stock decreases to **562 KG** (reduces by received 18 KG only).
  - `StockLedger` contains `RESTAURANT_RECEIPT_CONFIRMED` movement with `-18 KG`.
  - `Discrepancies` table logs a record with `Variance = -2 KG` and `Status = 'Open'`.

### Scenario 6: Concurrent Stock Contention & Negative Stock Guard
- **Initial State:** Warehouse Stock = `20 KG`.
- **Action:** Two supervisors simultaneously attempt to confirm supplies drawing `15 KG` each.
- **Expected Result:**
  - Exactly one confirmation succeeds (reducing stock to `5 KG`).
  - The second confirmation fails with HTTP 400 `INSUFFICIENT_STOCK`.
  - Warehouse Stock **NEVER becomes negative**.

---

## 2. Role Security Acceptance Criteria

| Test ID | Role Under Test | Action Attempted | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `SEC-001` | **Admin** | `GET /api/v1/items/{id}` | Returns HTTP 200 with `averageUnitCost: null`. |
| `SEC-002` | **Admin** | `GET /api/v1/audit` | Returns **HTTP 403 Forbidden**. |
| `SEC-003` | **Warehouse Staff** | `POST /api/v1/receiving-orders` for Unauthorized WH | Returns **HTTP 403 Forbidden**. |
| `SEC-004` | **Restaurant Supervisor**| `POST /api/v1/supplies/{id}/confirm` for Other Branch | Returns **HTTP 403 Forbidden**. |
| `SEC-005` | **Any Role** | Direct balance update `PATCH /api/v1/stock-balances` | Endpoint does NOT exist (**HTTP 404 / 405**). |
| `SEC-006` | **Any Role** | Direct stock ledger write `POST /api/v1/stock-ledger` | Endpoint does NOT exist (**HTTP 404 / 405**). |
| `SEC-007` | **Restaurant Supervisor** | `POST /api/v1/supply-requests` with an over-posted `"warehouseId"` naming another warehouse in the same company | Request is created against the restaurant's **configured serving warehouse**. Assert on the **persisted** `warehouse_id`, not the response body (ADR-028). |
| `SEC-008` | **Restaurant Supervisor** | `POST /api/v1/supply-requests` for a restaurant whose serving warehouse is deactivated | Returns **HTTP 409 `SERVING_WAREHOUSE_UNAVAILABLE`** and creates nothing. **No fallback warehouse is selected.** |
| `SEC-009` | **Any Role** | `GET /api/v1/reports/...` or any file-upload endpoint | Endpoint does NOT exist (**HTTP 404**) — deferred, and never stubbed (ADR-029). |
| `SEC-010` | **Owner/Admin** | Grant `costs:view` to a `User`, then read an item as that user | Grant rejected with **400 `NON_GRANTABLE_PERMISSION`**; costs remain `null` (ADR-012). |

---

## 3. Decision-Closure Scenarios

### Scenario 7: Serving Warehouse Derivation (ADR-028)
- **Setup:** Restaurant `فرع مدينة نصر` has `default_serving_warehouse_id` = `مخزن مدينة نصر الرئيسي` (`Active`). A second warehouse `مخزن العبور` exists in the same company.
- **Action:** The Restaurant Supervisor creates a supply request for their branch; the raw HTTP payload additionally carries `"warehouseId": "<مخزن العبور id>"`.
- **Expected Result:**
  - The persisted `warehouse_id` is **`مخزن مدينة نصر الرئيسي`**.
  - The over-posted value is discarded by the model binder; no handler ever reads it.
  - Staff scoped to `مخزن العبور` do **not** see the request; staff scoped to `مخزن مدينة نصر الرئيسي` do.

### Scenario 8: Serving Warehouse Unavailable (ADR-028 SW-5)
- **Setup:** The restaurant's serving warehouse is `status = 'Inactive'`.
- **Action:** The supervisor attempts to create a supply request.
- **Expected Result:** HTTP **409 `SERVING_WAREHOUSE_UNAVAILABLE`** (`"لا يوجد مستودع خدمة مفعّل لهذا الفرع، يرجى مراجعة إدارة النظام"`). **No row is created and no other warehouse is substituted.**

### Scenario 9: Serving Warehouse Resolves Once (ADR-028 SW-8)
- **Setup:** A request exists against warehouse A. An Admin then changes the restaurant's default to B.
- **Expected Result:** The existing request still targets **A**. Only later requests target B.

### Scenario 10: Core Data Stays Reportable While Reports Are Deferred (ADR-029)
- **Action:** Run the full Scenario 1–6 sequence.
- **Expected Result:** Every resulting `stock_ledger` row carries a non-null `unit_cost`, `actor_user_id`, `reference_type`, `reference_id`, and distinct `occurred_at` / `created_at` — **including** `RESTAURANT_RECEIPT_CONFIRMED` and `PHYSICAL_ADJUSTMENT` rows, where the WAC does not change. Metadata absent at posting time can never be reconstructed.
