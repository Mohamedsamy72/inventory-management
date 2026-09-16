# 05 — Business Workflows & State Machines

> ⚠️ **AUTHORITY NOTE (CR-002).** This document is a **subordinate summary**. The master business-flow authority is **`docs/04-end-to-end-business-flow.md`**. Where the two differ, `04` governs, and this document may never introduce a rule `04` does not state.
>
> **Superseded here by `04` and ADR-017 (CR-020, CR-021):** the supply lifecycle below omits the `Prepared` state. The canonical `Supply` state machine is `Prepared → Dispatched → Confirmed | ConfirmedWithDiscrepancy | RejectedAtDelivery`, with `Cancelled` reachable only from `Prepared`. Fulfilment creates `Prepared`; dispatch transitions to `Dispatched`. Neither touches stock.
>
> **Also superseded (CR-014):** §3 refers to a *"warehouse manager"*, which is not one of the five product roles. Stock counts are opened and approved by `Owner` or `Admin` (`stock_counts:create`, `stock_counts:approve`); Warehouse Staff record counts only (`stock_counts:count`).

> **Document ID:** SPEC-05  
> **Topic:** End-to-End Business Lifecycles, Controlled State Transitions, and Document Flows

---

## 1. Receiving Workflow (Supplier → Warehouse)

```
[ Draft ] ──► [ Submitted / Posted ] ──► [ Verified (Reconciled) ]
                       │
                       └──► [ Discrepancy Flagged ] ──► [ Resolved ]
```

### State Transitions & Lifecycle:
1. **Draft (`مسودة`):** Warehouse staff records supplier shipment details (supplier, warehouse, items, expected quantities, unit costs). No stock impact.
2. **Submitted / Posted (`مرحل / مستلم`):** 
   - Posting action creates `INCOMING_POSTED` movements in `StockLedger`.
   - `StockBalance` quantity and WAC are updated immediately.
   - Status changes to `Submitted`.
3. **Verified / Reconciled (`تم التحقق والمطابقة`):**
   - Physical count verification occurs against the posted document.
   - If actual matches expected, status moves to `Verified`.
   - If actual differs from expected, system writes an `INCOMING_RECONCILIATION` movement for the delta (`Actual - Expected`) and creates a `Discrepancy` record.
4. **Reversed (`معكوس`):** If an error occurred, an authorized user (Admin/Owner) can reverse the posting. Reversal generates opposite ledger entries and restores prior balances.

---

## 2. Restaurant Supply Lifecycle (Request → Dispatch → Receipt)

```
[ Restaurant Supervisor ]                 [ Warehouse Staff ]                [ Restaurant Supervisor ]
      Creates & Submits                        Fulfills & Dispatches                    Confirms Physical Receipt
              │                                          │                                          │
              ▼                                          ▼                                          ▼
   ┌───────────────────────┐                  ┌───────────────────────┐                  ┌───────────────────────┐
   │     SupplyRequest     │                  │        Supply         │                  │  Receipt Confirmation │
   │  Status: "Submitted"  │ ───────────────► │  Status: "Dispatched" │ ───────────────► │  Status: "Confirmed"  │
   │  (NO STOCK EFFECT)    │                  │  (NO STOCK EFFECT)    │                  │  (REDUCES WH STOCK)   │
   └───────────────────────┘                  └───────────────────────┘                  └───────────────────────┘
```

### Detailed Lifecycle Steps:

#### Step 1: Supply Request Creation (`SupplyRequest`)
- **Actor:** Restaurant Supervisor (scoped to their restaurant).
- **Action:** Selects items, specifies quantities, clicks "إرسال الطلب".
- **Validation:** Quantities > 0; items must be active.
- **Stock Impact:** `ZERO` (Requests are pure requisitions).
- **Status:** `Submitted` (`مقدم للمستودع`).

#### Step 2: Warehouse Fulfillment & Dispatch (`Supply`)
- **Actor:** Warehouse Staff (scoped to target warehouse).
- **Action:** Reviews pending requests, enters fulfilled quantities (Full or Partial), clicks "إرسال الشحنة".
- **Validation:** Fulfilled quantity $\le$ Requested quantity.
- **Stock Impact:** `ZERO`.
- **Status:** `Dispatched` (`تم الشحن / في الطريق`).

#### Step 3: Restaurant Receipt Confirmation (`Receipt Confirmation`)
- **Actor:** Restaurant Supervisor (scoped to destination restaurant).
- **Action:** Opens the dispatched supply, enters actual received quantities per line item, clicks "تأكيد الاستلام".
- **Validation:** 
  - $0 \le \text{Received Quantity} \le \text{Dispatched Quantity}$.
  - Idempotency key verified; no prior confirmation.
  - Warehouse stock must be sufficient to fulfill deduction.
- **Stock Impact:** **Warehouse Stock is reduced by the exact confirmed received quantity** via `RESTAURANT_RECEIPT_CONFIRMED` ledger entries.
- **Discrepancy Handling:** If `Received < Dispatched`, the missing difference is recorded in `Discrepancies` table with reason notes.

---

## 3. Physical Stock Count Workflow (Warehouse Only)

```
[ Initiated / Open ] ──► [ In Counting ] ──► [ Pending Approval ] ──► [ Approved / Adjusted ]
                                                     │
                                                     └──► [ Rejected / Recount ]
```

### Steps:
1. **Initiated (`بدء الجرد`):** Warehouse manager starts a stock count for a specific warehouse. System snapshots system quantities. Supports blind counting (counter UI hides system quantities).
2. **In Counting (`جاري العد والتسجيل`):** Warehouse staff records physical counts for each item.
3. **Pending Approval (`بانتظار الاعتماد`):** Variances are computed (`Variance = Physical - System`). Document sent to Admin/Owner.
4. **Approved (`معتمد وتمت التسوية`):** Admin/Owner approves the count. The system posts `PHYSICAL_ADJUSTMENT` ledger movements for non-zero variances and updates `StockBalance`. Historical count lines remain permanently immutable.

---

## 4. Restaurant Consumption Workflow

1. **Actor:** Restaurant Supervisor.
2. **Action:** Logs daily ingredient consumption (`ConsumptionRecord`) specifying Restaurant, Item, Quantity, Unit, and Business Date.
3. **Stock Impact:** **ZERO** (Does not touch warehouse stock). Used exclusively for consumption analytics and variance tracking against confirmed supplies.
