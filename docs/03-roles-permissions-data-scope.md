# 03 — Roles, Permissions & Data Scope

> **Document ID:** SPEC-03  
> **Topic:** The 5 Core Roles, Fine-Grained Permission Catalog, Server-Side Role Denial, and Data Scopes

---

## 1. The 5 Core Product Roles

The platform recognizes exactly five distinct product roles. The historical `Accountant` role is permanently retired.

```
                  ┌──────────────────────────────┐
                  │            Owner             │
                  │ (Full Authority & Financial) │
                  └──────────────┬───────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         ▼                       ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│      Admin       │    │ Warehouse Staff  │    │Restaurant Superv.│
│  (Operational)   │    │(Scoped Warehouse)│    │(Scoped Restaurant│
│ [NO FINANCIALS]  │    │ [Logistics Only] │    │  [Branch Ops]    │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │
                                 ▼
                        ┌──────────────────┐
                        │       User       │
                        │(Explicit Grants) │
                        └──────────────────┘
```

### Role Profiles & Authorities:

1. **Owner (المالك):**
   - Highest business authority with company-wide scope.
   - Unrestricted operational access across all warehouses and restaurants.
   - **Exclusive access** to financial valuations, unit costs, cost histories, and company audit logs.
   - Full user management authority: can create and assign all 5 roles and manage scopes.

2. **Admin (المدير التشغيلي):**
   - Broad operational administrator.
   - Manages items, categories, units, unit conversions, suppliers, warehouses, restaurants, and user provisioning.
   - Oversees supply requests, dispatches, receiving orders, stock counts, and discrepancies.
   - **CRITICAL SERVER-SIDE RESTRICTIONS (Role Denial):**
     - **CANNOT** view item costs, purchase prices, inventory valuation, or financial reports.
     - **CANNOT** access Audit Logs (`/api/v1/audit`) or the Activity Monitor.

3. **Warehouse Staff (موظف المخزن):**
   - Focused operational logistics role.
   - Bound strictly to assigned warehouses via `UserWarehouseScope`.
   - Primary duties: process incoming supplier goods ("الداخل الى المخزن" - `Receiving`) and fulfill/dispatch restaurant orders ("الطلبيات الواردة من المطعم" & "الصادر الى المطعم").
   - Denied access to administrative, financial, audit, and user management modules.

4. **Restaurant Supervisor (مشرف الفرع / المطعم):**
   - Operational branch role bound strictly to assigned restaurants via `UserRestaurantScope`.
   - Primary duties: create supply requests ("طلبات البضاعه"), confirm received goods ("تأكيد استلام البضاعه"), and review receipt discrepancies ("فروقات الاستلام").
   - Denied access to warehouse stock balances, inventory valuation, supplier setups, and user management.

5. **User (مستخدم عام):**
   - Base authenticated account with zero default capabilities.
   - Access is determined strictly through explicit permission assignments (`UserPermission`) and assigned data scopes.

---

## 2. Server-Side Role Denial Architecture

To guarantee that operational administrators cannot access sensitive financial figures or audit trails, the authorization engine applies **Role Denial** rules that override any direct user permission grants:

```
                  Authorization Request (User + Resource + Action)
                                         │
                                         ▼
                         Is Role == "Admin" AND Action in
                     {"costs:view", "audit:view", "audit:export"}?
                                    /         \
                                  YES          NO
                                  /             \
                   ┌───────────────────────┐    Check Role Permissions +
                   │ DENIED (HTTP 403)     │    User Permissions + Scope
                   │ (Override Grants)     │
                   └───────────────────────┘
```

---

## 3. Granular Permission Catalog

Permissions follow the standard syntax: `<module>:<action>`.

