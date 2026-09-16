# Domain Glossary & Terminology Mapping

> **Status:** Authoritative — Mandatory Domain Reference  
> **Rule:** Developers and implementation agents must strictly use these definitions and prohibited terms.

---

## 1. Operational & Organizational Entities

| Term (EN) | Term (AR) | Technical Identifier | Canonical Definition & Invariants |
| :--- | :--- | :--- | :--- |
| **Company / Tenant** | الشركة / المنشأة | `Company` | Top-level tenant container. All transactional data is isolated strictly by `companyId`. |
| **Warehouse** | المستودع / المخزن | `Warehouse` | **The ONLY entity in the system that owns physical stock balances.** Participates in stock movements and physical counts. |
| **Restaurant / Branch** | المطعم / الفرع | `Restaurant` | Operational consumption and service branch. **DOES NOT own stock balances, in-transit stock, or inventory valuations.** Carries exactly one `default_serving_warehouse_id` (ADR-028). |
| **Serving Warehouse** | مستودع الخدمة | `Restaurant.DefaultServingWarehouse` | The single warehouse that supplies a given restaurant. **Derived by the server** on every supply request; never selected by the Restaurant Supervisor and never accepted from a client payload (ADR-028). |
| **Supplier** | المورد | `Supplier` | External vendor providing goods to warehouses via receiving orders. |
| **Item** | الصنف | `Item` | Enterprise catalog product defined with Arabic name, server-generated code, category, base unit, and optional purchase unit. |

---

## 2. Inventory Operations & Workflows

| Term (EN) | Term (AR) | Key Workflow Concept |
| :--- | :--- | :--- |
| **Stock Ledger** | دفتر حركات المخزون | Append-only ledger recording every stock movement with timestamp, actor, reference type, and base quantity. |
| **Stock Balance** | رصيد المخزون المادي | Materialized current stock table per `(companyId, warehouseId, itemId)`. Derived projection of valid ledger movements. |
| **Receiving Order** | الاستلام الوارد / الداخل الى المخزن | Shipment of goods from a supplier to a warehouse. When `POSTED`, increases warehouse stock immediately. |
| **Reconciliation / Verification** | المطابقة والتحقق الفعلي | Post-posting physical check of receiving order. Reconciles variance (+/-) without double-adding received goods. |
| **Supply Request** | طلب التوريد / طلبات البضاعه / الطلبيات الواردة من المطعم | Internal requisition from a restaurant supervisor to a warehouse. **Has ZERO stock impact.** |
| **Supply / Dispatch** | أمر التوريد والإرسال / الصادر الى المطعم | Warehouse fulfilment document. Created in `Prepared` (`قيد التجهيز`) by fulfilment, moved to `Dispatched` by the dispatch act (ADR-017). **Neither transition deducts warehouse stock.** |
| **In-Transit Quantity** | قيد النقل | **Derived, read-only** sum of dispatched-but-unconfirmed quantities, attributed to the **warehouse**. Never persisted, never a balance, never attributed to a restaurant (ADR-018). |
| **Available Quantity** | الرصيد المتاح | `ledger balance − in transit`. A display and advisory figure only; it is not a reservation and creates no ledger effect. |
| **Receipt Confirmation** | تأكيد استلام التوريدة / تأكيد استلام البضاعه | Restaurant supervisor's confirmation of actual received goods. **The ONLY event that deducts warehouse stock.** |
| **Consumption Record** | سجل الاستهلاك | Operational log of kitchen/branch usage. Statistical only; **DOES NOT affect warehouse stock.** |
| **Stock Count** | الجرد الفعلي | Warehouse physical count to reconcile system balance with physical reality via approved adjustment movements. |
| **Discrepancy / Variance** | فروقات الاستلام / الفروقات والتباين | Tracked difference between expected/dispatched quantity and actual verified/received quantity. |

---

## 3. Measurement & Conversion Concepts

