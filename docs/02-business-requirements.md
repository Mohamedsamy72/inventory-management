# 02 — Business Requirements & Core Invariants

> **Document ID:** SPEC-02  
> **Topic:** Inviolable Business Invariants, Operational Rules, and Supply Chain Mechanics

---

## 1. The Fundamental Inventory Invariant: Warehouse-Only Ownership

### Rule Statement:
**ONLY WAREHOUSES HOLD AND OWN INVENTORY BALANCES.**  
Restaurants and retail branches **NEVER** hold inventory balances, on-hand balances, in-transit balances, or stock valuation assets.

### Concrete System Implications:
1. **No Restaurant Stock Tables:** The database schema must **never** contain tables named `restaurant_stocks`, `branch_inventories`, `restaurant_balances`, or `restaurant_in_transit`.
2. **No Restaurant Balance APIs:** There are no endpoints returning restaurant inventory or restaurant stock levels.
3. **Restaurant Role Scope:** A restaurant exists purely as an operational consumption node with:
   - Supply Requests (needs).
   - Confirmed Receipts (actual received goods).
   - Consumption Logs (kitchen consumption history).
   - Discrepancy Records (issues at delivery).

---

## 2. Inventory Source of Truth & Equation

Warehouse inventory is an immutable projection of authorized stock movements.

$$\text{Current Stock} = \text{Opening Balance} + \text{Incoming Posted} \pm \text{Reconciliation Variance} - \text{Confirmed Restaurant Receipts} \pm \text{Physical Adjustments}$$

### Allowed Movement Types (`MovementType` Enum):
1. `OPENING_BALANCE`: Initial stock setup.
2. `INCOMING_POSTED`: Immediate stock addition upon posting a supplier receiving order.
3. `INCOMING_RECONCILIATION`: Post-posting delta (+/-) resulting from physical verification.
4. `RESTAURANT_RECEIPT_CONFIRMED`: Warehouse stock reduction when a restaurant confirms receipt of goods.
5. `PHYSICAL_ADJUSTMENT`: Approved adjustment resulting from an official warehouse stock count.

---

## 3. Stock Event Mechanics & Timing

### A. Incoming Goods from Suppliers (Receiving)
1. When an incoming receiving order is **POSTED**, warehouse stock increases immediately by the expected/declared quantity.
2. Later, when physical verification is completed:
   - If `Actual == Expected`, no adjustment movement is created.
   - If `Actual != Expected`, the system posts an `INCOMING_RECONCILIATION` movement with `Quantity = (Actual - Expected)`.
   - **CRITICAL:** The verification step **must never** add the entire actual quantity a second time.

### B. Warehouse Fulfillment & Dispatch
1. A restaurant creates a **Supply Request** (e.g., Request 20 KG of Meat).
2. The warehouse fulfills the request (e.g., fulfills 18 KG).
3. The warehouse issues a **Dispatch** (`SUP-xxxx`).
4. **INVARIANT:** **DISPATCH DOES NOT REDUCE WAREHOUSE STOCK.**
   - Stock remains in the warehouse balance while in transit.
   - No phantom "in-transit" balance is created for the restaurant.

### C. Restaurant Receipt Confirmation
1. The Restaurant Supervisor inspects the physical delivery and records actual received quantities (e.g., Received 18 KG).
2. Upon confirmation:
   - An immutable stock movement `RESTAURANT_RECEIPT_CONFIRMED` of `-18 KG` is posted to the warehouse stock ledger.
   - The warehouse stock balance decreases by **exactly 18 KG**.
   - If partial receipt occurred (e.g., Dispatched 20 KG, Received 18 KG), only 18 KG is deducted; the remaining 2 KG variance generates a formal `Discrepancy` record.

### D. Restaurant Consumption
1. Kitchen supervisors log daily ingredient consumption (`ConsumptionRecord`).
2. **INVARIANT:** **CONSUMPTION RECORDS HAVE ZERO EFFECT ON WAREHOUSE STOCK.**
   - Consumption logs are statistical operational records used for menu yield analysis and consumption trend reporting.

---

## 4. Master Data Invariants

### A. Item Code Auto-Generation
- Item codes (`generatedCode`) are **generated automatically by the server**.
- Client submissions containing custom item codes are strictly rejected.
- Item codes must be unique per company, formatted as `ITM-000001` to `ITM-999999`, and protected against concurrent generation collisions.

### B. Unit Conversions
- All inventory calculations, stock balances, and ledger entries are normalized to the **Base Unit** (`baseUnitId`).
- Item-specific conversions (`ItemUnitConversion`) define the multiplier between Purchase Units and Base Units (`1 Carton = 12 KG`).
- Conversion factors are resolved **exclusively by the backend server** using configured master data. Client-submitted conversion rates are rejected.

### C. Costing & Purchase Prices
- Static purchase prices on items are eliminated.
- Unit cost is dynamically derived from actual posted receiving orders using the **Weighted Average Cost (WAC)** method.
- Financial cost information is accessible **exclusively to the Owner role**.
