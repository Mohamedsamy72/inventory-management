# CLAUDE-HANDOFF.md

> **Audience:** a fresh Claude Code agent taking over this repository.
> **Purpose:** continue implementation from the exact current state without repeating completed work, re-deciding settled questions, or reviving removed requirements.
> **Written:** 2026-09-16, from direct inspection of the repository — not from conversation memory.
> **Repository root:** `D:\سيستم المخازن`

---

## 0. READ THIS FIRST — 60-Second Orientation

| Question | Answer |
| :--- | :--- |
| What is this? | Arabic-only, RTL-native, multi-tenant restaurant inventory & supply management system. |
| Stack | ASP.NET Core 10 + EF Core 10 + PostgreSQL 16 / Next.js 16 + React 19 + TypeScript + Tailwind 4. |
| How far along? | **Phase 1 of 27 (project skeleton). Phase 1 is authored but NOT complete.** |
| Does it build? | **Backend: NO — 4 compiler/analyzer errors. Frontend: YES, fully green.** |
| Is there a database? | **No.** No DbContext, no entity, no migration. PostgreSQL is not even installed on the dev machine. |
| Is there business logic? | **None.** Zero domain types. The domain project contains one marker class. |
| What do I do first? | §21. It is a ~30-minute fix, not a feature. **Do not start Phase 2.** |
| Biggest trap | The `docs/` folder is ~45 files and some older ones are **superseded**. §17 and §15 tell you which. Newer ADR always wins. |

**The single most important fact:** the solution does not currently compile. `build-verification.log` at the repository root is the evidence. Fix that before anything else.

---

## 1. Project Purpose

### 1.1 What the system does

A back-of-house inventory and supply-chain platform for restaurant chains, hospitality groups and central commissary kitchens. It manages the flow of physical goods from supplier → central warehouse → restaurant branch, and the paperwork that proves it.

The operational loop:

```
Supplier
   │ delivers goods
   ▼
Central Warehouse ───── receives (stock INCREASES)
   │                     verifies physically (stock adjusts by the DELTA only)
   │
   │ ◄── Restaurant Supervisor raises a multi-item Supply Request (no stock effect)
   │
   ├── Warehouse Staff fulfil it  → Supply in state "Prepared"  (no stock effect)
   ├── Warehouse Staff dispatch it → Supply in state "Dispatched" (no stock effect)
   ▼
Restaurant dock
   └── Restaurant Supervisor confirms ACTUAL received quantities
            └──► warehouse stock DECREASES (this is the only deduction path)
            └──► any shortfall becomes a Discrepancy record
```

Alongside: physical stock counts with approved adjustments, restaurant consumption logging (statistical only), an append-only stock ledger, and a mandatory audit trail.

### 1.2 Business model

Multi-tenant SaaS / on-premise. Every operational row is isolated by `company_id`. One tenant = one restaurant group, owning many warehouses and many restaurant branches, with its own users, catalogue, suppliers and documents. Cross-tenant data access is prevented structurally at the database level, not merely filtered in application code (§4, ADR-016).

### 1.3 Explicitly out of scope

Point-of-sale billing, HR / payroll / scheduling, recipe management and bill-of-materials, customer loyalty. See §15 for the full DO-NOT-IMPLEMENT list.

---

## 2. Final Architecture

### 2.1 Implemented — verified present and (where stated) building

| Layer | Technology | State |
| :--- | :--- | :--- |
| Solution | `InventorySystem.sln`, 7 projects | ✅ exists |
| `Inventory.Domain` | net10.0 class library, **zero package references** | ✅ compiles (contains only `DomainAssemblyMarker`) |
| `Inventory.Application` | net10.0, references Domain only | ✅ compiles (contains only `ApplicationAssemblyMarker`) |
| `Inventory.Infrastructure` | net10.0, references Application + `Npgsql` 10.0.3 + `FrameworkReference Microsoft.AspNetCore.App` | ✅ compiles |
| `Inventory.Api` | ASP.NET Core 10 Web SDK | ❌ **does not compile** (2 errors, §12.2) |
| Frontend | Next.js 16.3.4, React 19, TypeScript 5.7, Tailwind 4.3.3 | ✅ typecheck, lint, tests, production build all green |
| Central package management | `Directory.Packages.props` | ✅ transitive pinning deliberately OFF |
| Build policy | `Directory.Build.props`: `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, deterministic builds | ✅ — and it is exactly this strictness that is currently failing the build |

**Dependency direction (ADR-002), enforced by tests, not convention:**

```
Inventory.Api  ──►  Inventory.Infrastructure  ──►  Inventory.Application  ──►  Inventory.Domain
                                                                                     ▲
                                        (Domain depends on NOTHING — no EF, no ASP.NET, no Npgsql)
```

`Inventory.Api` references `Inventory.Infrastructure` **solely** so that `Program.cs` can call `AddInfrastructure(...)`. No other Api file may name the infrastructure namespace — `tests/Inventory.ArchitectureTests/CompositionRootRules.cs` fails the build if one does.

### 2.2 Implemented application-level infrastructure

- **Structured logging** — Serilog 9.0.0, `CompactJsonFormatter` to stdout, `Enrich.FromLogContext()`, `UseSerilogRequestLogging`. Configured in `src/Inventory.Api/Program.cs`.
- **Correlation** — `src/Inventory.Api/Middleware/CorrelationIdMiddleware.cs`. Accepts inbound `X-Correlation-Id`, but **validates it** (≤64 chars, `[A-Za-z0-9-_:.]` only) and replaces anything else with a fresh GUID. This is a log-forging guard, not cosmetics.
- **Error handling** — `src/Inventory.Api/Middleware/GlobalExceptionHandler.cs` (`IExceptionHandler`). Emits RFC 7807 `ProblemDetails` with extensions `code`, `messageAr`, `correlationId`; `messageEn` only in Development. Never leaks exception text, stack trace or SQL.
- **Health probes** — `GET /health` (liveness, `Predicate = _ => false`) and `GET /health/ready` (filters on tag `ready`). Readiness is `PostgreSqlReadinessHealthCheck` in `src/Inventory.Infrastructure/HealthChecks/`, which opens a raw `NpgsqlConnection` and runs `SELECT 1`. **No DbContext involved** — deliberately, because none exists yet.
- **OpenAPI** — `Microsoft.AspNetCore.OpenApi` 10.0.11, Development only, document at `/openapi/v1.json`. **No Swagger UI** — deferred to Phase 3 when real endpoints exist.

### 2.3 NOT implemented — deferred to later phases

| Concern | Phase | Current state |
| :--- | :--- | :--- |
| Database schema, DbContext, entities, migrations | 2 | **Nothing exists.** Fully specified in `docs/06` + `docs/29`. |
| Tenant isolation (global query filters, composite FKs) | 2 | Specified, not built. |
| Authentication, sessions, CSRF, OTP, rate limiting | 3 | Specified, not built. |
| Authorization, scopes, role denial, **audit infrastructure** | 4 | Specified, not built. |
| Master data, document sequences | 5 | Specified, not built. |
| Unit conversions | 6 | Specified, not built. |
| Stock ledger, balances, WAC, transactions, concurrency, idempotency | 7 | Specified, not built. |
| Receiving, supply requests, fulfilment, dispatch, confirmation | 8–11 | Specified, not built. |
| Discrepancies, stock counts, audit viewer | 12–14 | Specified, not built. |
| File storage, Reporting | 15–16 | **DEFERRED out of MVP** (ADR-029). See §15. |
| All frontend business screens | F1–F6 | Only the root shell exists. |

### 2.4 Local environment

Backend `http://localhost:5165`, frontend `http://localhost:3000`, PostgreSQL `5432`. Scripts at the repository root: `run-local.bat`, `check-local.bat`, `stop-local.bat`, plus the verification runners `verify-env.bat` and `build-verify.bat`. See §18.

### 2.5 Deployment assumptions (documented, nothing built)

`docs/20-deployment-infrastructure.md`: containerised Next.js + Kestrel behind a reverse proxy with TLS 1.3, HSTS, CSP, `X-Frame-Options: DENY`; managed PostgreSQL with WAL archiving and 30-day PITR; secrets exclusively via environment variables or a secret vault — **zero plaintext secrets in git**.

---

## 3. Repository Structure

```
D:\سيستم المخازن\
├── CLAUDE-HANDOFF.md              ← this file
├── InventorySystem.sln            7 projects, grouped into src/ and tests/ solution folders
├── global.json                    SDK pin: 10.0.302, rollForward latestPatch
├── Directory.Build.props          net10.0, nullable, TreatWarningsAsErrors, AnalysisLevel
├── Directory.Packages.props       central package versions (single source of truth)
├── .editorconfig / .gitignore
│
├── ci-workflow.yml                ⚠ NOT INSTALLED — must be moved to .github/workflows/ci.yml
│
├── verify-env.bat                 toolchain verification → env-verification.log
├── build-verify.bat               build + all tests + frontend checks → build-verification.log
├── run-local.bat                  full local stack launcher (refuses to start without PostgreSQL)
├── check-local.bat                layered diagnostic, read-only
├── stop-local.bat                 stops only what run-local started, by window title
│
├── env-verification.log           ⚠ STALE — Run 1 only, aborted partway (§12.4)
├── build-verification.log         ⚠ FAILING — the current source of truth on build state (§12.2)
│
├── src/
│   ├── Inventory.Domain/          business entities & invariants — currently ONLY a marker class
│   ├── Inventory.Application/     use cases, DTOs, validation — currently ONLY a marker class
│   ├── Inventory.Infrastructure/  DependencyInjection.cs (composition root), HealthChecks/
│   └── Inventory.Api/             Program.cs, Middleware/, appsettings*.json, Properties/
│
├── tests/
│   ├── Inventory.UnitTests/       DomainLayerContractTests.cs
│   ├── Inventory.IntegrationTests/ HealthEndpointTests.cs (6 tests, WebApplicationFactory)
│   └── Inventory.ArchitectureTests/ LayeringRules, CompositionRootRules,
│                                    ForbiddenPatternRules, StartupScriptRules, RepositoryRoot
│
├── frontend/
│   ├── src/app/                   layout.tsx, page.tsx, globals.css  ← the ENTIRE UI today
│   ├── __tests__/                 rtl-foundations.test.tsx (Vitest, 3 tests)
│   ├── e2e/                       shell.spec.ts (@playwright/test)
│   └── package.json, tsconfig.json, eslint.config.mjs, vitest.config.mts, playwright.config.ts
│
├── docs/                          45 markdown specifications — see §19 for reading order
└── ui-ux-pro-max/                 design-intelligence skill (SKILL.md + data/ + scripts/)
                                   MANDATORY reading before authoring any UI component
```

