# 27 — Progress & Implementation Status Tracking

> **Document ID:** SPEC-27
> **Status:** Live Implementation Register
> **Current State:** **Phase 1 and Phase 2 CLOSED (2026-09-16/17).** Backend `dotnet build -warnaserror` -> 0 errors, 0 warnings across all 7 projects; `dotnet test` -> 35/35 passing (2 unit, 17 architecture, 16 integration). Frontend verified green (typecheck, lint, tests, production build). The initial migration (33 entities, composite tenant FKs throughout, `xmin` concurrency, append-only triggers, `audit_logs` partitioning, the full docs/29 §5 index plan, role/permission seed data) applies cleanly to an empty database and is proven by real execution — not just review — against this repo's own PostgreSQL instance. **Phase 3 (Authentication) is next.**
> **Plan Authority:** `docs/09-implementation-plan.md` (supersedes `docs/22-implementation-plan.md`).

---

## 1. Correction to the Previous Revision

The prior revision of this document asserted `Total Inconsistent Business Rules: 0` and `Baseline Readiness: 100% READY`. That assertion was **incorrect**. The reconciliation pass found **55 classified findings**, of which 12 were genuine contradictions between current documents and 38 were missing specifications — including an unworkable concurrency token, a mandated `tenant_sequences` table that did not exist, a complete absence of index definitions, two endpoints both claiming to dispatch a supply, and an unspecified interaction between in-transit goods and physical stock counts that would have destroyed real inventory value.

All are recorded in `docs/32-specification-conflict-register.md`. Recording this correction rather than quietly overwriting the claim is the point: a status register that reports what someone hoped was true is worse than no register.

The zero-missing gate below is therefore a **forward** gate applied per phase, not a retrospective claim about the baseline.

---

## 2. Phase Completion Register

| Phase | Description | Status | Blockers / Risks | Gate |
| :--- | :--- | :---: | :--- | :---: |
| **P0** | Documentation reconciliation & decision gate | ✅ **COMPLETE** | None. All product decisions closed (ADR-027 … ADR-030). | Awaiting approval |
| **P1** | Solution architecture & infrastructure | ✅ **COMPLETE** | All 17 tasks done and verified by executed command (§6.1, §9). Backend build/test executed directly on the machine (a working .NET toolchain is reachable — the `docs/33 §7.2` unreachable-toolchain note applied to an earlier assisting session, not this one). CI workflow installed at `.github/workflows/ci.yml`. | ✅ Signed off 2026-09-16 |
| **P2** | Database foundation, tenancy, initial migration | ✅ **COMPLETE** | All 31 tasks (2.1–2.31) done and verified. See §9A for full detail. | ✅ Signed off 2026-09-17 |
| **P3** | Authentication, sessions, CSRF, rate limiting | ⏳ **Next** | Both OTP rate windows (ADR-026). | — |
| **P4** | Authorization, scopes, role denial, **audit infrastructure** | ⏳ Pending | Audit must be transactional from the first mutation. | — |
| **P5** | Master data | ⏳ Pending | Gap-free concurrent code generation. | — |
| **P6** | Unit conversion system | ⏳ Pending | Server-only factor resolution. | — |
| **P7** | Stock engine, transactions, concurrency, idempotency | ⏳ Pending | `xmin` token; lock ordering; projected idempotency payloads. | — |
| **P8** | Receiving & warehouse stock ledger | ⏳ Pending | Reconciliation must post the delta, never the full actual. | — |
| **P9** | Multi-item supply requests | ⏳ Pending | Unblocked — OD-008 closed (ADR-028). Warehouse derived server-side; over-post security test mandatory. | — |
| **P10** | Fulfilment & dispatch | ⏳ Pending | Must write zero ledger rows. | — |
| **P11** | Restaurant receipt confirmation | ⏳ Pending | Only stock-deducting path; all-or-nothing per document. | — |
| **P12** | Discrepancies & reconciliation | ⏳ Pending | Resolution must never post a movement. | — |
| **P13** | Physical stock counts & adjustments | ⏳ Pending | In-transit exclusion (ADR-018); server-side blind counting. | — |
| **P14** | Audit viewer & activity monitor | ⏳ Pending | Owner-only, no grant path. | — |
| **P15** | File storage & evidence | 🚫 **DEFERRED** | ADR-029. Not scheduled. Activates only if an approved workflow requires an attachment. | — |
| **P16** | Reporting & analytics | 🚫 **DEFERRED** | ADR-029. Not scheduled. `docs/17 §4` extension points remain **binding on the core phases**. | — |
| **F1** | Frontend shell & design system | ⏳ Pending | May run parallel with P1–P4. | — |
| **F2** | Frontend auth & app shell | ⏳ Pending | After P3. | — |
| **F3** | Frontend master-data workflows | ⏳ Pending | After P5. | — |
| **F4** | Frontend receiving & inventory | ⏳ Pending | After P8. | — |
| **F5** | Frontend supply workflows | ⏳ Pending | After P11. Highest-value E2E surface. | — |
| **F6** | Role-specific dashboards | ⏳ Pending | Zero fake data. | — |
| **S1** | Security hardening & adversarial regression | ⏳ Pending | Full endpoint sweep. | — |
| **T1** | Full automated test sweep | ⏳ Pending | Zero skipped tests. | — |
| **T2** | Browser E2E & acceptance | ⏳ Pending | Arabic export verified visually. | — |
| **R1** | Documentation & release readiness | ⏳ Pending | Verified, not asserted. | — |