| Module | Permission Code | Description (AR) | Default Owner | Default Admin | Default WH Staff | Default Rest. Sup. |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| **Items** | `items:view`<br>`items:create`<br>`items:update`<br>`items:delete` | عرض الأصناف<br>إضافة صنف<br>تعديل صنف<br>تعطيل صنف | ✅<br>✅<br>✅<br>✅ | ✅<br>✅<br>✅<br>✅ | ✅<br>❌<br>❌<br>❌ | ✅<br>❌<br>❌<br>❌ |
| **Master Data** | `categories:manage`<br>`units:manage`<br>`suppliers:manage`<br>`conversions:manage` | إدارة الأقسام<br>إدارة الوحدات<br>إدارة الموردين<br>إدارة التحويلات | ✅<br>✅<br>✅<br>✅ | ✅<br>✅<br>✅<br>✅ | ❌<br>❌<br>❌<br>❌ | ❌<br>❌<br>❌<br>❌ |
| **Warehouses & Branches**| `warehouses:manage`<br>`restaurants:manage` | إدارة المستودعات<br>إدارة الفروع | ✅<br>✅ | ✅<br>✅ | ❌<br>❌ | ❌<br>❌ |
| **Supply Requests** | `supply_requests:create`<br>`supply_requests:view`<br>`supply_requests:fulfill` | إنشاء طلب توريد<br>عرض طلبات التوريد<br>تلبية وشحن الطلب | ✅<br>✅<br>✅ | ✅<br>✅<br>✅ | ❌<br>✅ (Scoped)<br>✅ (Scoped) | ✅ (Scoped)<br>✅ (Scoped)<br>❌ |
| **Supplies** | `supplies:view`<br>`supplies:dispatch`<br>`supplies:confirm` | عرض التوريدات<br>إرسال الشحنة<br>تأكيد استلام الفرع | ✅<br>✅<br>✅ | ✅<br>✅<br>✅ | ✅ (Scoped)<br>✅ (Scoped)<br>❌ | ✅ (Scoped)<br>❌<br>✅ (Scoped) |
| **Receiving** | `receiving:view`<br>`receiving:create`<br>`receiving:submit`<br>`receiving:verify`<br>`receiving:reverse` | عرض الاستلام الوارد<br>إنشاء استلام<br>ترحيل استلام<br>مطابقة استلام<br>عكس استلام | ✅<br>✅<br>✅<br>✅<br>✅ | ✅<br>✅<br>✅<br>✅<br>✅ | ✅ (Scoped)<br>✅ (Scoped)<br>✅ (Scoped)<br>✅ (Scoped)<br>❌ | ❌<br>❌<br>❌<br>❌<br>❌ |
| **Stock Counts** | `stock_counts:view`<br>`stock_counts:create`<br>`stock_counts:count`<br>`stock_counts:approve` | عرض الجرد<br>بدء جرد جديد<br>تسجيل الكميات<br>اعتماد الجرد والتسوية | ✅<br>✅<br>✅<br>✅ | ✅<br>✅<br>✅<br>✅ | ✅ (Scoped)<br>❌<br>✅ (Scoped)<br>❌ | ❌<br>❌<br>❌<br>❌ |
| **Financials**<br>🔒 `NON_GRANTABLE` | `costs:view`<br>`valuation:view` | عرض التكاليف والأسعار<br>عرض تقييم المخزون | ✅<br>✅ | ⛔ **DENIED**<br>⛔ **DENIED** | ⛔ **DENIED**<br>⛔ **DENIED** | ⛔ **DENIED**<br>⛔ **DENIED** |
| **Audit Log**<br>🔒 `NON_GRANTABLE` | `audit:view`<br>`audit:export` | عرض سجل التدقيق<br>تصدير سجل التدقيق | ✅<br>✅ | ⛔ **DENIED**<br>⛔ **DENIED** | ⛔ **DENIED**<br>⛔ **DENIED** | ⛔ **DENIED**<br>⛔ **DENIED** |
| **Users & Scope** | `users:view`<br>`users:manage`<br>`users:scope` | عرض المستخدمين<br>إدارة المستخدمين<br>تعيين النطاقات | ✅<br>✅<br>✅ | ✅<br>✅<br>✅ | ❌<br>❌<br>❌ | ❌<br>❌<br>❌ |
| **Discrepancies**<br>*(added Phase 12 - not in this table's original revision; see docs/27 §20)* | `discrepancies:view`<br>`discrepancies:resolve` | عرض الفروقات<br>حل الفروقات | ✅<br>✅ | ✅<br>✅ | ✅ (Scoped - own warehouses, every type)<br>❌ | ✅ (Scoped - own restaurants, `SupplyReceiptVariance` only)<br>❌ |

---

## 4. Data Scopes & Assignments

1. **Warehouse Scope (`UserWarehouseScope`):**
   - Links a user to one or more specific warehouses in the company.
   - Enforced by server-side query filters:
     ```csharp
     if (userRole == Roles.WarehouseStaff) {
         query = query.Where(r => authorizedWarehouseIds.Contains(r.WarehouseId));
     }
     ```
2. **Restaurant Scope (`UserRestaurantScope`):**
   - Links a user to one or more specific restaurant branches.
   - Enforced by server-side query filters:
     ```csharp
     if (userRole == Roles.RestaurantSupervisor) {
         query = query.Where(r => authorizedRestaurantIds.Contains(r.RestaurantId));
     }
     ```
3. **Company Scope (All Roles):**
   - Universally enforced via EF Core Global Query Filters.

---

## 5. Reconciliation Amendment

### 5.1 Non-Grantable Permissions (ADR-012, CR-010/CR-011)

`costs:view`, `valuation:view`, `audit:view`, and `audit:export` are marked 🔒 `NON_GRANTABLE`. They document `Owner` capability and **can never be assigned to any user of any role**. An attempt to grant one returns `400 NON_GRANTABLE_PERMISSION`, and the permission-assignment UI never lists them.

Previously the matrix showed `❌` for Warehouse Staff and Restaurant Supervisor, implying these codes were merely un-granted by default and therefore grantable. They are not. Financial visibility is `Owner`-only, unconditionally, for every other role including `User` — a grantable permission that can never take effect is a trap, because an administrator would believe they had delegated something.

### 5.2 Exactly One Product Role Per User (ADR-014, CR-013)

Every authorization sample in this document and in `docs/09` reads a **singular** `user.Role`, while `user_roles` is a many-to-many join. With two roles assigned, Role Denial would have no defined subject.

**A user holds exactly one of the five product roles**, enforced by `UNIQUE (user_id)` on `user_roles` and by the user service. Variation within a role is expressed through `user_permissions` and data scopes, never through a second role. `GET /api/v1/account/me` returns a single `role` value.

### 5.3 Operational Authority vs Financial Visibility (ADR-015, CR-015)

`Admin` holds `receiving:reverse` and `stock_counts:approve`. Both trigger server-side cost recomputation. An `Admin` **may** trigger such an operation; the server performs the computation and returns **no** cost field in the response, and the audit entry records it for `Owner` review. Authority over an operation and visibility of its financial output are independent concerns.

### 5.4 Stock Count Roles (CR-014)

`docs/05 §3` refers to a *"warehouse manager"*, which is not a product role. Per the §3 matrix: `Owner` and `Admin` hold `stock_counts:create` and `stock_counts:approve`; Warehouse Staff hold `stock_counts:view` and `stock_counts:count` within their scope only.