**Navigation tips for an agent:**
- Business rules live in `docs/`, never in code comments alone.
- `docs/decision-log.md` (ADR-001 … ADR-031) is the highest authority. When any two documents disagree, the newer ADR wins.
- `docs/32-specification-conflict-register.md` (CR-001 … CR-099) records every known contradiction and how it was resolved. **Check it before "fixing" an apparent inconsistency — it is probably already resolved.**
- `docs/09-implementation-plan.md` is the work breakdown: 27 phases, task-level, with Definition of Done and verification commands.

---

## 4. Critical Business Invariants

**These are inviolable. Changing any of them silently is the worst failure mode available to you.**

### 4.1 Inventory ownership

1. **Only warehouses hold stock.** Restaurants NEVER have a stock balance, on-hand balance, in-transit balance, or inventory valuation.
2. **No restaurant inventory table may ever exist.** Forbidden names: `restaurant_stock`, `restaurant_stocks`, `restaurant_inventory`, `restaurant_inventories`, `restaurant_on_hand`, `restaurant_in_transit`, `restaurant_inventory_balance`, `branch_inventories`, `restaurant_balances`.
3. A restaurant is a consumption node with four things only: supply requests, confirmed receipts, consumption logs, discrepancy records.

### 4.2 Stock movement rules

4. **Every** stock change originates from an append-only row in `stock_ledger`. There is no "set stock" endpoint and no manual balance overwrite.
5. `stock_ledger` and `audit_logs` are **append-only**, enforced by database trigger plus revoked `UPDATE`/`DELETE` grants — not by application discipline.
6. Corrections are made by posting a **reversing movement**, never by editing history.
7. Allowed `MovementType` values, and nothing else:
   `OPENING_BALANCE`, `INCOMING_POSTED`, `INCOMING_RECONCILIATION`, `RESTAURANT_RECEIPT_CONFIRMED`, `PHYSICAL_ADJUSTMENT`.

### 4.3 Which operations touch stock

| Operation | Ledger row? | Balance effect |
| :--- | :---: | :--- |
| Supplier receiving **submit/post** | ✅ `INCOMING_POSTED` | **increases** by declared quantity, recalculates WAC |
| Receiving **verify/reconcile** | ✅ `INCOMING_RECONCILIATION` | adjusts by **`Actual − Expected` (the DELTA ONLY)** |
| Supply **request** created/submitted | ❌ | **zero** |
| Warehouse **fulfilment** (→ `Prepared`) | ❌ | **zero** |
| Warehouse **dispatch** (→ `Dispatched`) | ❌ | **zero** |
| Restaurant **receipt confirmation** | ✅ `RESTAURANT_RECEIPT_CONFIRMED` | **decreases** by ACTUAL RECEIVED quantity |
| Restaurant **consumption** log | ❌ | **zero** |
| Stock count **approval** | ✅ `PHYSICAL_ADJUSTMENT` | adjusts by variance |
| Stock count **rejection** | ❌ | **zero** |
| Discrepancy **resolution** | ❌ | **zero** |

8. **DISPATCH DOES NOT DEDUCT STOCK.** Goods on the truck remain in the warehouse balance.
9. **RECEIPT CONFIRMATION IS THE ONLY DEDUCTION PATH** for outbound goods.
10. **CONSUMPTION RECORDS HAVE ZERO STOCK EFFECT.** Statistical only.
11. **Reconciliation posts the delta, never the full actual quantity.** Posting `+100` then verifying `Actual = 80` must land on `580` from `500` — never `680`, never `660`. This is the single most likely defect in the entire product; `docs/23` Scenario 2 exists to catch it.

### 4.4 The in-transit consequence (ADR-018) — easy to get catastrophically wrong

Because dispatch does not deduct, a physical count run while a truck is in transit will find a shortfall equal to the goods on that truck. Approving that count would post a `PHYSICAL_ADJUSTMENT` **destroying real inventory value**.

The rule: a figure `قيد النقل` (in-transit) is **computed on demand** as the sum of `dispatched_base_quantity` over `supply_items` whose parent `Supply.status = 'Dispatched'`. It is:
- never persisted as a balance row;
- always attributed to the **warehouse**, never to a restaurant (so §4.1 still holds);
- subtracted from the expected physical figure during a stock count.

### 4.5 Costing (ADR-020)

12. Weighted Average Cost only. FIFO/LIFO are out of scope.
13. WAC changes **only** on inbound movements. `RESTAURANT_RECEIPT_CONFIRMED` and `PHYSICAL_ADJUSTMENT` are valued at the *current* WAC and leave it **unchanged**.
14. `INCOMING_RECONCILIATION` reverses value at **the originating receiving line's own `unit_cost`**, not at the current WAC — otherwise value leaks between receipts.
15. `stock_ledger.unit_cost` is populated for **every** movement type, including those where WAC does not move. A cost not captured at posting time is unrecoverable.
16. `decimal` / `NUMERIC(18,4)` everywhere. `float`/`double` are banned in the domain and the ban is test-enforced.

### 4.6 Negative stock (ADR-021)

17. **Stock is never negative, not even transiently inside a transaction.** `CHECK (quantity >= 0)` is absolute.
18. Any operation that would breach it fails atomically with `INSUFFICIENT_STOCK`. The business resolution is an approved physical stock count — the system never invents a number to make an operation succeed.

### 4.7 Units and conversions (ADR-023)

19. All balances and ledger rows are normalised to the item's **base unit**.
20. Conversion factors are resolved **exclusively server-side**. A client-submitted factor or base quantity is rejected.
21. `items.base_unit_id` is **immutable** once any ledger row exists for that item.
22. An `item_unit_conversions` row is **immutable once used**; a correction is a new row and the old one is deactivated.

### 4.8 Identifiers (ADR-008, ADR-024, ADR-025)

23. All item codes and document numbers are **server-generated**, gap-free, monotonic per `(company, type, period)`.
24. Generated identifiers are **immutable**.
25. `SELECT MAX(...) + 1` is forbidden. So are PostgreSQL `SEQUENCE` objects for business identifiers — they do not roll back, and a gap in a numbered document series reads to an auditor as a deleted record.
26. Client-supplied `generatedCode` / `documentNumber` → `400 GENERATED_FIELD_NOT_ACCEPTED`. The property is **not declared on the DTO**, so an over-post is discarded by the model binder.

### 4.9 Financial visibility (ADR-012)

27. Costs, valuations and cost-bearing reports are visible to the **`Owner` role only** — unconditionally, for every other role including `User`.
28. Masking happens in the **SQL projection**, not the UI. A column omitted from a React table while still present in the JSON is a security failure.

### 4.10 Serving warehouse (ADR-028)

29. Each restaurant has exactly **one** `default_serving_warehouse_id` (`NOT NULL`).
30. The Restaurant Supervisor **never** selects a warehouse. The supply-request DTO **does not declare `warehouseId`**.
31. No active serving warehouse → `409 SERVING_WAREHOUSE_UNAVAILABLE`. **Never fall back to "any warehouse".**

---

## 5. Roles and Authorization

### 5.1 The formula

```
Authorization = Authentication
              → Company / Tenant isolation
              → Role
              → Explicit Permissions
              → Data Scope
              → Business Rules
```

Evaluated server-side in that order. **Frontend authorization is UX only and is never a security control.**

### 5.2 Exactly five roles — and exactly one per user

**ADR-014: a user holds exactly ONE product role**, enforced by `UNIQUE (user_id)` on `user_roles`. Every authorization code sample reads a singular `user.Role`; with two roles assigned, Role Denial would have no defined subject. Variation within a role is expressed through `user_permissions` and data scopes — never a second role.

#### Owner (المالك)
- **Responsibilities:** company-wide governance, master setup, user creation, valuation oversight, audit review.
- **Scope:** company-wide, unrestricted.
- **Financial visibility:** ✅ **FULL — and exclusively.**
- **Audit visibility:** ✅ **FULL — and exclusively.**
- **Notes:** the only role that can create another `Owner`.

#### Admin (المدير التشغيلي)
- **Responsibilities:** operational administration — items, categories, units, conversions, suppliers, warehouses, restaurants, user provisioning; oversees requests, dispatches, receiving, counts, discrepancies.
- **Scope:** company-wide operationally.
- **Financial visibility:** ⛔ **DENIED** at the server/DTO projection layer.
- **Audit visibility:** ⛔ **DENIED** — `/api/v1/audit` returns 403, and so does the Activity Monitor.
- **Subtlety (ADR-015):** an Admin **may trigger** operations whose server-side effects are financial (receiving reversal, stock-count approval). The server performs the cost recomputation and returns **no** cost field. Authority over an operation and visibility of its financial output are independent concerns.

#### Warehouse Staff (موظف المخزن)
- **Responsibilities:** receiving supplier goods, reviewing restaurant requests, fulfilling lines, dispatching.
- **Scope:** bound to assigned warehouses via `UserWarehouseScope`.
- **Financial visibility:** ⛔ DENIED. **Audit:** ⛔ DENIED.
- **Denied:** administrative, user-management modules; cannot open or approve a stock count (they only record counts).

#### Restaurant Supervisor (مشرف الفرع / المطعم)
- **Responsibilities:** create multi-item supply requests, confirm received goods, review receipt discrepancies, log consumption.
- **Scope:** bound to assigned restaurants via `UserRestaurantScope`.
- **Financial visibility:** ⛔ DENIED. **Audit:** ⛔ DENIED.
- **Denied:** warehouse stock balances, supplier setup, user management, and **any choice of serving warehouse** (§4.10).

#### User (مستخدم عام)
- **Responsibilities:** none by default.
- **Scope/permissions:** entirely by explicit grant.
- **Financial visibility:** ⛔ DENIED. **Audit:** ⛔ DENIED.