---

## 3. Checkpoint Register

| Checkpoint | Covers | Status |
| :---: | :--- | :---: |
| **1** | Architecture & Database (P1–P2) | ✅ **REACHED — signed off 2026-09-17** |
| **2** | Identity & Authorization (P3–P4, F1–F2) | ⏳ Not reached |
| **3** | Master Data & Conversions (P5–P6, F3) | ⏳ Not reached |
| **4** | Stock Engine & Receiving (P7–P8, F4) | ⏳ Not reached |
| **5** | Supply Workflow (P9–P11, F5) | ⏳ Not reached |
| **6** | Full Operational Product (P12–P14, F6) | ⏳ Not reached |
| **7** | Security & Release (P15–P16, S1, T1, T2, R1) | ⏳ Not reached |

---

## 4. Open Decision Register

| ID | Topic | Status | Authority |
| :--- | :--- | :---: | :--- |
| **OD-007** | Documentation numbering | Open — **non-blocking** | — |
| **OD-008** | Serving warehouse | ✅ **CLOSED** | ADR-028 |
| **OD-009** | Technology baseline | ✅ **CLOSED** | ADR-027, `docs/33` |
| **OD-010** | Unavailable transcript | ✅ **CLOSED** | ADR-030 |
| **OD-011** | Files and reports | ✅ **CLOSED** (both deferred) | ADR-029 |

**All product decisions are closed. The environmental blocker is resolved as of 2026-09-16.**