| Term (EN) | Term (AR) | Rules & Invariants |
| :--- | :--- | :--- |
| **Base Unit** | الوحدة الأساسية | Standard internal unit of measure (e.g., Kilogram, Piece). Stock ledger and balances normalize all quantities to this unit. |
| **Purchase Unit** | وحدة الشراء | Packaging unit for purchasing/receiving (e.g., Box, Carton). |
| **Conversion Factor** | معامل التحويل | Item-specific multiplier (`1 Carton = 10 KG`). **Resolved exclusively by the server.** Client factors are rejected. |
| **Weighted Average Cost (WAC)** | متوسط التكلفة المرجح | Unit cost calculated from incoming receipts and existing stock: `(OldValue + NewValue) / TotalQuantity`. Changed **only** by inbound movements; issues and adjustments are valued at the current WAC and leave it unchanged (ADR-020). |

---

## 4. Roles & Security Concepts

| Role / Concept (EN) | Arabic Equivalent | Authority & Access Boundaries |
| :--- | :--- | :--- |
| **Owner** | المالك | Full business authority. Unrestricted access to operational, financial, costing, audit, and user management modules. |
| **Admin** | المدير التشغيلي | Operational administrator. **Denied server-side from viewing financial data, costs, and audit logs.** |
| **Warehouse Staff** | موظف المستودع | Operational logistics user scoped to assigned warehouses. Handles receiving and supply fulfillment/dispatch. |
| **Restaurant Supervisor**| مشرف المطعم | Operational branch user scoped to assigned restaurants. Creates supply requests, confirms receipts, logs consumption. |
| **User** | مستخدم عام | Base user with no default capabilities. Access determined strictly by explicit permissions and assigned scopes. |
| **Data Scope** | نطاق البيانات | Server-enforced assignment binding users to specific warehouses (`UserWarehouseScope`) or restaurants (`UserRestaurantScope`). |
| **Role Denial** | حرمان الدور البرمجي | Inviolable server-side rule where a role's inherent restriction (e.g., Admin denied costs) overrides any user permission grant. Each user holds exactly one role, so denial always has a single subject (ADR-014). |
| **Non-Grantable Permission** | صلاحية غير قابلة للمنح | A permission code documenting Owner-only capability that can never be assigned to any user: `costs:view`, `valuation:view`, `audit:view`, `audit:export` (ADR-012). |
| **Idempotency Key** | مفتاح منع التكرار | Client-generated `UUID` in `X-Idempotency-Key` identifying one business intent, so a network retry cannot post a movement twice (`docs/30`). |
| **Document Sequence** | عداد ترقيم المستندات | Transactional per-tenant counter producing gap-free business identifiers (`docs/28`, ADR-025). |
| **Composite Tenant FK** | مفتاح أجنبي مقيّد بالشركة | A foreign key on `(company_id, id)` rather than `id` alone, making cross-tenant references physically impossible (ADR-016). |

---

## 5. Prohibited Concepts & Terminology (Blacklist)

The following concepts are **strictly forbidden** in the codebase, database, API, and frontend:

- ❌ `restaurant_stock` / `restaurant_inventory`
- ❌ `restaurant_in_transit` / `restaurant_in_transit_balance`
- ❌ `Accountant` (Product role is permanently retired)
- ❌ `barcode` (Removed from item model)
- ❌ `name_english` / `nameEn` on Item, Unit, Category
- ❌ Direct stock balance editing / generic `PATCH /status` mutations
- ❌ Client-supplied conversion factors or client-calculated stock results
- ❌ `Discrepancy` used as a `Supply` **status** (it is a separate record — ADR-017)
- ❌ Stock **reservation** at dispatch (dispatch advises, never reserves — ADR-019)
- ❌ Negative stock, transient or otherwise (ADR-021)
- ❌ `SELECT MAX(...) + 1` identifier generation (ADR-025)
- ❌ `float` / `double` for any quantity, factor, or cost
- ❌ A user holding more than one product role (ADR-014)
- ❌ Editing an item's base unit or a used conversion factor in place (ADR-023)
- ❌ `warehouseId` on the supply-request creation DTO — the warehouse is derived (ADR-028)
- ❌ A `restaurant_serving_warehouses` join table — v1.0 is one serving warehouse per restaurant (ADR-028)
- ❌ Falling back to "any warehouse" when a restaurant's serving warehouse is inactive (ADR-028 SW-5)
- ❌ Placeholder charts, mock KPI cards, sample datasets, or "coming soon" screens for deferred features (ADR-029)
- ❌ Substituting SQL Server, MySQL, or Oracle for PostgreSQL (ADR-027)