### 5.3 Non-grantable permissions (ADR-012)

These four codes exist in the catalogue to **document Owner capability** and can never be assigned to any user of any role. Attempting to grant one returns `400 NON_GRANTABLE_PERMISSION`, and the permission-assignment UI never lists them:

```
costs:view      valuation:view      audit:view      audit:export
```

A grantable permission that can never take effect is a security trap — an administrator would believe they had delegated something.

### 5.4 Role Denial overrides grants

```
Authorization request
      │
      ▼
Is Role == Admin AND action ∈ {costs:view, valuation:view, audit:view, audit:export}?
      ├── YES → 403 FORBIDDEN, regardless of any explicit user permission grant
      └── NO  → evaluate role permissions + user permissions + scope
```

Implement this as a **pipeline-level handler**, not a controller attribute — a new controller that forgets the attribute would silently bypass it.

### 5.5 Data scopes

- `UserWarehouseScope` — warehouse staff see only their warehouses.
- `UserRestaurantScope` — supervisors see only their branches.
- Company scope — universal, via EF Core global query filters **plus** composite tenant foreign keys.
- **Out of tenant → `404`. In tenant but out of scope → `403`.** The distinction is deliberate: a `404` must not confirm that a record exists in another tenant.

### 5.6 RETIRED ROLE — DO NOT REINTRODUCE

> ### ⛔ `Accountant` is permanently retired (ADR-005).
> It created permission ambiguity and overlapping responsibilities with `Owner` and `Admin`.
> It must not appear in role seed data, enums, permission matrices, UI, or documentation as a live role.
> If you find it referenced anywhere outside ADR-005 and the migration-mapping note in `docs/24`, that reference is stale.
> Legacy `Accountant` users map to `User` with explicit permissions configured by the Owner.

---

## 6. Authentication and Security

> **Status: fully specified, NOTHING implemented.** This is Phase 3 work. The section below is the contract to build against, not a description of existing code.

### 6.1 Authentication
- **Mobile number + password.** Not email — restaurant and warehouse workers operate by phone number.
- Mobile numbers normalised (non-numeric stripped, stored e.g. `01012345678`).
- **No self-service registration.** Users are provisioned by `Owner` or an authorised `Admin`.
- Inactive account → `403 ACCOUNT_INACTIVE` (`"هذا الحساب معطل، يرجى مراجعة إدارة النظام."`).
- Failed-attempt counter + lockout via `users.access_failed_count` / `users.lockout_end_at`.

### 6.2 Sessions and cookies
```
Set-Cookie: __Host-InventorySession=<token>; Path=/; Secure; HttpOnly; SameSite=Strict; Max-Age=28800
```
- **Tokens are NEVER stored in `localStorage` or `sessionStorage`** — that is the XSS token-theft vector this design exists to eliminate.
- Security-stamp validation on every request; rotation on password change or role/scope change immediately invalidates live cookies.

### 6.3 CSRF
- All mutating verbs require `X-CSRF-TOKEN`.
- Token fetched from `GET /api/v1/auth/csrf-token` at application boot and attached by the central API client.

### 6.4 CORS
Restricted to the configured frontend origin, credentials allowed, **no wildcard**.

### 6.5 OTP password reset
- Cryptographically random 6 digits, **hashed** into `password_reset_otps`, 5-minute expiry, single use.
- Dispatched via the `ISmsSender` abstraction. **The OTP is never returned in an HTTP response** — development writes it to the console log only.
- **Rate limiting (ADR-026): both windows apply simultaneously — 3 per mobile per 15 minutes AND 5 per mobile per hour**, plus the per-IP limit. Whichever trips first returns `429`.

### 6.6 Other rate limits
`/auth/login` 5/IP/min · sensitive mutations (`/confirm`, `/submit`) 30/user/min · general 300/user/min.

### 6.7 Tenant isolation
- Tenant identity comes **exclusively** from the authenticated session. Client-supplied `companyId` in body, query, route or header is ignored for every security decision.
- EF Core global query filters on all `ITenantScopedEntity` types.
- **Composite foreign keys on `(company_id, id)` throughout (ADR-016)** — this makes a cross-tenant reference *physically impossible* rather than merely filtered, and must be present in the **initial** migration. Retrofitting it onto populated tables means a full table rewrite.

### 6.8 IDOR protection
Every resource lookup checks tenant **and** scope:
```csharp
var supply = await _context.Supplies
    .FirstOrDefaultAsync(s => s.Id == supplyId && s.CompanyId == _currentTenant.Id);
if (supply is null) return NotFound();
if (!userAuthorizedRestaurantIds.Contains(supply.RestaurantId)) return Forbid();
```

### 6.9 Mass assignment
Endpoints bind to dedicated command DTOs. `companyId`, `userId`, `generatedCode`, `documentNumber`, `createdAt` and (on supply requests) `warehouseId` are **not declared as properties anywhere**, so an over-post is discarded by the model binder rather than relied upon to be ignored downstream.

### 6.10 Audit requirements
- Audit rows are written **inside the same database transaction** as the business change. A rolled-back operation leaves no audit trace of success.
- **Because of this, audit infrastructure is Phase 4 — before the first business mutation.** Retrofitting it later means reopening every handler, and any handler missed loses its trail silently.
- Sanitisation: passwords, OTPs, session tokens and connection strings never enter `old_values`/`new_values`.
- `audit_logs` is monthly range-partitioned, 7-year retention.

### 6.11 Secrets
Environment variables or a secret vault. **Zero plaintext secrets in git** — `.gitignore` already excludes `.env*`, `*.pfx`, `*.p12`, `appsettings.*.local.json`, `secrets.json`.

> ⚠️ `src/Inventory.Api/appsettings.Development.json` currently contains a **local-development-only** PostgreSQL connection string with default local credentials. It is committed deliberately as developer convenience for a database that does not exist yet. **Do not follow this pattern for any non-local environment**, and do not copy those values anywhere.

---

## 7. Inventory Model

### 7.1 Source of truth

Two layers, and the distinction matters:

| | Role |
| :--- | :--- |
| **`stock_ledger`** | **THE source of truth.** Append-only, immutable, every movement with actor, cost, reference and timestamps. |
| **`stock_balances`** | A **materialised projection** for fast querying and concurrency locking. Derivable from the ledger; never authoritative on its own. |

```
Current Stock = Opening
              + Σ INCOMING_POSTED
              ± Σ INCOMING_RECONCILIATION
              − Σ RESTAURANT_RECEIPT_CONFIRMED
              ± Σ PHYSICAL_ADJUSTMENT
```

### 7.2 Worked example — trace it end to end

```
Stage 1   Warehouse stock = 0 KG
Stage 2   Receiving posted: +500 KG @ 200 EGP   → 500 KG, WAC 200.0000
Stage 3   Physical verification finds 490 KG
          INCOMING_RECONCILIATION (−10)          → 490 KG     ← delta only, NOT +490
Stage 4   Restaurant requests 50 KG              → 490 KG (unchanged)
Stage 5   Warehouse fulfils 45 KG (Prepared)     → 490 KG (unchanged)
Stage 6   Warehouse dispatches (Dispatched)      → 490 KG (unchanged)  ← in-transit = 45
Stage 7   Restaurant confirms 43 KG received
          RESTAURANT_RECEIPT_CONFIRMED (−43)     → 447 KG
          Discrepancy logged: variance −2 KG
Stage 8   Restaurant logs 15 KG consumption      → 447 KG (unchanged)
Stage 9   Receiving #2 posted: +200 KG @ 220     → 647 KG
          New WAC = ((447×200)+(200×220)) / 647 = 206.1823
Stage 10  Physical count finds 640 KG (system 647, variance −7)
Stage 11  Owner approves → PHYSICAL_ADJUSTMENT (−7) → 640 KG, WAC unchanged
Stage 12  Valuation (Owner view) = 640 × 206.1823 = 131,956.67 EGP
Stage 13  Valuation (Admin view) = MASKED (null)
Stage 14  Restaurant stock balance = DOES NOT EXIST
```

### 7.3 Partial receipt matrix

| Scenario | Dispatched | Received | Stock effect | Resulting status |
| :--- | :---: | :---: | :---: | :--- |
| Full delivery | 20 | 20 | **−20** | `Confirmed` |
| Partial delivery | 20 | 18 | **−18** | `ConfirmedWithDiscrepancy`, variance −2 |
| Complete rejection | 20 | 0 | **0 — no ledger row at all** | `RejectedAtDelivery`, variance −20 |

Why zero receipt writes no ledger row: dispatch never deducted, so the balance already reflects the goods as present. When the shipment returns, reality and the ledger already agree.

### 7.4 Multi-item confirmation is all-or-nothing

A five-line confirmation where line 3 is short on stock rolls back **entirely**. Partial confirmation is never written.

### 7.5 The impossible confirmation (ADR-019 + `docs/30 §7.1`)

Dispatch **advises** on sufficiency (`available = quantity − inTransit`) but **never reserves** — a reservation would be a phantom balance, which ADR-003 forbids. So a warehouse balance can be depleted between dispatch and confirmation.

The only approved resolution path:
1. Confirmation fails `INSUFFICIENT_STOCK`; supply stays `Dispatched`; nothing written.
2. Supervisor raises a `StockUnavailableAtConfirmation` discrepancy.
3. Owner/Admin runs a physical stock count establishing the true balance.
4. Supervisor retries; it now succeeds.

**Never permitted:** negative balance, silently reducing the confirmed quantity to fit, or auto-posting an adjustment without approval.

---

## 8. Current Database Model

> ### ⛔ NOTHING IS IMPLEMENTED.
> There is **no** `DbContext`, **no** entity class, **no** EF configuration, **no** migration, and **no** `Migrations/` folder anywhere in the repository. Verified 2026-09-16 by directory inspection.
> PostgreSQL is **not installed** on the development machine.
> Everything below is the **specification to build in Phase 2**, summarised so you know what you are aiming at. Do not treat it as existing.

### 8.1 Authority order for schema work

**`docs/29-database-integrity-indexes-constraints.md` GOVERNS `docs/06-database-schema.md`.**
`docs/06` is the table inventory; `docs/29` corrects it. Generating a migration from `docs/06` alone would faithfully reproduce every defect the reconciliation pass found. `docs/06` carries a banner listing its own known defects.

