# 01 — Product Overview & Multi-Tenancy Architecture

> **Document ID:** SPEC-01  
> **Topic:** Product Vision, Organizational Hierarchy, Multi-Tenancy Isolation, and Domain Boundaries

---

## 1. Product Vision & Positioning

The **Restaurant Inventory Management System** is an enterprise-grade, Arabic-first SaaS and on-premise inventory and supply chain management platform specifically engineered for restaurant chains, hospitality groups, and central commissary kitchens.

The system is designed as a **mission-critical ERP operational tool** characterized by:
- High data density and operational ergonomics.
- Strict accounting and stock invariants.
- High-trust transactional isolation.
- Complete elimination of phantom stock and unverified transfers.

---

## 2. Multi-Company Organizational Hierarchy

The system operates under a strict multi-tenant architecture where every business entity is scoped to a single `Company` (Tenant):

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Company (Tenant Isolation)                      │
│                    Identified by immutable companyId                   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
         ┌──────────────────────────┴──────────────────────────┐
         │                                                     │
         ▼                                                     ▼
┌───────────────────────────────────┐       ┌───────────────────────────────────┐
│            Warehouses             │       │       Restaurants / Branches      │
│  • Holds 100% of Physical Stock   │       │  • Consuming operational units    │
│  • Central distribution points    │       │  • NO stock balances              │
│  • Manages Receiving & Counts     │       │  • Creates Supply Requests        │
│  • Fulfills Supply Requests       │       │  • Confirms Received Supplies     │
└───────────────────────────────────┘       └───────────────────────────────────┘
```

### Multi-Tenancy Rules:
1. **Tenant Identification:** The current tenant is derived **exclusively** from the authenticated user's cryptographically signed session.
2. **Untrusted Request Context:** Client payloads, query parameters, URL segments, and HTTP headers containing `companyId` are **strictly ignored** for security decisions.
3. **Global Tenant Filtering:** All Entity Framework Core database queries automatically apply tenant isolation:
   ```csharp
   builder.Entity<Item>().HasQueryFilter(e => e.CompanyId == _currentUserService.CompanyId);
   ```
4. **Cross-Tenant Data Leakage:** Any attempt by an actor in Company A to access or mutate an entity belonging to Company B results in an immediate `404 Not Found` or `403 Forbidden` response and an audit security alert.

---

## 3. High-Level System Supply Chain Flow

The core supply chain lifecycle flows deterministically across suppliers, central warehouses, and restaurant branches:

```
[ Supplier ]
     │ (Shipment)
     ▼
[ Central Warehouse ] ◄── (Receiving Order POSTED: Warehouse Stock INCREASES)
     │                ◄── (Reconciliation: Reconciles Variance +/-)
     │
     │ ◄── [ Restaurant Supervisor ] creates [ Supply Request ] (NO stock effect)
     │
     ├── [ Warehouse Staff ] fulfills & dispatches [ Supply / Dispatch ] (NO stock effect)
     │
     ▼
[ Restaurant Dock ]
     │
     └── [ Restaurant Supervisor ] confirms [ Receipt Confirmation ]
               │
               └──► (Warehouse Stock is REDUCED by actual received quantity)
               └──► (Restaurant records daily [ Consumption ] for analytics)
```

---

## 4. Product Boundaries & Out-of-Scope Concepts

To maintain structural clarity and prevent architectural bloat, the following modules are explicitly excluded from this system:

| Module / Concept | Status | Reason & Architectural Guard |
| :--- | :--- | :--- |
| **Point of Sale (POS) Billing** | Excluded | System manages back-of-house inventory and supply logistics, not front-of-house customer billing. |
| **Restaurant Inventory Balances** | **Strictly Prohibited** | Restaurants do not store or track stock balances; only warehouses own stock. |
| **HR, Payroll, & Employee Scheduling** | Excluded | Standard HR functions belong to third-party HR systems. |
| **Direct Stock Writes** | **Strictly Prohibited** | Direct balance updates bypassing the stock ledger are forbidden. |
| **Accountant Product Role** | **Retired** | The `Accountant` role is permanently removed to prevent role conflicts with `Owner` and `Admin`. |
