# 15 — Costing Model & Financial Data Security

> **Document ID:** SPEC-15  
> **Topic:** Weighted Average Costing (WAC), Financial Precision, and Role-Based Financial Quarantine

---

## 1. Accounting & Costing Principles

1. **Source of Truth for Costs:** Cost values originate exclusively from posted supplier receiving orders (`ReceivingOrder`). Static manual purchase price inputs on items are prohibited.
2. **Weighted Average Costing (WAC):** The system calculates and tracks inventory valuation per warehouse and per item using the standard Weighted Average Cost method:
   $$\text{WAC} = \frac{\sum (\text{Quantity}_i \times \text{UnitCost}_i)}{\sum \text{Quantity}_i}$$
3. **Fixed-Point Precision:**
   - Internal math: `decimal(18,4)`
   - PostgreSQL column type: `NUMERIC(18,4)`
   - UI display: `decimal(18,2)` with standard thousand separators.

---

## 2. Strict Role-Based Financial Quarantine

Financial figures represent highly confidential business intelligence (margins, purchase prices, warehouse total values).

### Access Rules by Role:

| Financial Attribute | Owner | Admin | Warehouse Staff | Restaurant Supervisor | User |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Item Unit Cost** | ✅ Visible | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** |
| **Receiving Order Total Cost** | ✅ Visible | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** |
| **Warehouse Inventory Valuation** | ✅ Visible | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** | ⛔ **MASKED (null)** |
| **Financial Valuation Reports** | ✅ Accessible | ⛔ **FORBIDDEN (403)**| ⛔ **FORBIDDEN (403)**| ⛔ **FORBIDDEN (403)**| ⛔ **FORBIDDEN (403)**|

### Implementation Mandate:
- **Server Projection Masking:** Stripping financial columns must occur in backend query projections (LINQ `.Select()` projections), ensuring that raw numbers never traverse the wire in JSON responses.
- **Frontend Hiding is Insufficient:** Merely omitting a column in a React table while transmitting `unitCost` in the API JSON payload is an unacceptable security failure.