### 8.2 Table groups

- **Identity/tenancy:** `companies`, `users`, `roles`, `user_roles`, `permissions`, `role_permissions`, `user_permissions`, `user_warehouse_scopes`, `user_restaurant_scopes`
- **Master data:** `categories`, `units`, `suppliers`, `warehouses`, `restaurants`, `items`, `item_unit_conversions`
- **Inventory:** `stock_balances`, `stock_ledger`
- **Documents:** `receiving_orders` + `receiving_order_items`, `supply_requests` + `supply_request_items`, `supplies` + `supply_items`, `stock_counts` + `stock_count_items`, `discrepancies`, `consumption_records`
- **Cross-cutting:** `audit_logs`, `idempotency_records`, `document_sequences`, `password_reset_otps`, `company_settings`
- **Specified but NOT migrated in Phase 2:** `stored_files`, `file_attachments` (files are deferred — ADR-029)

### 8.3 Corrections in `docs/29` that MUST land in the initial migration

| ID | Correction | Why it cannot wait |
| :--- | :--- | :--- |
| DB-01 | **Remove `stock_balances.version BYTEA`; use the PostgreSQL `xmin` system column** via `UseXminAsConcurrencyToken()` | `BYTEA` is a SQL Server idiom. PostgreSQL has no auto-maintained rowversion, so it would **silently fail to detect lost updates** — a concurrency bug that passes every single-threaded test. |
| DB-02/03 | `UNIQUE (company_id, id)` on every tenant table; **every tenant FK composite** `(company_id, fk)` | Retrofitting onto populated tables = full table rewrite. Highest-consequence risk in the plan. |
| DB-05 | `UNIQUE (user_id)` on `user_roles` | One role per user (ADR-014). |
| DB-06 | `discrepancies.document_number` + unique; `discrepancies.reference_line_id` | `DSC-` numbering and line-level attribution. |
| DB-07 | `supply_items.supply_request_item_id` | Without it, request→dispatch→receipt traceability is impossible. |
| DB-08 | Widen `supplies.status`; add `prepared_by`, `prepared_at` | ADR-017 canonical status set. |
| DB-17 | `restaurants.default_serving_warehouse_id UUID NOT NULL` + composite tenant FK | ADR-028. **No `restaurant_serving_warehouses` join table** — v1.0 is one-to-one. |
| §4.3 | `uq_ledger_posting UNIQUE (company_id, reference_type, reference_id, item_id, movement_type)` | Prevents double-posting a document. |
| §4.3 | One line per item per document, on all four line tables | Duplicate-item handling. |
| §4.4 | Append-only **triggers** on `stock_ledger` + `audit_logs`, and revoked grants | Use the raising trigger, **not** `DO INSTEAD NOTHING` — a silent no-op hides the defect. |
| §4.5 | `audit_logs PARTITION BY RANGE (created_at)`, monthly | 7-year retention. |
| §5 | **The entire index plan** — `docs/06` defines none at all | P95 budgets of 150–300 ms and keyset pagination depend on it. |

### 8.4 Document numbering (`docs/28`)

`document_sequences (company_id, document_type, period_key, last_value, updated_at)`, PK on the first three. Allocation inside the business transaction:

```sql
INSERT INTO document_sequences (company_id, document_type, period_key, last_value, updated_at)
VALUES (@companyId, @documentType, @periodKey, 1, NOW())
ON CONFLICT (company_id, document_type, period_key)
DO UPDATE SET last_value = document_sequences.last_value + 1, updated_at = NOW()
RETURNING last_value;
```

Formats: `ITM-{000000}` (lifetime) · `REC-`/`REQ-`/`SUP-`/`CNT-`/`DSC-{YYYYMM}-{0000}` (monthly). Period derives from the **business date in the tenant timezone** (default `Africa/Cairo`), not UTC `NOW()` — otherwise late-evening Cairo documents land in the following month.

### 8.5 Unit conversions

`item_unit_conversions (company_id, item_id, from_unit_id, to_base_unit_id, conversion_factor NUMERIC(18,6), is_active)`. `to_base_unit_id` must equal `items.base_unit_id`, enforced by composite FK plus server validation. Immutable once used.

---

## 9. API Contract

> ### ⛔ ZERO BUSINESS ENDPOINTS ARE IMPLEMENTED.

### 9.1 Implemented — verified in `src/Inventory.Api/Program.cs`

| Endpoint | Auth | Behaviour |
| :--- | :--- | :--- |
| `GET /health` | none | Liveness. Returns literal `Healthy`. Runs no checks. |
| `GET /health/ready` | none | Readiness. Filters health checks on tag `ready` → the Npgsql probe. Returns `Healthy`/`Unhealthy`/`Degraded` with 200/503. |
| `GET /openapi/v1.json` | none | **Development only.** OpenAPI document. No Swagger UI. |

Both health probes deliberately disclose nothing beyond status — no version, environment name, host, or exception text. `HealthEndpointTests` asserts that absence.

### 9.2 Cross-cutting behaviour that IS wired

- **Correlation:** every response carries `X-Correlation-Id`; a valid inbound value is echoed, a hostile one replaced.
- **RFC 7807:** unhandled exceptions → `application/problem+json` with `code`, `messageAr`, `correlationId`; `messageEn` in Development only. Currently only the generic `UNEXPECTED_ERROR` (500) exists — business codes arrive with the phases that introduce their rules.

### 9.3 Specified but NOT implemented — `docs/07-api-specification.md`

Base route `/api/v1`. Every group below is **planned only**:

`/auth` (login, logout, csrf-token, forgot-password otp/verify/reset) · `/account/me` · `/items` · `/categories` · `/units` · `/suppliers` · `/warehouses` · `/restaurants` · `/items/{id}/unit-conversions` · `/receiving-orders` (+ submit, verify, reverse) · `/supply-requests` (+ items CRUD, submit, cancel, fulfill) · `/supplies` (+ dispatch, confirm, cancel) · `/stock-counts` (+ record, approve, reject) · `/discrepancies` (+ resolve) · `/consumption` · `/users` (+ scope, permissions) · `/audit` (+ activity)

**Not in the MVP surface at all (ADR-029):** any `/reports/*` endpoint, any file-upload endpoint. `SEC-009` asserts they return 404 and are never stubbed.

### 9.4 Rules every future endpoint must honour

- **Server-side identification:** bodies never accept `companyId`, `actorUserId`, `createdAt`, `documentNumber`, `generatedCode`, or `warehouseId` (on supply requests).
- **Financial projection:** cost fields returned **only** to `Owner` — masked in the LINQ `.Select()`, not the UI.
- **Idempotency (`docs/30 §6.1`) — REQUIRED on:** `/supplies/{id}/confirm`, `/receiving-orders/{id}/submit`, `/verify`, `/reverse`, `/stock-counts/{id}/approve`. Missing → `400 IDEMPOTENCY_KEY_REQUIRED`. Same key + different payload → `409 IDEMPOTENCY_KEY_REUSE`. The cached payload **must be the role-projected DTO**, never the domain object, or an Owner's cached response could be replayed to a non-Owner.
- **CSRF** on every mutating verb.
- **Scope:** out-of-tenant `404`; in-tenant out-of-scope `403`.
- **Errors:** RFC 7807 from the `docs/13` Arabic catalogue.

### 9.5 Error catalogue (`docs/13`)

`INVALID_CREDENTIALS` 401 · `ACCOUNT_INACTIVE` 403 · `INSUFFICIENT_STOCK` 400 · `FORBIDDEN_SCOPE` 403 · `FINANCIAL_ACCESS_DENIED` 403 · `DUPLICATE_ITEM_NAME` 409 · `INVALID_QUANTITY` 400 · `INVALID_STATE_TRANSITION` 400 · `ALREADY_CONFIRMED` 409 · `INVALID_CONVERSION_FACTOR` 400 · `OTP_EXPIRED` 400 · `OTP_INVALID` 400 · `RATE_LIMIT_EXCEEDED` 429 · `CONCURRENCY_CONFLICT` 409 · `SERVING_WAREHOUSE_UNAVAILABLE` 409 · `GENERATED_FIELD_NOT_ACCEPTED` 400 · `NON_GRANTABLE_PERMISSION` 400 · `IDEMPOTENCY_KEY_REQUIRED` 400 · `IDEMPOTENCY_KEY_REUSE` 409 · `CONVERSION_NOT_DEFINED` 409 · `SEQUENCE_EXHAUSTED` 409 · `EMPTY_DOCUMENT` 400

---

## 10. Frontend

### 10.1 What exists — verified, and it is very little

```
frontend/src/app/layout.tsx     <html lang="ar" dir="rtl">, Arabic metadata
frontend/src/app/page.tsx       one static Arabic card, NO data, NO API call
frontend/src/app/globals.css    Tailwind import, RTL base, .font-mono-code bidi isolation
```

**That is the entire UI.** There is no component library, no API client, no navigation, no dashboard, no form. `src/lib/` and `src/components/` **do not exist yet**.

### 10.2 Verified green (`build-verification.log`, 2026-09-11 08:13)

`npm ci` (498 packages) · `npm run typecheck` · `npm run lint` · `npm run test` (3/3) · `npm run build` (Next 16.3.4 Turbopack, compiled 7.5s, 3 static routes)

### 10.3 Configuration in place

| File | Purpose |
| :--- | :--- |
| `eslint.config.mjs` | flat config + **RTL guard**: `no-restricted-syntax` bans `pl-/pr-/ml-/mr-` and `text-left/right`, **scoped to `src/**`** (unscoped it matched its own message strings) |
| `vitest.config.mts` | jsdom, `include: __tests__/**`, **excludes `e2e/`** |
| `playwright.config.ts` | `testDir: ./e2e`, `locale: ar-EG`, `timezoneId: Africa/Cairo`, webServer on 3000 |
| `next.config.ts` | `reactStrictMode`, `NEXT_PUBLIC_API_BASE_URL` default `http://localhost:5165` |
| `tsconfig.json` | strict + `noUncheckedIndexedAccess`; `jsx: react-jsx` and `.next/dev/types` already applied by Next |

### 10.4 Arabic / RTL requirements — `docs/31` is authoritative