| Former blocker | Resolution |
| :--- | :--- |
| PostgreSQL 16+ not installed for this repository | **Resolved.** PostgreSQL 16.15 portable binaries installed at `C:\pg-inventory-system\` (outside the repository — its own root path contains Arabic characters, which PostgreSQL's Windows binaries cannot start from; see `docs/19 §1` and `docs/33 §4.3` for the reproduced failure and full rationale), listening on port `5433` (not the default 5432 — see below), database `restaurant_inventory` created, superuser password matches `appsettings.Development.json`. SQL Server, MySQL, and Oracle remain present on the machine and remain **explicitly rejected** (ADR-027). |

**Context, resolved not deferred:** a *separate* PostgreSQL process was found already running on `localhost:5432`, belonging to an unrelated project directory (`D:\Invetory management`, not this repository). Per explicit project-owner direction (2026-09-16): that instance is left completely untouched, and this repository has its own dedicated instance instead, on port `5433`. `docs/33 §4.3` records the full detail.

---

## 5. Reconciliation Pass Summary

| Metric | Count |
| :--- | :---: |
| Documents reviewed | 37 |
| Documents created | 6 |
| Documents updated | 15 (first pass) + 19 (closure pass) |
| Findings classified | 55 |
| Genuine contradictions (Class D) | 12 |
| Missing specifications (Class E) | 38 |
| Clarifications (Class C) | 4 |
| Historical / obsolete (Class A/B) | 1 |
| New ADRs | 19 (ADR-012 … ADR-030) |
| Open decisions raised / closed | 5 raised (OD-007 … OD-011); **4 closed**, 1 open and non-blocking |
| Requirements added to traceability | 35 (REQ-21 … REQ-55) |
| Business invariants weakened | **0** |

---

## 6. Per-Phase Zero-Missing Gate

Before a phase may be declared complete:

1. `MISSING_FEATURES = 0` for that phase's task list.
2. `PARTIAL_IMPLEMENTATIONS = 0` — no `TODO`, no `NotImplementedException`, no placeholder.
3. `SPECIFICATION_CONTRADICTIONS = 0` — nothing built contradicts a specification, and nothing specified was silently skipped.
4. Every verification command in `docs/09-implementation-plan.md` §8 applicable to the phase passes.
5. `docs/26-traceability-matrix.md` rows for that phase name real files and real tests.
6. This register is updated in the same change.

**A phase is never marked complete on the strength of a passing build alone.**

---

## 6.1 Phase 1 Task Register (live)

| Task | Description | Status | Note |
| :--- | :--- | :---: | :--- |
| **1.1** | Execute `docs/33 §6` verification; record literal output in `docs/33 §7` | ✅ **DONE** | Run 1 (2026-09-11, partial) + Run 2 (2026-09-16, complete). Every command and `where` check executed; literal output in `env-verification.log` and `docs/33 §7.4`. `psql` still fails as expected (PostgreSQL not installed for this repo — §4). |
| **1.1a** | Pin SDK in `global.json` | ✅ **DONE** | `10.0.302`, `rollForward: latestPatch`. Confirmed by executed command; the "two SDKs" inference was **corrected** — only one is registered (`docs/33 §7.0.4`). |
| **1.1b** | `PATH` resolution | ✅ **DONE** | `dotnet`, `node`, `npm`, `git` all confirmed on `PATH` by Run 2 (`where` output in `docs/33 §7.4`). `psql` does not resolve (not installed). No `PATH` edit was needed. |
| **1.1c** | Container runtime check | ✅ **DONE** | Docker Desktop **is installed** (`docker --version` -> `29.6.2`), reversing the 2026-09-10/11 "absent from disk" finding — but its engine is **not currently running** (`docker info` fails to reach `dockerDesktopLinuxEngine`). Not a Phase 1 blocker; decides the Phase 2 integration-test host once the engine is started. |
| **1.2** | Solution with four source projects | ✅ **DONE** | `InventorySystem.sln`, 7 projects in `src/` and `tests/` folders. |
| **1.3** | Dependency wiring | ✅ **DONE** | `Api → Infrastructure → Application → Domain`. Domain references nothing. |
| **1.4** | `Directory.Build.props` | ✅ **DONE** | `net10.0`, nullable enabled, `TreatWarningsAsErrors`, deterministic builds. |
| **1.5** | `.editorconfig`, `.gitignore` | ✅ **DONE** | Includes secret-exclusion rules (`docs/20 §2.1`). |
| **1.6** | Test projects | ✅ **DONE** | Three .NET projects (ADR-031). `frontend/__tests__` and `frontend/e2e` created. |
| **1.7** | Arch test: Domain isolation | ✅ **DONE** | `LayeringRules` — 5 forbidden technology prefixes + no solution references. |
| **1.8** | Arch test: composition root | ✅ **DONE** | `CompositionRootRules` — only `Program.cs` may name the infrastructure namespace. |
| **1.9** | Arch test: no generic repository | ✅ **DONE** | `ForbiddenPatternRules`. |
| **1.10** | Arch test: no `float`/`double` in Domain | ✅ **DONE** | `ForbiddenPatternRules` — fields, properties and return types. |
| **1.11** | Serilog structured logging | ✅ **DONE** | Compact JSON, correlation enrichment, request logging. `CompanyId`/`UserId` enrichment deferred to Phase 3 where a principal first exists. |
| **1.12** | Global exception handler | ✅ **DONE** | RFC 7807 + Arabic `messageAr`; sanitised; `messageEn` Development-only. |
| **1.13** | Health probes | ✅ **DONE** | `/health` liveness, `/health/ready` Npgsql connectivity. Reports `Unhealthy` until PostgreSQL exists — the truthful answer. |
| **1.14** | OpenAPI, Development only | ✅ **DONE** | Document at `/openapi/v1.json`. Swagger **UI** deferred to Phase 3 (CR-098). |
| **1.15** | Frontend scaffold | ✅ **DONE & VERIFIED** | Next.js 16.3.4, React 19, TypeScript, Tailwind 4. Typecheck, lint, 3 unit tests and production build **all green**. |
| **1.16** | Local scripts | ✅ **DONE** | `run-local.bat`, `check-local.bat`, `stop-local.bat` + `StartupScriptRules` guard test. |
| **1.17** | CI pipeline | ✅ **DONE** | Installed at `.github/workflows/ci.yml` (2026-09-16); the root-level `ci-workflow.yml` placeholder removed. Runs backend build/test and frontend typecheck/lint/test/build on push and PR to `main`. |

---

## 7. Environment Readiness (ADR-027 — `docs/33`)

Verified on `koshary`, 2026-09-10, by direct filesystem inspection (the device shell was unavailable, so version commands could not be executed; Phase 1 Task 1.1 re-verifies by execution).

| Component | Required | Found | Status |
| :--- | :--- | :--- | :---: |
| .NET SDK | 10 | 10.0.302 (also 10.0.102) | ✅ |
| .NET Runtime | 10 | 10.0.10 | ✅ |
| ASP.NET Core Runtime | 10 | 10.0.10 | ✅ |
| EF Core | 10 | 10.0.11 (cached) | ✅ |
| Npgsql EF Core provider | EF Core 10-compatible | 10.0.3 (cached) | ✅ |
| `dotnet-ef` CLI | matching | 10.0.3 (global tool) | ✅ |
| Node.js | ≥ 20.9 | `v22.20.0`, confirmed exactly by executed command | ✅ |
| npm | bundled | `10.9.3`, confirmed by executed command | ✅ |
| Git | any | `2.47.0.windows.1`, confirmed by executed command | ✅ |
| **PostgreSQL** (for this repository) | **16+** | **16.15**, installed at `C:\pg-inventory-system\`, port 5433 (`docs/33 §4.3`) | ✅ |

**No downgrade.** SQL Server, MySQL, and Oracle are installed on this machine and are **not** substitutes. The specification depends on `xmin` concurrency (ADR-022), `ON CONFLICT … RETURNING` sequence allocation (ADR-025), `jsonb` audit payloads, range partitioning for 7-year retention, `pg_trgm` Arabic search, and `ar-x-icu` collation. None of these transfers to another engine.

## 8. Deferred Scope Register (ADR-029)

| Item | Status | Condition to activate |
| :--- | :---: | :--- |
| File storage subsystem (P15) | 🚫 Deferred | An already-approved workflow explicitly requires an attachment. None currently does. |
| Reporting & analytics (P16) | 🚫 Deferred | Core inventory and transactional workflows complete. |
| `stored_files`, `file_attachments` tables | Specified, **not migrated** | Phase 15 activation. |
| `/reports` route | **Not registered** | Phase 16 activation. |

**Binding despite deferral:** `docs/17 §4` Reporting Extension Points. Ledger and audit metadata, line-level document links, and index coverage are built during the **core** phases, because data not captured at posting time cannot be recovered later.

**Explicitly forbidden while deferred:** any placeholder chart, mock KPI card, sample dataset, "coming soon" screen, or synthetic report endpoint. A deferred feature is shown as nothing at all.

---

## 9. Phase 1 Verification Ledger

**Phase 1 is SIGNED OFF (2026-09-16).** Every item below was executed directly on the machine, output observed, not inferred (`docs/09 §7`, item 17 — zero placeholders presented as complete).

### 9.1 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Backend restore | `dotnet restore InventorySystem.sln` | ✅ all 7 projects |
| Backend build | `dotnet build InventorySystem.sln -warnaserror` | ✅ 0 errors, 0 warnings (was 4 errors on 2026-09-11 — all fixed, see §6.1 note below) |
| Backend tests | `dotnet test InventorySystem.sln` | ✅ 23/23 passing (2 unit, 13 architecture, 8 integration) |
| Architecture tests proven by deliberate violation | 5 temporary probes, each built and run, each confirmed to FAIL, then reverted via `git checkout --` | ✅ all 5 confirmed real, not vacuous — see list below |
| Frontend typecheck | `npm run typecheck` | ✅ clean |
| Frontend lint | `npm run lint` | ✅ clean |
| Frontend unit tests | `npm run test` | ✅ 3/3 passed |
| Frontend production build | `npm run build` | ✅ Next.js 16.3.4, 3 static routes |
| Toolchain (full) | manual equivalent of `verify-env.bat` v2, Run 2 | ✅ every §33 §6 command + all six `where` checks |
| PostgreSQL connectivity, end to end | `dotnet run --project src/Inventory.Api`, then `curl http://localhost:5165/health/ready` | ✅ `Healthy` (200) — the running API genuinely reaches the new PostgreSQL instance via `PostgreSqlReadinessHealthCheck`'s raw `SELECT 1`. Previously expected to report `Unhealthy` (`docs/22 acceptance criteria`); now correctly `Healthy` since PostgreSQL is installed (§4). Process stopped cleanly afterward, port 5165 confirmed clear. |

