# 04 — Inventory & Stock Model

> ⚠️ **CORRECTED BY ADR-020, ADR-021, ADR-022.** Two statements below are superseded — do not implement them as written:
> - §3 lists `Version` (`byte[]` / RowVersion). **PostgreSQL has no auto-maintained rowversion.** The concurrency token is the `xmin` system column (ADR-022, CR-035, `docs/29` DB-01). A `byte[]` column would silently fail to detect lost updates.
> - §4 describes the WAC formula for inbound movements only. Per-movement-type costing behaviour — issues and negative adjustments are valued at the current WAC and leave it **unchanged**; `INCOMING_RECONCILIATION` reverses at the originating receiving line's own `unit_cost` — is specified in `docs/04-end-to-end-business-flow.md` §22 (ADR-020).
>
> Also see `docs/04-end-to-end-business-flow.md` §21 (in-transit is derived, never stored — ADR-018) and §23 (negative stock is absolutely prohibited — ADR-021).

> **Document ID:** SPEC-04  
> **Topic:** Ledger Architecture, Stock Movements, Materialized Balances, Negative Stock Guards, and Cost Calculations

---

## 1. The Dual-Layer Inventory Model

The system employs an **Event-Sourced Append-Only Ledger** as the primary source of truth, alongside a **Materialized Stock Balance Table** for high-performance querying and concurrency locks.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Stock Mutation Business Transaction                  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
          ┌─────────────────────────┴─────────────────────────┐
          │ (1) Append Movement                               │ (2) Update Projection
          ▼                                                   ▼
┌───────────────────────────────────┐               ┌───────────────────────────────────┐
│           StockLedger             │               │           StockBalance            │
│  • Immutable Append-Only Rows     │               │  • Current physical quantity      │
│  • Full Audit & Actor Metadata    │               │  • Weighted Average Cost (WAC)    │
│  • Reference IDs (REC, SUP, CNT)  │               │  • Row Version / Concurrency Lock │
│  • Historical traceability        │               │  • Constraint: quantity >= 0      │
└───────────────────────────────────┘               └───────────────────────────────────┘
```

---

## 2. Stock Ledger (`StockLedger`) Specification

The ledger records every physical increment or decrement in warehouse inventory.

### Structure & Fields:
- `Id` (UUID, Primary Key)
- `CompanyId` (UUID, Indexed)
- `WarehouseId` (UUID, Indexed)
- `ItemId` (UUID, Indexed)
- `MovementType` (`OPENING_BALANCE`, `INCOMING_POSTED`, `INCOMING_RECONCILIATION`, `RESTAURANT_RECEIPT_CONFIRMED`, `PHYSICAL_ADJUSTMENT`)
- `Quantity` (`decimal(18,4)` — positive for additions, negative for reductions)
- `BaseQuantity` (`decimal(18,4)` — normalized quantity in item's base unit)
- `UnitId` (UUID — unit utilized during transaction)
- `ReferenceType` (`ReceivingOrder`, `Supply`, `StockCount`, `ManualAdjustment`)
- `ReferenceId` (UUID — foreign key to source document)
- `UnitCost` (`decimal(18,4)`, Nullable — unit cost at transaction time; Owner visible)
- `TotalCost` (`decimal(18,4)`, Nullable — total financial impact; Owner visible)
- `ActorUserId` (UUID — user who executed the posting)
- `OccurredAt` (Timestamp with timezone, UTC)
- `CreatedAt` (Timestamp with timezone, UTC)

### Invariants:
1. **Append-Only:** `UPDATE` and `DELETE` queries on `StockLedger` are strictly prohibited by application services and database trigger rules.
2. **Reversals:** Corrections require an explicit reversing movement entry referencing the original transaction.

---

## 3. Materialized Stock Balance (`StockBalance`)

### Structure & Fields:
- `Id` (UUID, Primary Key)
- `CompanyId` (UUID)
- `WarehouseId` (UUID)
- `ItemId` (UUID)
- `Quantity` (`decimal(18,4)` — current balance in Base Unit)
- `BaseUnitId` (UUID)
- `AverageUnitCost` (`decimal(18,4)` — current Weighted Average Cost)
- ~~`Version` (`byte[]` / RowVersion for optimistic concurrency)~~ — **SUPERSEDED (ADR-022).** Use the PostgreSQL `xmin` system column via `UseXminAsConcurrencyToken()`. No `version` column exists.
- `UpdatedAt` (Timestamp with timezone, UTC)

### Constraints & Indexes:
- **Unique Constraint:** `UNIQUE (CompanyId, WarehouseId, ItemId)`
- **Check Constraint:** `CHECK (Quantity >= 0)` (Enforces zero-tolerance for negative stock).

---

## 4. Mathematical Costing Engine: Weighted Average Cost (WAC)

When an incoming receiving order is posted or reconciled, the new average unit cost for an item in a warehouse is updated using the WAC formula:

$$\text{New Average Cost} = \frac{(\text{Current Qty} \times \text{Current Avg Cost}) + (\text{Incoming Qty} \times \text{Incoming Unit Cost})}{\text{Current Qty} + \text{Incoming Qty}}$$

### Precision & Rounding Rules:
- **Calculation Precision:** Handled using .NET `decimal` (128-bit fixed-point) and PostgreSQL `NUMERIC(18,4)`. Floating-point types (`float`, `double`) are strictly prohibited.
- **Rounding Strategy:** Midpoint rounding away from zero (`MidpointRounding.AwayFromZero`) applied only upon final financial summary projection.

---

## 5. Negative Stock & Concurrency Protection

1. **Pessimistic / Optimistic Locking:**
   When mutating stock (e.g., during Receipt Confirmation or Receiving Posting), the repository executes an atomic update with concurrency check:
   ```sql
   UPDATE stock_balances
   SET quantity = quantity - @DeductionQty,
       updated_at = NOW()
   WHERE id = @BalanceId AND quantity >= @DeductionQty;
   ```
2. **Race Condition Prevention:**
   If two restaurant supervisors concurrently attempt to confirm receipts drawing from a warehouse with remaining quantity `20 KG` (Request A: `15 KG`, Request B: `15 KG`):
   - The first transaction acquires the lock, reduces stock to `5 KG`, and succeeds.
   - The second transaction fails the `quantity >= 15` predicate, rollback occurs, and the server returns RFC 7807 error `INSUFFICIENT_WAREHOUSE_STOCK` with a descriptive Arabic message:  
     `"الرصيد المتاح في المستودع غير كافٍ لإتمام العملية"`.