- **No English string ever reaches a business user.** Server returns `messageAr`; the client renders only that.
- `<html dir="rtl" lang="ar">`, sidebar on the **right**.
- **Logical CSS properties only** — `ps-`, `pe-`, `ms-`, `me-`, `text-start`, `text-end`. Lint-enforced.
- **Bidi isolation** on every Latin run: `<span dir="ltr" class="font-mono-code">ITM-000142</span>`. Without it that renders as `142-000-ITM` — the most common Arabic-UI defect.
- **Western digits (0–9)**, `,` thousands, `.` decimal. Warehouse staff read scales and supplier invoices in Western digits.
- Dates Gregorian `DD/MM/YYYY`; date-times rendered in tenant timezone, stored UTC.
- **All formatting through one module, `src/lib/formatters.ts`** (Phase F1). Ad-hoc `toLocaleString` in components is forbidden.
- Status enums are English server-side; the UI renders only the Arabic mapping in `docs/31 §4.6`.
- Arabic sorting uses the `ar` collator / `COLLATE "ar-x-icu"`, never byte order.
- Arabic search normalises (strip diacritics/tatweel, unify alef forms) into a `name_normalized` column with a `pg_trgm` index.
- Strip `U+202E` from all input.
- CSV exports need a **UTF-8 BOM** (Excel on Windows) and PDFs need a shaping-capable engine — naive PDF libraries emit disconnected reversed Arabic. Verify visually, not by absence of error.

### 10.5 Design system — MANDATORY prerequisite

`ui-ux-pro-max/SKILL.md` is present in this repository. **Read it before authoring any component** — with its `data/stacks/nextjs.csv`, `shadcn.csv`, `colors.csv`, `typography.csv`, `ux-guidelines.csv`. The fallback protocol in the UI/UX guide §1.3 does **not** apply, because the skill is present. Do not use a generic AI admin-dashboard template — explicitly forbidden.

Aesthetic: Swiss enterprise minimalism, high data density, `Noto Sans Arabic` + `Plus Jakarta Sans`.

### 10.6 Every screen still to build (F1–F6)

App shell · login/OTP · items + quick-add modal · master data · suppliers · locations · users/scopes · receiving (draft/post/verify/reverse) · warehouse stock (balance / in-transit / available) · **multi-item supply request editor** · fulfilment queue · dispatch · **receipt confirmation** · discrepancies · consumption · stock counts · role dashboards. **No `/reports` route is ever registered** (ADR-029).

### 10.7 Frontend patterns that are non-negotiable

- **Five API states on every screen:** loading skeleton · success · empty · error banner with retry · mutation lock.
- **Zero fake data.** No hardcoded `0`, mock array, placeholder chart, sample row, or "coming soon" screen. A number on screen is always a number the server returned. A deferred feature renders *nothing*.
- **Three distinguishable unauthorized states:** 401 → redirect to login; 403 → Arabic forbidden message with a back action (never a blank screen); out-of-scope → Arabic "outside your scope".
- `/audit` route is **not registered** for non-Owners — a direct URL yields the standard not-found page rather than hinting the feature exists.
- Idempotency key generated **once per form instance**, never regenerated on retry — that is the entire point of it.

---

## 11. Completed Work

Every row below is verified from the repository or from `build-verification.log`.

### 11.1 Documentation — complete and reconciled (Phase 0)

45 files in `docs/`. 31 ADRs. 99 conflict-register entries. 55 traceability requirements (REQ-01 … REQ-55). All four blocking product decisions closed (OD-008/009/010/011). Zero business invariants weakened.

### 11.2 Solution skeleton — exists, partially building

| Item | Evidence |
| :--- | :--- |
| `InventorySystem.sln` with 7 projects | file present, restore succeeded for all 7 |
| Layer wiring per ADR-002 | `.csproj` `ProjectReference` sets |
| `Directory.Build.props` strictness | file present; it is what is failing the build |
| `Directory.Packages.props` CPM | file present; restore succeeded → **all package versions resolve, including `Serilog.AspNetCore` 9.0.0** |
| `.editorconfig`, `.gitignore` | present; `.gitignore` covers secrets, `node_modules`, `bin/obj`, both verification logs |
| `global.json` | SDK `10.0.302`, `rollForward: latestPatch` |
| Domain / Application / Infrastructure compile | `build-verification.log` — three `-> ...dll` lines |

### 11.3 Code authored (compiles only where noted)

| File | Contents | Compiles? |
| :--- | :--- | :---: |
| `src/Inventory.Infrastructure/DependencyInjection.cs` | `AddInfrastructure`, health-check registration, constants `DatabaseReadinessCheckName`/`ReadinessTag`/`DatabaseConnectionName` | ✅ |
| `src/Inventory.Infrastructure/HealthChecks/PostgreSqlReadinessHealthCheck.cs` | raw Npgsql `SELECT 1`, generic failure description | ✅ |
| `src/Inventory.Api/Program.cs` | Serilog, correlation, exception handler, health probes, OpenAPI, `public partial class Program` for tests | ❌ CS8604 |
| `src/Inventory.Api/Middleware/CorrelationIdMiddleware.cs` | validate-or-replace correlation id | ✅ |
| `src/Inventory.Api/Middleware/GlobalExceptionHandler.cs` | RFC 7807 + Arabic | ❌ CA1848 |
| 3 assembly markers | anchors for architecture tests | ✅ |

### 11.4 Tests authored (not yet executed — build gate)

- `LayeringRules.cs` — 5 forbidden technology prefixes on Domain; Domain references no solution project; Application must not reference Infrastructure/Api; Infrastructure must not reference Api.
- `CompositionRootRules.cs` — only `Program.cs` may name `Inventory.Infrastructure`.
- `ForbiddenPatternRules.cs` — no generic repository; no `float`/`double` in Domain (fields, properties, return types).
- `StartupScriptRules.cs` — no `DROP DATABASE`/`DROP SCHEMA`/`EnsureDeleted`/`dropdb` in any root `.bat`, REM lines excluded.
- `HealthEndpointTests.cs` — 6 tests via `WebApplicationFactory<Program>`.
- `DomainLayerContractTests.cs` — 2 tests. ❌ CA1707.

### 11.5 Frontend — complete for Phase 1 and VERIFIED

Scaffold, RTL foundations, lint guard, 3 Vitest tests, 1 Playwright spec, production build green.

### 11.6 Local scripts

`run-local.bat` (8-step launcher that **fails loudly** if PostgreSQL is absent and names ADR-027), `check-local.bat` (layered read-only diagnostic), `stop-local.bat` (kills only its own window titles, never a blanket `taskkill dotnet.exe`), `verify-env.bat` v2, `build-verify.bat`.

### 11.7 NOT completed despite existing as files

- **`ci-workflow.yml` is at the repository root, not at `.github/workflows/ci.yml`.** It is inert until moved.
- **The backend build.** See §12.2.

---

## 12. Current Test State

### 12.1 Test inventory

| Project | File | Tests | Last result |
| :--- | :--- | :---: | :--- |
| `Inventory.UnitTests` | `DomainLayerContractTests.cs` | 2 | **never ran — compile error** |
| `Inventory.IntegrationTests` | `HealthEndpointTests.cs` | 6 | **never ran — build gate** |
| `Inventory.ArchitectureTests` | 4 rule classes | ~9 | **never ran — build gate** |
| `frontend/__tests__` | `rtl-foundations.test.tsx` | 3 | ✅ **3 passed** (2026-09-11 08:13) |
| `frontend/e2e` | `shell.spec.ts` | 2 | **never run** — needs a running server; scheduled Phase T2 |

**No .NET test has ever executed in this repository.** Do not assume any architecture rule actually works — `docs/09` Phase 1 requires each to be proven by a temporary deliberate violation, and that has not been done.

### 12.2 The build failure — verbatim from `build-verification.log`

Run 2026-09-11 08:12 on `KOSHARY`. `dotnet restore` ✅ (all 7 projects). `dotnet build ... -warnaserror` ❌ **4 errors**:

```
tests/Inventory.UnitTests/DomainLayerContractTests.cs(22,17): error CA1707:
    Remove the underscores from member name Domain_Assembly_Is_Resolvable()

tests/Inventory.UnitTests/DomainLayerContractTests.cs(34,17): error CA1707:
    Remove the underscores from member name Domain_Marker_Belongs_To_The_Domain_Assembly()

src/Inventory.Api/Program.cs(60,46): error CS8604:
    Possible null reference argument for parameter 'value' in
    'void IDiagnosticContext.Set(string propertyName, object value, bool destructureObjects = false)'

src/Inventory.Api/Middleware/GlobalExceptionHandler.cs(62,9): error CA1848:
    For improved performance, use the LoggerMessage delegates instead of calling LogError(...)
```

`dotnet test` then failed as a **cascade**, not independently: `build-verify.bat` passes `--no-build`, so the test DLLs did not exist (`The test source file ... was not found`, `MSB4181`). Fixing the build is expected to clear it.

**Root cause, one sentence:** `Directory.Build.props` combines `TreatWarningsAsErrors=true` with `AnalysisLevel=latest-recommended`, and nothing exempts test projects from naming analyzers or exempts the two code sites from nullable/logging analyzers.

The exact offending lines, verified:
- `Program.cs:60` — `diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);` — `PathString.Value` is `string?`.
- `GlobalExceptionHandler.cs:62` — `_logger.LogError(exception, "...", ...)`.

### 12.3 Frontend — last verified green

```
npm ci        → added 498 packages in 22s
npm run typecheck → clean
npm run lint      → clean
npm run test      → 1 file, 3 tests passed
npm run build     → Next.js 16.3.4, compiled in 7.5s, 3 static routes
```

### 12.4 Environment verification is INCOMPLETE

`env-verification.log` is **Run 1 only** (2026-09-11 03:12) and aborted partway on a defect in the v1 script (`npm.cmd` invoked without `call` destroyed the subroutine). `verify-env.bat` **v2 is on disk and has never been run**.

Confirmed by executed command: .NET SDK `10.0.302` (the **only** registered SDK) · runtimes ASP.NET Core + .NET `10.0.10` (also `8.0.29`, plus WindowsDesktop) · `dotnet ef` `10.0.3` · Node `v22.20.0` · `dotnet` and `node` resolve on `PATH`.