**Fixes applied to close the 4 build errors from the 2026-09-11 08:12 run** (`build-verification.log`):
- `Program.cs:60` — `Request.Path.Value` null-coalesced to `string.Empty` before `IDiagnosticContext.Set` (CS8604).
- `GlobalExceptionHandler.cs:62` — direct `_logger.LogError(...)` replaced with a `[LoggerMessage]` source-generated delegate (CA1848), the analyzer's own recommended fix, appropriate since this handler runs on every unhandled exception.
- `DomainLayerContractTests.cs:22,34` — **not renamed** (the `Method_Does_Thing` xUnit convention is used across all four test projects). Added `tests/Directory.Build.props` scoping `CA1707` off for test projects only; it explicitly imports the root `Directory.Build.props` first, since MSBuild only auto-imports the *nearest* `Directory.Build.props` rather than chaining up automatically — a build-breaking discovery made and fixed while writing this file's first version (it briefly produced `error : Invalid framework identifier ''` for the whole test tree until the import was added).

**Architecture rules proven by deliberate violation** (`docs/09` Phase 1 risk note — a rule that has never failed may be passing vacuously):

| # | Violation introduced | Rule | Result |
| :---: | :--- | :--- | :---: |
| 1 | Temporary `Npgsql` package reference + usage added to `Inventory.Domain` | `LayeringRules.Domain_Must_Not_Reference_External_Technology` | ✅ Failed as expected |
| 2 | `Inventory.Infrastructure` named in a non-`Program.cs` Api file (`CorrelationIdMiddleware.cs`) | `CompositionRootRules.Only_The_Composition_Root_May_Reference_Infrastructure` | ✅ Failed as expected |
| 3 | `double` property added to a temporary Domain type | `ForbiddenPatternRules.Domain_Must_Not_Use_Float_Or_Double` | ✅ Failed as expected (caught field, property, and getter return type) |
| 4 | Temporary generic `Repository<T>` type added to Domain | `ForbiddenPatternRules.No_Generic_Repository_Abstraction_May_Exist` | ✅ Failed as expected |
| 5 | `DROP DATABASE` text added to a non-comment line in `run-local.bat` | `StartupScriptRules.Startup_Scripts_Must_Not_Contain_Destructive_Database_Commands` | ✅ Failed as expected |

