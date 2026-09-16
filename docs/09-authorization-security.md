# 09 — Authorization & Security Hardening

> **Document ID:** SPEC-09  
> **Topic:** Policy Engine, Role Denial, IDOR Prevention, Mass Assignment Protection, and Tenant Isolation

---

## 1. Authorization Pipeline Architecture

Authorization evaluation in ASP.NET Core follows a strict three-tier evaluation model:

```
[ Incoming Request ]
         │
         ▼
[ 1. Tenant Context Verification ] ──► Matches Authenticated User CompanyId
         │
         ▼
[ 2. Role Denial Check ] ────────────► Checks if Role is explicitly prohibited (e.g. Admin -> Costs/Audit)
         │
         ▼
[ 3. Resource & Scope Guard ] ───────► Verifies Warehouse/Restaurant Scope & Entity Ownership
         │
         ▼
[ Execute Command / Query ]
```

---

## 2. Server-Side Protection Against Standard Vulnerabilities

### 2.1 Insecure Direct Object Reference (IDOR)
- **Vulnerability Scenario:** A malicious user swaps a UUID in `/api/v1/receiving-orders/{id}` or `/api/v1/supplies/{id}/confirm` to access another branch's record.
- **Remediation:** Every resource lookup checks both `CompanyId` and the user's explicit scope:
  ```csharp
  var supply = await _context.Supplies
      .FirstOrDefaultAsync(s => s.Id == supplyId && s.CompanyId == _currentTenant.Id);
  
  if (supply == null) return NotFound();
  
  if (!userAuthorizedRestaurantIds.Contains(supply.RestaurantId))
      return Forbid();
  ```

### 2.2 Mass Assignment & Over-Posting
- **Vulnerability Scenario:** Client sends extra JSON properties (e.g., `is_active: true`, `role: "Owner"`, `unitCost: 0.01`, `companyId: "..."`).
- **Remediation:** Endpoints bind strictly to dedicated input Command DTOs. Auto-mapping untrusted input into domain entities without explicit property projection is forbidden.

### 2.3 Server-Derived Serving Warehouse (ADR-028)

- **Vulnerability Scenario:** A Restaurant Supervisor edits the `POST /api/v1/supply-requests` payload to insert `"warehouseId": "<another warehouse in the company>"`, redirecting a requisition away from their branch's serving warehouse — a lateral movement inside the tenant that scope filtering alone would not catch, because the supervisor legitimately has *some* create permission.
- **Remediation — remove the input, not just validate it:**
  ```csharp
  // CreateSupplyRequestCommand declares NO WarehouseId property.
  // An over-posted value is discarded by the model binder; nothing downstream can read it.
  var restaurant = await _context.Restaurants
      .Where(r => r.Id == command.RestaurantId)          // global tenant filter applies
      .Select(r => new { r.Id, r.DefaultServingWarehouseId, r.DefaultServingWarehouse!.Status })
      .FirstOrDefaultAsync();

  if (restaurant is null) return NotFound();                       // out of tenant
  if (!userAuthorizedRestaurantIds.Contains(restaurant.Id)) return Forbid();  // out of scope
  if (restaurant.Status != WarehouseStatus.Active)
      throw new BusinessRuleException(ErrorCodes.ServingWarehouseUnavailable);

  var warehouseId = restaurant.DefaultServingWarehouseId;          // derived, never supplied
  ```
- **Why this shape:** validating a client-supplied warehouse would still leave a value that some future handler, mapper, or bulk-edit endpoint might trust. Not declaring the property removes the class of bug rather than one instance of it.
- **Structural guarantee:** the composite foreign key `(company_id, default_serving_warehouse_id) → warehouses(company_id, id)` makes a cross-company serving warehouse impossible at the database level (ADR-016), so same-company-ness is never merely asserted in code.
- **Unchanged:** Warehouse Staff still see and process only requests whose derived warehouse is inside their `UserWarehouseScope`. Derivation removes the supervisor's ability to choose; it does not relax the warehouse side of authorization.

---

### 2.4 Privilege Escalation
- Users cannot modify their own roles or scopes.
- Only an `Owner` can create another `Owner` or assign system administrator capabilities.
- An `Admin` cannot grant permissions or roles that exceed their own operational bounds.

---

## 3. Financial & Costing Data Quarantine

Financial properties are filtered at the SQL query projection level, not merely masked in the UI:

```csharp
public async Task<ItemDto> GetItemById(Guid id, UserContext user)
{
    var isFinancialAuthorized = user.Role == Roles.Owner;

    return await _context.Items
        .Where(i => i.Id == id)
        .Select(i => new ItemDto
        {
            Id = i.Id,
            NameArabic = i.NameArabic,
            GeneratedCode = i.GeneratedCode,
            CategoryName = i.Category.NameArabic,
            BaseUnitName = i.BaseUnit.NameArabic,
            // Strictly null for Admin, Warehouse Staff, and Restaurant Supervisor
            AverageUnitCost = isFinancialAuthorized ? i.StockBalances.Average(b => b.AverageUnitCost) : null
        })
        .FirstOrDefaultAsync();
}
```

---

## 4. Global Query Filters & Tenant Guardrails

To prevent accidental data leakage across companies, Entity Framework Core applies Global Query Filters across all tenant-scoped entities:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(CreateTenantFilter(entityType.ClrType));
        }
    }
}
```

Any use of `.IgnoreQueryFilters()` requires explicit justification and architectural review.