Still unconfirmed by direct command: `npm --version` (though `npm ci` succeeded in `build-verification.log`, so it evidently works), `git --version`, `psql --version`, `docker --version`, and all six `where` checks.

### 12.5 PostgreSQL and Docker

**Neither is installed.** Last checked 2026-09-11 across `C:\Program Files`, `C:\Program Files (x86)`, `D:\Program Files` and both drive roots. SQL Server, MySQL (standalone + XAMPP) and Oracle are present and are **explicitly rejected as substitutes** (ADR-027). Nothing in the repository indicates this changed.

---

## 13. Current Implementation Position

### 13.1 Where development stopped

| | |
| :--- | :--- |
| **Last fully complete phase** | **Phase 0 — Documentation reconciliation.** Signed off. |
| **Current phase** | **Phase 1 — Solution architecture & infrastructure. AUTHORED, NOT COMPLETE.** |
| **Stopped at** | Running `build-verify.bat` for the first time on 2026-09-11 08:12. It failed with 4 errors. **Nothing has been done since.** |
| **Next logical task** | Fix those 4 errors and re-run. See §21. |

### 13.2 Phase 1 task-level state

| Task | State |
| :--- | :--- |
| 1.1 Execute environment verification | 🔄 **PARTIAL** — Run 1 aborted; v2 never run |
| 1.1a Pin SDK in `global.json` | ✅ done and confirmed by executed command |
| 1.1b PATH resolution | 🔄 `dotnet`/`node` confirmed; `git`/`psql`/`docker` pending |
| 1.1c Container runtime check | ⚠️ Docker absent from disk; command pending |
| 1.2–1.6 Solution, projects, wiring, props, test projects | ✅ authored; restore proves the graph is valid |
| 1.7–1.10 Architecture tests | ⚠️ authored, **never executed** |
| 1.11 Serilog | ⚠️ authored, does not compile |
| 1.12 Exception handler | ⚠️ authored, does not compile |
| 1.13 Health probes | ⚠️ authored, never executed |
| 1.14 OpenAPI | ⚠️ authored, never executed |
| 1.15 Frontend scaffold | ✅ **done and verified** |
| 1.16 Local scripts + guard test | ✅ authored; guard test never executed |
| 1.17 CI pipeline | ⚠️ written but **NOT INSTALLED** — still `ci-workflow.yml` at the root |

### 13.3 Work partially completed — pick these up, do not restart them

1. The four build errors (§12.2) — the code is 95% there.
2. `verify-env.bat` Run 2.
3. Moving `ci-workflow.yml` into `.github/workflows/ci.yml`.
4. Proving each architecture test by temporary deliberate violation.

### 13.4 Checkpoint status

Checkpoint 1 (Architecture + Database) covers Phases 1–2. **Neither half is signed off.** No later checkpoint is reachable.

---

## 14. Remaining Work

### 14.1 Required next — in order

1. **Close Phase 1** (§21): fix 4 build errors → `build-verify.bat` green → Run 2 of `verify-env.bat` → install CI workflow → prove architecture tests → mark Phase 1 complete in `docs/27`.
2. **Install PostgreSQL 16+** — hard prerequisite for Phase 2, and the only thing blocking it. Decide the integration-test host at the same time (Testcontainers needs Docker, which is absent; the alternative is a dedicated local test database).
3. **Phase 2** — schema, tenancy, initial migration, per `docs/29` (not `docs/06` alone).
4. **Phase 3** — authentication, sessions, CSRF, OTP, rate limiting.
5. **Phase 4** — authorization, scopes, role denial, **audit infrastructure** (early by design — see §17.4).
6. **Phases 5 → 14** — master data → conversions → stock engine → receiving → supply requests → fulfilment/dispatch → confirmation → discrepancies → counts → audit viewer.
7. **F1 → F6** — frontend, each gated on its backend phase. F1 may start in parallel now.
8. **S1, T1, T2, R1** — security hardening, full test sweep, browser E2E, release readiness.

### 14.2 Deferred (specified, NOT scheduled — ADR-029)

- **Phase 15 — File storage.** Activates only if an already-approved workflow explicitly requires an attachment. None currently does. `stored_files`/`file_attachments` are specified but **not migrated**.
- **Phase 16 — Reporting.** `docs/17 §4` extension points remain **binding on the core phases** — ledger metadata, audit context, line-level links and indexes must be built now so reports are possible later. Data not captured at posting time is unrecoverable.

### 14.3 Optional / future (version increment required)

Recipe management & BOM · FIFO/LIFO costing · SignalR real-time push · offline PWA sync · multi-currency · multiple serving warehouses per restaurant · stock reservation at dispatch.

---

## 15. Removed / Rejected Requirements — ⛔ DO NOT IMPLEMENT

> This section exists **specifically** to stop you reviving something you found in an older document. If you find any of these in `docs/`, the document is stale — check `docs/decision-log.md` and `docs/32` before acting.

| # | Do NOT | Authority |
| :--- | :--- | :--- |
| 1 | Create **any** restaurant stock / inventory / on-hand / in-transit table or balance | ADR-003, `docs/02 §1` |
| 2 | Deduct stock at **dispatch** | ADR-004 |
| 3 | **Reserve** stock at dispatch (reservation = phantom balance) | ADR-019 |
| 4 | Deduct stock on **consumption** records | `docs/02 §3.D` |
| 5 | Reintroduce the **`Accountant`** role | ADR-005 |
| 6 | Allow a user to hold **more than one** product role | ADR-014 |
| 7 | Add `barcode` to items | ADR-007 |
| 8 | Add `name_english` / `nameEn` to items, categories, units | ADR-007 |
| 9 | Add `reorder_points`, `max_stock`, `waste_records` tables | `docs/24 §1` |
| 10 | Use **Supabase** or any BaaS | ADR-001 |
| 11 | Substitute **SQL Server / MySQL / MariaDB / Oracle / SQLite** for PostgreSQL | ADR-027 |
| 12 | Add a `version BYTEA` rowversion column | ADR-022 |
| 13 | Use single-column tenant foreign keys | ADR-016 |
| 14 | Build a generic `IGenericRepository<T>` | `docs/25 §1` |
| 15 | Use `float`/`double` for any quantity, factor or cost | `docs/25 §2.1` |
| 16 | Generate identifiers with `SELECT MAX(...) + 1`, client-side, or a PostgreSQL `SEQUENCE` | ADR-025 |
| 17 | Accept a client-supplied `generatedCode` / `documentNumber` | ADR-008 |
| 18 | Declare `warehouseId` on the supply-request DTO — even hidden, even "validated" | ADR-028 |
| 19 | Create a `restaurant_serving_warehouses` join table | ADR-028 |
| 20 | Fall back to "any warehouse" when a serving warehouse is inactive | ADR-028 SW-5 |
| 21 | Use `Discrepancy` as a `Supply` **status** | ADR-017 |
| 22 | Collapse fulfilment and dispatch into one act | ADR-017 |
| 23 | Persist an in-transit **balance** | ADR-018 |
| 24 | Let WAC change on an **outbound** movement | ADR-020 |
| 25 | Permit negative stock, even transiently | ADR-021 |
| 26 | Edit an item's base unit or a used conversion factor in place | ADR-023 |
| 27 | Make `costs:view`, `valuation:view`, `audit:view`, `audit:export` grantable | ADR-012 |
| 28 | Give `Admin` any financial or audit visibility, or an "unless granted" path | ADR-006, ADR-013 |
| 29 | Hide financial data in the UI while sending it in the JSON | `docs/15`, `docs/25` |
| 30 | Build a generic file-management subsystem | ADR-029 |
| 31 | Build reports, `/reports` routes, placeholder charts, mock KPIs or "coming soon" screens | ADR-029 |
| 32 | Trim ledger metadata because "reports are deferred" | `docs/17 §4.1` |
| 33 | Build a `tests/Inventory.E2E/` .NET Playwright project | ADR-031 |
| 34 | Adopt FluentAssertions v8+ without a licence decision | CR-097 / OD-012 |
| 35 | Put `DROP DATABASE` / `EnsureDeleted` in any startup script | `docs/19`, `docs/25` |
| 36 | Store auth tokens in `localStorage`/`sessionStorage` | ADR-010 |
| 37 | Ship hardcoded zeros, mock arrays or sample rows on any surface | `docs/12 §2.3` |
| 38 | Build POS, HR, payroll, loyalty, recipes/BOM | `docs/01 §4` |

---

## 16. Deferred Decisions — DO NOT SILENTLY DECIDE THESE

### OD-007 — Documentation numbering
**Open, non-blocking.** The brief named `docs/08-frontend-ui-ux-implementation-guide.md` and `docs/09-implementation-plan.md`, but `08`/`09` were already the auth and authorization specs, and `04` is used by two documents (disambiguated by `SPEC-04` vs `SPEC-FLOW-04`). Current handling: existing numbers preserved; alias stubs published at the requested paths containing no specification content; `09-implementation-plan.md` is genuinely new and holds that path for real, with `22-implementation-plan.md` demoted to a pointer. **Recommendation: leave it.** Do not renumber unilaterally — it would break every cross-reference in `README`, `26` and the ADRs.

### OD-012 — Assertion library licensing
**Open. Due before Phase 7** (first domain tests). Does not block Phases 1–6.
`docs/18` mandates FluentAssertions, which is **commercially licensed from v8** (free only for open-source/non-commercial). This is a commercial product, so v8+ needs paid per-seat licences. **Version 8.10.0 is already in the machine's NuGet cache**, so it would be adopted by default if nobody looked.
Options: pin FluentAssertions **7.x** (last Apache-2.0) · buy v8+ licences · switch to **Shouldly** (BSD) or **xUnit built-in assertions**.
**Interim handling — no commitment made:** Phase 1 uses xUnit's built-in assertions only. `Directory.Packages.props` deliberately contains **no** FluentAssertions entry, with a comment explaining why. **Do not add one without an explicit decision from the project owner.**

### Integration-test host — undecided, due at Phase 2
`docs/18` assumes PostgreSQL Testcontainers, which needs Docker. **Docker is not installed.** Either install it, or target a dedicated local PostgreSQL test database. Record whichever is chosen.

---

## 17. Known Inconsistencies / Technical Debt

### 17.1 Authority order when sources disagree