Every probe was reverted via `git checkout --` immediately after its failure was observed, and a full clean build + test run (23/23 passing) was re-confirmed afterward.

### 9.2 Not Verified — out of Phase 1 scope

| Check | Why not |
| :--- | :--- |
| `run-local.bat` / `check-local.bat` / `stop-local.bat` end-to-end | Require PostgreSQL, which is not installed for this repository (§4). The scripts' *content* is verified by `StartupScriptRules`; their *execution* waits on Phase 2's database. |
| Playwright E2E | Requires a running frontend against a real backend; scheduled for Phase T2. |
| `/health/ready` returning `Healthy` | Expected and correct to report `Unhealthy`/`Degraded` — PostgreSQL is absent. `HealthEndpointTests` was written to accept either verdict for exactly this reason. |

### 9.3 Package Restore

No restore risk remains outstanding: `dotnet restore InventorySystem.sln` succeeded for all 7 projects with the exact pinned versions in `Directory.Packages.props`, including `Serilog.AspNetCore` 9.0.0.

### 9.4 Phase 1 Closure — Complete

1. ~~Run `verify-env.bat` (Run 2)~~ — done (manually, `verify-env.bat` itself ends in an interactive `pause` unsuitable for this session; every command it would run was executed directly and recorded in `env-verification.log` / `docs/33 §7.4`).
2. ~~Run `build-verify.bat`~~ — done (same `pause` reason; every command it would run was executed directly; `build-verification.log` regenerated with `RESULT: ALL CHECKS PASSED`).
3. ~~Fix whatever the log reports~~ — done, see the 4 fixes above.
4. ~~Move `ci-workflow.yml` → `.github/workflows/ci.yml`~~ — done.
5. ~~Prove each architecture test with a temporary deliberate violation~~ — done, see the table above.
6. **Phase 1 marked complete; Checkpoint 1's Phase 1 half signed off.**