```
1. docs/decision-log.md (ADR-001 … ADR-031)      ← highest; newest ADR wins
2. docs/01, docs/02                              product invariants
3. docs/04-end-to-end-business-flow.md           MASTER business flow
4. docs/04-inventory-stock-model.md, docs/03     stock model, roles
5. docs/06 AS AMENDED BY docs/29                 database
6. docs/07 + docs/28 + docs/30                   API, numbering, concurrency
7. docs/08, 09, 14, 15, 16                       security
8. frontend-ui-ux guide, 10, 11, 12, 31          frontend
9. docs/18, docs/23                              testing
10. docs/09-implementation-plan.md               plan
11. docs/27-implementation-status.md             status
12. Pre-existing code                            UNTRUSTED — never overrides a written spec
```

### 17.2 Live mismatches — implementation vs documentation

| # | Mismatch | Which wins | Action |
| :--- | :--- | :--- | :--- |
| 1 | `docs/27` Phase 1 register marks tasks 1.11–1.14 "DONE"; **the code does not compile** | **The build log wins.** | `docs/27` must be corrected when Phase 1 actually closes. Treat "authored" ≠ "done". |
| 2 | `docs/33 §4` lists Node/git/docker rows as ⚠️ pending; `build-verification.log` proves npm works | Both true — npm proven *indirectly*. | Run `verify-env.bat` v2 and record §7.4. |
| 3 | `docs/09` task 1.17 says CI configured; **`.github/` does not exist** | **Filesystem wins.** | Move `ci-workflow.yml`. |
| 4 | `docs/18 §1` (original text) lists `tests/Inventory.E2E/` | **ADR-031 wins** — E2E is `frontend/e2e/`. Already corrected in `docs/18`, with the old line struck through. | None — do not "restore" it. |
| 5 | `docs/06` DDL still shows `version BYTEA` and the old `supplies.status` comment | **`docs/29` wins.** Both sites carry inline ⚠️ SUPERSEDED comments. | Follow `docs/29` when writing the migration. |
| 6 | `docs/05` and `system-master-flow.md` omit the `Prepared` supply state | **`docs/04 §18.2` + ADR-017 win.** Both files carry authority banners. | None. |
| 7 | `docs/04-inventory-stock-model.md §3` lists `Version (byte[])` | **ADR-022 wins.** Struck through in place. | None. |
| 8 | `docs/22-implementation-plan.md` | **Superseded pointer.** `docs/09-implementation-plan.md` is authoritative. | Ignore `22`. |

### 17.3 Technical debt introduced in Phase 1

1. **Analyzer configuration is too strict for test projects.** CA1707 flags the `Method_Does_Thing` naming convention that xUnit uses universally. Needs a scoped exemption, not a rename.
2. **`docs/06` is a landmine** — its DDL is copy-pasteable and partly wrong. It carries warning banners, but an agent in a hurry could still lift it.
3. **Architecture tests are unproven.** They may pass vacuously. Each must be proven by a temporary deliberate violation.
4. **No `CLAUDE.md` exists** at the repository root. A future session gets no automatic project context beyond this handoff.
5. **`frontend/tsconfig.tsbuildinfo`** is on disk; `.gitignore` covers `*.tsbuildinfo`, so it is untracked. No action.
6. **`env-verification.log` and `build-verification.log` are gitignored** — they are evidence, not source, and are regenerated on demand.

### 17.4 Deliberate plan departures — do NOT "correct" these

`docs/09 §4` justifies four reorderings against the original outline. They are intentional:
1. **Audit infrastructure moved to Phase 4** (from ~13) — audit rows are written inside the same transaction as their business change, so it must exist before the first mutation.
2. **Transaction/concurrency/idempotency moved into Phase 7** — the first stock writer needs them.
3. **CSRF / rate limiting / tenant isolation moved into their owning phases** — a control retrofitted into a working system is a control nobody trusts.
4. **Frontend design system parallel; frontend workflows strictly gated** on their backend phase. **There is no mock-API track** — building a screen against an imagined API is the drift this plan exists to prevent.

---

## 18. Local Development

### 18.1 Prerequisites

| Tool | Required | Installed? |
| :--- | :--- | :--- |
| .NET SDK | 10 | ✅ `10.0.302` |
| .NET + ASP.NET Core runtime | 10 | ✅ `10.0.10` |
| `dotnet-ef` | matching | ✅ `10.0.3` |
| Node.js | ≥ 20.9 | ✅ `v22.20.0` at `D:\Nodejs` (non-default path, but on `PATH`) |
| npm | bundled | ✅ (proven by `npm ci`) |
| **PostgreSQL** | **16+** | ❌ **NOT INSTALLED — blocks Phase 2** |
| Docker | optional | ❌ not installed — affects only the integration-test host |

### 18.2 Verification (run these first, they are cheap)

```bat
verify-env.bat        :: toolchain + PATH  → env-verification.log   [Run 2 still owed]
build-verify.bat      :: build + tests + frontend → build-verification.log
check-local.bat       :: layered read-only diagnostic
```

`build-verify.bat` runs, in order: `dotnet restore` → `dotnet build -warnaserror --no-restore` → `dotnet test --no-build` → `npm ci` → `npm run typecheck` → `npm run lint` → `npm run test` → `npm run build`, writing every command's literal output to `build-verification.log` and a final `RESULT:` line.

### 18.3 Individual commands — `docs/09 §8`, use these, do not invent others

```bat
dotnet build InventorySystem.sln -warnaserror
dotnet test InventorySystem.sln
dotnet test tests/Inventory.UnitTests
dotnet test tests/Inventory.IntegrationTests
dotnet test tests/Inventory.ArchitectureTests

dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api
dotnet ef migrations has-pending-model-changes --project src/Inventory.Infrastructure --startup-project src/Inventory.Api
dotnet ef migrations script --idempotent --project src/Inventory.Infrastructure --startup-project src/Inventory.Api

npm run typecheck --prefix frontend
npm run lint      --prefix frontend
npm run test      --prefix frontend
npm run build     --prefix frontend
npm run test:e2e  --prefix frontend
```

### 18.4 Running the stack

```bat
run-local.bat     :: prerequisites → PostgreSQL check → migrations → API → /health → frontend → browser
stop-local.bat    :: stops only Inventory.Api / Inventory.Web windows; never a blanket taskkill
```

`run-local.bat` **aborts with an explicit message** if PostgreSQL is not listening on 5432, naming ADR-027 and refusing to substitute another engine. That is by design. Today it will always abort at step 2.

### 18.5 Ports and health

API `http://localhost:5165` · Frontend `http://localhost:3000` · PostgreSQL `5432`
`GET /health` → `Healthy` · `GET /health/ready` → `Unhealthy`/503 until PostgreSQL exists (correct behaviour) · `GET /openapi/v1.json` (Development only)

### 18.6 Migrations

**None exist.** The first is Phase 2 and must be authored from **`docs/29` as amended over `docs/06`**, reviewed as generated SQL before commit, and forward-only after Checkpoint 1.

---

## 19. Important Files for Claude Code — Prioritised Reading Order

### Tier 1 — before you touch anything (~20 min)

1. `CLAUDE-HANDOFF.md` — this file
2. `build-verification.log` — **the current build truth**
3. `docs/27-implementation-status.md` — live phase/task register
4. `docs/decision-log.md` — ADR-001 … ADR-031, highest authority
5. `docs/09-implementation-plan.md` §4, §6, §7, §8, §9 (Phase 1–2), §12, §13

> `CLAUDE.md` **does not exist** in this repository. If you create one, keep it a short pointer to this handoff — do not duplicate content that will drift.

### Tier 2 — before writing business logic

6. `docs/02-business-requirements.md` — inviolable invariants
7. `docs/04-end-to-end-business-flow.md` — MASTER flow (§5 numbering, §8 requests, §9 dispatch, §18 state machines, §21 in-transit, §22 costing, §23 negative stock, §25 traceability)
8. `docs/04-inventory-stock-model.md` — ledger/balance/WAC/concurrency
9. `docs/03-roles-permissions-data-scope.md` — roles, permission matrix, role denial, scopes
10. `docs/32-specification-conflict-register.md` — **check before "fixing" any inconsistency**

### Tier 3 — per phase

11. `docs/29-database-integrity-indexes-constraints.md` — **governs** `docs/06` (Phase 2)
12. `docs/06-database-schema.md` — table inventory only; read its warning banner first
13. `docs/08-authentication-session-security.md` (Phase 3)
14. `docs/09-authorization-security.md` (Phase 4)
15. `docs/14-audit-activity.md` (Phase 4)
16. `docs/28-document-numbering-and-sequences.md` (Phase 5)
17. `docs/30-transactions-concurrency-idempotency.md` (Phase 7)
18. `docs/07-api-specification.md` + `docs/13-validation-error-handling.md`
19. `docs/15-costing-financial-data.md`
20. `docs/23-acceptance-criteria.md` — the numerical scenarios every phase must satisfy

### Tier 4 — frontend

21. `ui-ux-pro-max/SKILL.md` — **MANDATORY before any component**
22. `docs/31-arabic-rtl-localization-spec.md`
23. `docs/frontend-ui-ux-implementation-guide.md` (incl. §8/§9 amendments)
24. `docs/10-frontend-architecture.md`, `docs/11-ui-ux-design-system.md`, `docs/12-navigation-role-based-screens.md`

### Tier 5 — guardrails, re-read often

25. `docs/25-developer-rules-anti-patterns.md` — the forbidden-pattern table
26. `docs/18-testing-strategy.md`
27. `docs/33-verified-environment-matrix.md`
28. `docs/open-decisions.md` — OD-007, OD-012
29. `docs/26-traceability-matrix.md` — REQ-01 … REQ-55
30. `docs/glossary.md` — canonical terms + prohibited-concept blacklist

### Code entry points

`src/Inventory.Api/Program.cs` · `src/Inventory.Infrastructure/DependencyInjection.cs` · `tests/Inventory.ArchitectureTests/LayeringRules.cs` · `Directory.Build.props` · `Directory.Packages.props`

---

## 20. Rules for the Next Coding Agent

### 20.1 Before changing anything