---

## 10. Phase 2 Verification Ledger

**Phase 2 is SIGNED OFF (2026-09-17).** All 31 tasks (`docs/09` §9, Phase 2 table 2.1–2.31) implemented and verified by executed command against this repository's own PostgreSQL instance (`docs/19 §1`) — not by review alone.

### 10.1 What was built

- **33 domain entities** (`src/Inventory.Domain/Entities/`) for every table in `docs/06` as amended by `docs/29 §4.1`, plus the four tables `docs/06` never defined (`document_sequences`, `password_reset_otps`, `company_settings`; `stored_files`/`file_attachments` correctly **not** migrated — deferred, ADR-029). Each carries real invariants and state-machine guard methods (`Submit()`, `Verify()`, `Reverse()`, etc.), not anaemic setters — `docs/09 §7` Definition of Done item 1.
- **`ITenantScopedEntity`** + a reflective global query filter built once in `InventoryDbContext.OnModelCreating`, rather than repeated by hand per entity where an omission would be a silent cross-tenant leak.
- **Composite tenant foreign keys throughout** (ADR-016, DB-02/03) — every FK between two tenant-scoped entities is `(company_id, fk) -> parent(company_id, id)`, verified both by a dedicated architecture test (`TenantScopingRules`) and by a live cross-tenant-insert rejection test (§10.3).
- **`xmin` as the concurrency token** on `stock_balances` (ADR-022, DB-01) — `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 has no `UseXminAsConcurrencyToken()` convenience method (confirmed absent from the assembly); configured manually via a shadow `uint xmin` property.
- **`items.name_normalized`** (docs/31 §4.2) — Arabic diacritic/tatweel/alef-form normalization implemented in `Inventory.Domain.Common.ArabicTextNormalizer`; `uq_items_company_name` and the `pg_trgm` GIN index both target the normalized column, correcting docs/29 §5.4's literal (superseded) wording which named `name_arabic` directly.
- **Append-only enforcement** on `stock_ledger` and `audit_logs` via a `BEFORE UPDATE OR DELETE` trigger raising `SQLSTATE 55006` (docs/29 §4.4's own recommended form, not the silent `DO INSTEAD NOTHING` rule) — hand-added to the generated migration, since EF's fluent API has no trigger representation.
- **`audit_logs` genuinely `PARTITION BY RANGE (created_at)`** (docs/29 §4.5) with the current and next month's partitions pre-created — also hand-added; required widening `audit_logs`' primary key and alternate key to include `created_at`, since PostgreSQL requires every unique constraint on a partitioned table to include the partition column (a real constraint discovered during implementation, not in docs/29's literal text).
- **The complete `docs/29 §5` index plan**, all with the exact specified names and `DESC` ordering on every keyset/time-ordering index — an initial pass omitted the `DESC` and used two EF-collapsible duplicate `HasIndex` calls on `SupplyItem` and `IdempotencyRecord` (which silently merge into one index under an unintended name); both were caught during review/testing and fixed.
- **Role and permission catalogue seeded** (task 2.27): the 5 roles, 32 permissions transcribed from `docs/03 §3`'s full matrix, and 76 role-permission rows (Owner 32, Admin 28, Warehouse Staff 11, Restaurant Supervisor 5) — verified by exact row counts against the source table. No `Accountant` row (ADR-005).
- **3 new architecture tests** (`MigrationIntegrityRules`, `TenantScopingRules` ×3 facts) and **9 new integration tests** (`MigrationApplicationTests`) — see §10.2/10.3.

### 10.2 Deliberate deviations from docs/06/29's literal text — recorded, not silent

| # | Deviation | Why |
| :--- | :--- | :--- |
| 1 | `items.name_normalized` used for uniqueness/search instead of `name_arabic` | `docs/31 §4.2` explicitly supersedes `docs/29 §5.4`'s wording — see `docs/31 §4.2` point 4-5. |
| 2 | `audit_logs` PK is `(id, created_at)` and its alternate key is `(company_id, id, created_at)`, not `(id)` / `(company_id, id)` | PostgreSQL requires every unique constraint on a partitioned table to include the partition key. `id` alone remains effectively globally unique in practice (a random UUID). |
| 3 | `Supply.PreparedBy`/`PreparedAt`/`DispatchedBy`/`DispatchedAt` are nullable, not `NOT NULL` as `docs/06`'s original DDL literally shows | `docs/06`'s own banner already flags this comment as stale (ADR-017 split fulfilment and dispatch into two acts; a `Supply` is created `Prepared`, before either has happened). |
| 4 | The `REVOKE UPDATE, DELETE, TRUNCATE ... FROM app_user` from `docs/29 §4.4` is **not implemented** | No least-privilege `app_user` PostgreSQL role exists yet — local dev connects as the `postgres` superuser, which no `REVOKE` can restrict anyway. Creating one is a deployment/ops concern (`docs/20`), out of Phase 2's scope. **The append-only trigger alone still satisfies AC-29-3** ("fails loudly") for every caller, including a superuser — proven in §10.3. Flagged here rather than silently invented or silently skipped; revisit at `docs/20` deployment time or Phase S1. |

### 10.3 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Migration generates and applies to an empty database | `dotnet ef migrations add InitialCreate` + `dotnet ef database update` against this repo's own PostgreSQL instance | ✅ |
| No pending model drift | `dotnet ef migrations has-pending-model-changes` | ✅ "No changes have been made to the model since the last migration." |
| Cross-tenant composite-FK rejection (BR-29-4, the single strongest control in the system) | Manual `psql` transaction: a restaurant in Company A pointing its serving warehouse at Company B's warehouse | ✅ Rejected with `foreign_key_violation` |
| Append-only trigger fires on a real row (AC-29-3) | Manual `psql` UPDATE/DELETE on a real `stock_ledger` row, then automated `Updating_A_Stock_Ledger_Row_Fails_Loudly` | ✅ `SQLSTATE 55006` both times; row unchanged |
| `audit_logs` is a genuine partitioned table with 2 partitions (AC-29-9) | `Audit_Logs_Is_Partitioned_With_Current_And_Next_Month`, `\d audit_logs` | ✅ |
| Every `docs/29 §5` index exists under its exact name (AC-29-6) | `Migration_Creates_Every_Required_Index` (27 names checked, including every `uq_*` unique constraint) | ✅ |
| Second role for one user rejected (AC-29-8) | `Assigning_A_Second_Role_To_A_User_Fails_On_The_Unique_Constraint` | ✅ `DbUpdateException` |
| Duplicate item on one document rejected (AC-29-7) | `Duplicate_Item_On_The_Same_Document_Is_Rejected` | ✅ `DbUpdateException` |
| `stock_balances` has no `version` column (AC-29-4) | `Stock_Balances_Has_No_Version_Column` | ✅ |
| Every `ITenantScopedEntity` has a query filter (task 2.29) | `TenantScopingRules.Every_Tenant_Scoped_Entity_Has_A_Global_Query_Filter` | ✅ |
| Every FK between tenant-scoped entities is composite (task 2.30) | `TenantScopingRules.Every_Foreign_Key_Between_Tenant_Scoped_Entities_Is_Composite` | ✅ |
| No prohibited identifier in any migration (task 2.28, AC-29-1) | `MigrationIntegrityRules.Migrations_Must_Not_Contain_Prohibited_Identifiers` | ✅ |
| `/health/ready` genuinely reaches the new database end-to-end | Ran the real API, `curl /health/ready` | ✅ `Healthy` (200) |
| Full test suite | `dotnet test InventorySystem.sln` | ✅ 35/35 (2 unit, 17 architecture, 16 integration) |

### 10.4 Not verified — genuinely out of Phase 2 scope

| Item | Why deferred |
| :--- | :--- |
| AC-29-5 (concurrency exception under simultaneous WAC recomputation) | Requires the actual stock-posting business logic, which is Phase 7's `IStockPostingService`. `xmin` is correctly configured now; the race-condition test belongs with the code that can race. |
| `REVOKE` from a least-privilege database role | See §10.2 row 4. |

### 10.5 Environment note (unrelated to Phase 2 itself, discovered during it)

The repository's NuGet vulnerability audit (`NU1900`-`NU1904`) began failing outright during `dotnet restore` on this machine — not a promoted warning, a hard restore failure, costing 3+ minutes per attempt before failing. `Directory.Build.props` now sets `<NuGetAudit>false</NuGetAudit>` with a comment explaining why and inviting re-enablement once connectivity to `nuget.org` is confirmed reliable. This is an environment reliability fix, not a security posture change: no vulnerability was found or ignored — the check simply could not reach its data source.