1. **Inspect the existing implementation first.** Files exist for most of Phase 1 — read before you write.
2. **Do not rebuild completed functionality.** The frontend scaffold is verified green; the solution skeleton exists and restores.
3. **Do not change architecture without a requirement.** Layering, CPM, strictness settings and health-probe design are all deliberate and documented.
4. **Do not trust stale documentation over newer approved decisions.** §17.1 is the authority order. The newest ADR always wins.
5. **Do not introduce a feature because it appears in a historical document.** Check §15 first.

### 20.2 Never weaken these

6. **Do not weaken authorization.** Role denial is pipeline-level, not an attribute.
7. **Do not use frontend hiding as security.** Mask in the SQL projection.
8. **Do not create restaurant inventory** in any form, under any name.
9. **Do not change stock semantics.** Dispatch never deducts; confirmation always does; consumption never does; reconciliation posts the delta.
10. **Do not reintroduce retired roles** — `Accountant` is gone permanently.
11. **Do not expose financial data** to anyone but `Owner`.
12. **Do not expose the audit log or activity monitor** to anyone but `Owner`.

### 20.3 While implementing

13. **Do not perform unrelated refactors** while implementing a workflow. One concern per change.
14. **Preserve** transaction boundaries, concurrency protection, idempotency, tenant isolation, scope enforcement and audit logging. If a change makes one of these harder, the change is wrong.
15. **Documentation-first change control.** If you discover a missing requirement, an ambiguity, a conflict, or a needed model/API/authorization change: **stop**, update the authoritative `docs/` file, then `decision-log.md` (if it is a decision) or `open-decisions.md` (if unresolved), then `26-traceability-matrix.md`, `09-implementation-plan.md` and `27-implementation-status.md`. Only then write code. **Never solve it in code alone.**
16. **Stay in phase.** If an unavoidable dependency needs later-phase work: stop, explain it, update the plan, and do not silently pull scope forward.
17. **Run the relevant tests after every change** — `build-verify.bat` for a full pass.
18. **Update documentation in the same change as the implementation**, never afterwards.

### 20.4 No fake completion

19. A task is **not** complete because a file exists. `docs/09 §7` lists 17 Definition-of-Done criteria; item 17 is **zero** `TODO`, `NotImplementedException`, commented-out logic, mock data, or placeholder presented as complete.
20. **Do not mark anything implemented unless you have verified it** — ideally by an executed command whose output you have seen.
21. Temporary scaffolding is allowed **only** when explicitly labelled temporary and never presented as working functionality.
22. **No hardcoded business identifiers, user IDs, company IDs, Owner accounts, or stock values.** Ever.

### 20.5 Environment realities

23. **PostgreSQL is not installed.** Do not work around it, do not substitute an engine, do not write a downgrade ADR. Phase 2 waits.
24. Two .NET-10-era analyzer settings (`TreatWarningsAsErrors` + `latest-recommended`) make the build strict. **Fix the code or scope the analyzer properly — do not disable `TreatWarningsAsErrors` globally.** That switch is a deliberate quality gate: a nullable warning in an inventory system is a future null balance.

---

## 21. Recommended Next Task

> ### Close Phase 1: make the solution build, then verify it.
> **Do not start Phase 2. Do not write business logic. Do not touch the database.**

### 21.1 What needs to be done

**Step 1 — Fix the four build errors (§12.2).**

| Error | Location | Suggested approach — verify before applying |
| :--- | :--- | :--- |
| `CA1707` ×2 | `tests/Inventory.UnitTests/DomainLayerContractTests.cs:22,34` | **Do NOT rename the tests.** `Method_Does_Thing` is the xUnit convention and is used across all four test projects. Scope the analyzer off for test projects — e.g. a `tests/Directory.Build.props` adding `<NoWarn>$(NoWarn);CA1707</NoWarn>`, or an `.editorconfig` section for `tests/**/*.cs`. Renaming would mean renaming ~20 tests and every future one. |
| `CS8604` | `src/Inventory.Api/Program.cs:60` | `httpContext.Request.Path.Value` is `string?`. Coalesce: `httpContext.Request.Path.Value ?? string.Empty`. |
| `CA1848` | `src/Inventory.Api/Middleware/GlobalExceptionHandler.cs:62` | Either implement a `LoggerMessage.Define` delegate (the analyzer's intent, and correct for a hot path), or scope CA1848 off with a documented justification. Prefer the delegate — this handler runs on every unhandled exception. |

**Step 2** — re-run `build-verify.bat` until `RESULT: ALL CHECKS PASSED`.

**Step 3** — run `verify-env.bat` (v2) and paste its literal output into `docs/33 §7.4`; resolve the ⚠️ rows in `docs/33 §4`.

**Step 4** — move `ci-workflow.yml` → `.github/workflows/ci.yml` (strip the instruction header) and delete the root copy.

**Step 5** — prove each architecture test by temporarily introducing a deliberate violation, confirming the test fails, then reverting. A rule that has never failed may be passing vacuously. Cover: Domain referencing EF Core · a non-`Program.cs` Api file naming `Inventory.Infrastructure` · a `double` property in Domain · a generic `Repository<T>` · `DROP DATABASE` in a `.bat`.

**Step 6** — update `docs/27-implementation-status.md`: Phase 1 register → all ✅, §9 Verification Ledger filled in, Phase 1 marked complete. Correct the §17.2 mismatches this handoff records.

### 21.2 Where to inspect first

1. `build-verification.log` — the errors
2. `Directory.Build.props` — lines 19–21, 29 (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel`, `NoWarn`)
3. `src/Inventory.Api/Program.cs` lines 54–62
4. `src/Inventory.Api/Middleware/GlobalExceptionHandler.cs` lines 58–68
5. `tests/Inventory.UnitTests/DomainLayerContractTests.cs` lines 18–38
6. `docs/09-implementation-plan.md` Phase 1 task table and §7 Definition of Done

### 21.3 Acceptance criteria

- `dotnet build InventorySystem.sln -warnaserror` → **0 errors, 0 warnings**
- `dotnet test InventorySystem.sln` → all projects run, **0 failures**
- `build-verify.bat` → `RESULT: ALL CHECKS PASSED`
- `env-verification.log` contains Run 2 output including all six `where` checks
- `.github/workflows/ci.yml` exists; `ci-workflow.yml` no longer at the root
- Every architecture rule demonstrated to fail on a deliberate violation
- `TreatWarningsAsErrors` still `true` — **not disabled to make the build pass**
- No production behaviour changed beyond the null-coalesce and the logging delegate
- `docs/27` accurately reflects reality

### 21.4 Tests that should pass afterwards

`DomainLayerContractTests` (2) · `HealthEndpointTests` (6: liveness, no environment disclosure, readiness verdict, correlation present, correlation echoed, malformed correlation replaced ×3 theory cases) · `LayeringRules` · `CompositionRootRules` · `ForbiddenPatternRules` · `StartupScriptRules` · `rtl-foundations.test.tsx` (3).

**Expect `/health/ready` to report `Unhealthy`** — PostgreSQL is absent, and `HealthEndpointTests` was written to accept either verdict precisely because of that.

### 21.5 Then stop

Report the result and wait. **Phase 2 needs PostgreSQL 16+ installed, which is not this task.**

---

## 22. Handoff Confidence / Verification

| Claim | Confidence | Basis |
| :--- | :--- | :--- |
| Repository structure, file inventory, absence of `.github/`, absence of migrations, absence of `CLAUDE.md` | **Verified from current code** | Directory listing, 2026-09-16 |
| Backend build fails with exactly 4 errors at the stated lines | **Verified from tests** | `build-verification.log`, 2026-09-11 08:12 |
| The offending source lines (`Path.Value`, `LogError`, test names) | **Verified from current code** | Files read, 2026-09-16 |
| `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` are the root cause | **Verified from current code** | `Directory.Build.props` lines 19–21 |
| Restore succeeds for all 7 projects; every pinned package resolves incl. `Serilog.AspNetCore` 9.0.0 | **Verified from tests** | `build-verification.log` |
| Frontend typecheck/lint/3 tests/build all green | **Verified from tests** | `build-verification.log` |
| .NET toolchain versions, Node `v22.20.0`, `dotnet`/`node` on PATH | **Verified from tests** | `env-verification.log` Run 1 |
| `npm` works | **Verified from tests** (indirect) | `npm ci` succeeded; `npm --version` never executed |
| `git`, `psql`, `docker` availability; all six `where` checks | **Unresolved** | Run 2 never executed |
| PostgreSQL and Docker not installed | **Last-known status** (2026-09-11) | Filesystem search; not re-verified 2026-09-16 |
| No .NET test has ever executed | **Verified from tests** | `build-verification.log` — DLLs not found |
| Architecture rules actually work | **Unresolved** | Never executed, never proven by violation |
| Frontend source is 3 files and nothing more | **Verified from current code** | `frontend/src` listing |
| `docs/` unchanged since 2026-09-11 01:04 | **Verified from current code** | mtimes |
| All business invariants in §4 | **Documented decision** | ADRs + `docs/02`, `docs/04` |
| Role model, permissions, non-grantable codes, retired `Accountant` | **Documented decision** | ADR-005/012/013/014/015, `docs/03` |
| Authentication, CSRF, OTP, rate limits, tenant isolation | **Documented decision** — none implemented | `docs/08`, `docs/09` |
| Database model in §8 | **Documented decision** — nothing implemented | `docs/06` + `docs/29` |
| API contract in §9.3 | **Documented decision** — nothing implemented | `docs/07` |
| Phase ordering and its four departures | **Documented decision** | `docs/09 §4` |
| Files/Reporting deferred | **Documented decision** | ADR-029 |
| OD-007, OD-012, integration-test host | **Unresolved** | `docs/open-decisions.md` |
| `docs/27` overstates Phase 1 completeness | **Verified from current code** | Contradicted by the build log — see §17.2 |

### 22.1 What this handoff does NOT know

- Whether anything changed on the machine after 2026-09-11 08:12 beyond the build artefacts observed.
- Whether PostgreSQL or Docker have been installed since.
- Whether the architecture tests are meaningful, or pass vacuously.
- Whether `run-local.bat`, `check-local.bat` and `stop-local.bat` work — none has ever been executed.
- Whether the Playwright E2E specs pass — never run.

**When in doubt: run the command and look at the output. Do not infer.**
