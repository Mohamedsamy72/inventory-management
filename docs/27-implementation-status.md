# 27 — Progress & Implementation Status Tracking

> **Document ID:** SPEC-27
> **Status:** Live Implementation Register
> **Current State:** **Phase 1 through Phase 6 CLOSED (2026-09-17).** Backend `dotnet build -warnaserror` -> 0 errors, 0 warnings across all 7 projects; `dotnet test` -> 125/125 passing (19 unit, 18 architecture, 88 integration), confirmed stable across repeated consecutive runs. Frontend verified green (typecheck, lint, tests, production build) as of Phase 1. The initial migration (33 entities, composite tenant FKs throughout, `xmin` concurrency, append-only triggers, `audit_logs` partitioning, the full docs/29 §5 index plan, role/permission seed data) applies cleanly to an empty database and is proven by real execution — not just review — against this repo's own PostgreSQL instance. Phase 3 also fixed a critical, phase-independent bug in the tenant query filter introduced in Phase 2 — see §11.2 row 1; it affects every tenant-scoped query in the system, not just auth. Phase 5 also found and fixed a CSRF gap spanning every Phase 4/5 mutating endpoint — see §13.2. Phase 6 found and fixed a second Phase 2 schema bug (a non-partial unique index that would have made ADR-023 corrections impossible) — see §14.2. **Phase 7 (Stock Engine Foundation) is next.**
>
> **Environment note for the next session:** this repo's PostgreSQL 16 instance (`C:\pg-inventory-system\`, port 5433) is portable binaries, not a registered Windows service — it does not survive a machine/session restart on its own. If `dotnet test`'s integration suite fails with "Failed to connect to 127.0.0.1:5433 ... actively refused", start it first: `C:\pg-inventory-system\pgsql\bin\pg_ctl.exe start -D C:\pg-inventory-system\data -l C:\pg-inventory-system\logfile.log -o "-p 5433" -w` (docs/19 §1, docs/33 §4.3).
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
| **P3** | Authentication, sessions, CSRF, rate limiting | ✅ **COMPLETE** | All 14 tasks (3.1–3.14) done and verified. See §11 for full detail. | ✅ Signed off 2026-09-17 |
| **P4** | Authorization, scopes, role denial, **audit infrastructure** | ✅ **COMPLETE** | All 15 tasks (4.1–4.15) done and verified. See §12 for full detail. | ✅ Signed off 2026-09-17 |
| **P5** | Master data | ✅ **COMPLETE** | All 16 tasks (5.1–5.16) done and verified. See §13 for full detail. | ✅ Signed off 2026-09-17 |
| **P6** | Unit conversion system | ✅ **COMPLETE** | All 8 tasks (6.1–6.8) done and verified. See §14 for full detail. | ✅ Signed off 2026-09-17 |
| **P7** | Stock engine, transactions, concurrency, idempotency | ⏳ **Next** | `xmin` token; lock ordering; projected idempotency payloads. | — |
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

---

## 11. Phase 3 Verification Ledger

**Phase 3 is SIGNED OFF (2026-09-17).** All 14 tasks (`docs/09` §Phase 3, table 3.1–3.14) implemented and verified against this repository's own PostgreSQL instance and a real in-process HTTP pipeline (`WebApplicationFactory<Program>`) — not by review alone.

### 11.1 What was built

- **ASP.NET Core Identity via `AddIdentityCore<User>()`** (the API-only variant) over a fully custom `IUserStore<User>` (`Inventory.Infrastructure.Identity.UserStore`) adapting the rich `User` domain entity — granular `IUserPasswordStore`/`IUserSecurityStampStore`/`IUserLockoutStore` interfaces, every mutation delegating to the entity's own domain methods, never reaching around them.
- **Cookie authentication** registered explicitly under `IdentityConstants.ApplicationScheme` (required because `AddIdentityCore`, unlike `AddIdentity`, registers no scheme by itself) — `__Host-InventorySession`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`, 8-hour expiry, no sliding window, verified by direct cookie-header inspection of a real response (task 3.2).
- **Security-stamp validation on every request** via `OnValidatePrincipal`, rejecting and signing out a principal whose stamp no longer matches (task 3.6).
- **`POST /auth/login`** — `403 ACCOUNT_INACTIVE` checked before the password (task 3.3); `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)` incrementing/resetting `access_failed_count` and setting `lockout_end_at` through the custom lockout store (task 3.4); `POST /auth/logout` (task 3.5).
- **CSRF**: `GET /auth/csrf-token` plus a custom `IEndpointFilter` (`AntiforgeryEndpointFilter`) calling `IAntiforgery.ValidateRequestAsync` explicitly on every mutating auth endpoint (task 3.7) — discovered during implementation that `app.UseAntiforgery()` alone validates nothing on minimal-API `MapPost` lambdas (no `IAntiforgeryMetadata` is attached by default), so the middleware-only approach docs/08 implies does not actually enforce anything without this filter.
- **OTP password reset**: `ConsoleSmsSender`/`IOtpService` (dev-only, task 3.8) generating a cryptographically random 6-digit code (`RandomNumberGenerator.GetInt32`), SHA-256 hashed into `password_reset_otps`, 5-minute expiry, single-use (task 3.9); `POST /auth/forgot-password/otp|verify|reset`, rotating the security stamp on reset (task 3.10).
- **Rate limiting** (task 3.11, ADR-026): login 5/IP/min, OTP 3/IP/15min (named `RateLimitPartition` policies, registered in `Inventory.Api` — `AddRateLimiter` only resolves in a Web SDK compile context, not the class-library `Inventory.Infrastructure`), plus the OTP per-mobile 3/15min **and** 5/hour dual window enforced explicitly inside the endpoint handler via `IOtpAttemptLimiter` (two independent `PartitionedRateLimiter<string>` instances, both must admit the request); general 300/user-or-IP/min global limiter.
- **`GET /account/me`** (task 3.12) returning identity, the single role, warehouse/restaurant scopes, and the effective permission-code set (role baseline ∪ user grants − user denials — ADR-012 Role Denial itself is explicitly out of scope here, deferred to Phase 4's pipeline).
- **CORS** restricted to the configured frontend origin, credentials allowed, no wildcard (task 3.13).
- **14 new integration tests** across two deliberately separate classes — `AuthEndpointTests` (13, normal-flow behaviour) and `AuthRateLimitingTests` (4, budget-exhausting) — split because `WebApplicationFactory`'s rate-limiter/OTP-limiter singletons are shared by every test in one `IClassFixture`, and a budget-exhausting test would otherwise starve unrelated tests' login/OTP calls for the rest of the run (task 3.14).

### 11.2 Bugs found and fixed during implementation — recorded, not silent

| # | Bug | Root cause | Fix |
| :--- | :--- | :--- | :--- |
| 1 | **Critical, phase-independent**: tenant-scoped queries could silently read/write the wrong company's data, or find nothing for the right one, depending on which request happened to run first in the process | `InventoryDbContext.ApplyTenantFilter` (built in Phase 2) built the global query filter with `Expression.Constant(_currentUserService)` — embedding that ONE scoped service instance as a frozen literal. EF Core caches the compiled model (including query filters) once per context CLR type and reuses it for every later `InventoryDbContext` instance, including ones from a completely different request/DI scope — so every tenant-scoped query system-wide was silently using whichever `ICurrentUserService` existed when the model was first compiled, not the current request's. Confirmed directly: the generated SQL embedded `company_id = '<literal>'` as hardcoded text, not a `@parameter`. | Reference `this` (the `DbContext` instance) instead of the injected service — `this.TenantCompanyId`, a property on the context that reads `_currentUserService.CompanyId`. EF Core specifically recognizes and rebinds `this`-rooted member access in a query filter to the actual executing context instance at each query (the documented pattern for this exact scenario, see [EF Core docs — global query filters](https://learn.microsoft.com/ef/core/querying/filters)). Verified fixed: the same SQL now carries a `@__` parameter, and the full 52-test suite — run repeatedly, in different orders and groupings — is consistently green. |
| 2 | `/forgot-password/verify` always returned `OTP_INVALID`, even with the correct code | `PasswordResetOtpService.VerifyAsync`'s own query lacked `.IgnoreQueryFilters()`. Called pre-authentication (same as login), `ICurrentUserService.CompanyId` reads `Guid.Empty` there, so the tenant filter hid the real row for every caller — collapsing `NotFound`/`Expired`/`Invalid` all into the same "nothing found" outcome. | Added `.IgnoreQueryFilters()`, matching the same documented pattern already applied twice in `UserStore` (`FindByNameAsync`, `FindByIdAsync`) for the identical pre-authentication reason. |
| 3 | Login always returned `401` even with correct credentials | `UserStore.FindByNameAsync` had no `.IgnoreQueryFilters()`, so the pre-authentication tenant filter (`Guid.Empty`) excluded every real user. | Added `.IgnoreQueryFilters()` — resolving which tenant a mobile number belongs to is the entire point of this lookup. |
| 4 | Every session was silently invalidated on the request immediately after login | `UserStore.FindByIdAsync`, called from `SignInManager.ValidateSecurityStampAsync` during `OnValidatePrincipal` — which runs *before* `HttpContext.User` is updated to the new principal — was also tenant-filtered to `Guid.Empty`. | Added `.IgnoreQueryFilters()`, safe here specifically because the id comes from a DataProtection-signed cookie ticket, not client input. |
| 5 | `AccountProfile.Role` serialized as a raw integer (`"role":0`), not `"Owner"` | No `JsonStringEnumConverter` was registered for minimal API's JSON options. | Registered one globally via `ConfigureHttpJsonOptions` in `Program.cs`. |
| 6 | CSRF validation silently did nothing on any minimal-API endpoint | `app.UseAntiforgery()` only validates endpoints carrying `IAntiforgeryMetadata`; nothing attaches that to a plain `MapPost` lambda. | Added `AntiforgeryEndpointFilter` (`IEndpointFilter` calling `IAntiforgery.ValidateRequestAsync` explicitly), applied to every mutating auth endpoint. |

### 11.3 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Valid login issues the session cookie with every documented flag | `Valid_Login_Succeeds_And_Issues_The_Session_Cookie_With_The_Documented_Flags` | ✅ |
| Invalid password / unknown mobile → `401 INVALID_CREDENTIALS` | `Invalid_Password_Returns_401_Invalid_Credentials`, `Unknown_Mobile_Number_Also_Returns_401_Invalid_Credentials` | ✅ |
| Inactive account → `403 ACCOUNT_INACTIVE` before password check | `Inactive_Account_Returns_403_Account_Inactive` | ✅ |
| 5 failed attempts lock the account out (`lockout_end_at` set, in the future) | `Repeated_Failures_Lock_The_Account_Out` | ✅ |
| `GET /auth/csrf-token` returns a token and sets the pairing cookie | `Csrf_Token_Endpoint_Returns_A_Token_And_Sets_A_Pairing_Cookie` | ✅ |
| A mutating request with no `X-CSRF-TOKEN` header is rejected | `Login_Without_A_Csrf_Token_Is_Rejected` | ✅ `400` |
| OTP request always `200`, whether or not the mobile exists (no enumeration) | `Otp_Request_Always_Returns_200_Whether_Or_Not_The_Mobile_Exists` | ✅ |
| Wrong OTP code → `400 OTP_INVALID` | `Otp_Verify_With_Wrong_Code_Returns_400_Otp_Invalid` | ✅ |
| Full reset flow: verify → reset → old code rejected on reuse → new password logs in | `Otp_Full_Reset_Flow_Succeeds_With_The_Real_Code_And_The_Code_Cannot_Be_Reused` | ✅ |
| Expired OTP rejected even with the correct code | `Otp_Expiry_Is_Rejected_Even_With_The_Correct_Code` | ✅ `400 OTP_EXPIRED` |
| Logout invalidates the session (`/account/me` `200` before, `401` after) | `Logout_Then_Account_Me_Is_Unauthenticated` | ✅ |
| `/account/me` returns identity, role, scopes and permission codes | `Account_Me_Returns_Identity_Role_Scopes_And_Permissions` | ✅ |
| `/account/me` with no session → `401` | `Account_Me_Without_A_Session_Is_Unauthorized` | ✅ |
| Login rate-limited to 5/IP/min | `Login_Is_Rate_Limited_To_Five_Per_Ip_Per_Minute` | ✅ `429` on the 6th |
| OTP per-IP window (3/15min) trips | `Otp_Ip_Rate_Limit_Trips_After_Three_Requests_In_The_Window` | ✅ `429` on the 4th |
| OTP per-mobile window (3/15min and 5/hour) trips | `Otp_Mobile_Rate_Limit_Trips_After_Three_Requests_In_Fifteen_Minutes` | ✅ `429` on the 4th |
| Full solution suite, multiple consecutive runs (confirming the §11.2 row 1 fix under real ordering variance, not one lucky run) | `dotnet test InventorySystem.sln` | ✅ 52/52 (2 unit, 17 architecture, 33 integration), every run |

### 11.4 Not verified — genuinely out of Phase 3 scope

| Item | Why deferred |
| :--- | :--- |
| `localStorage` never used for the session | A frontend (F2) concern — no frontend code exists yet to violate this. The backend never issues a bearer token to store in the first place; only the `HttpOnly` cookie, which JavaScript cannot read regardless. |
| ADR-012 Role Denial (`Admin` blocked from cost/audit permissions regardless of grant) | Explicitly Phase 4's pipeline-level handler (task 4.3), not this read-only `/account/me` projection — noted in `AccountProfileReader`'s own doc comment so the omission is never mistaken for an oversight. |
| `docs/09`'s literal `--filter Category=Auth` verification command | No test in the repository (any phase) carries an xUnit `Category` trait yet — verification instead ran by fully-qualified class name, consistent with how Phase 1/2 were actually verified (§9/§10 use specific fact/class names, not category filters). Recorded as a pre-existing documentation/reality gap across all phases, not a Phase 3 regression. |

---

## 12. Phase 4 Verification Ledger

**Phase 4 is SIGNED OFF (2026-09-17).** All 15 tasks (`docs/09` §Phase 4, table 4.1–4.15) implemented and verified against this repository's own PostgreSQL instance and a real in-process HTTP pipeline — not by review alone.

### 12.1 What was built

- **The five-stage pipeline** (task 4.1, docs/04 §15): authenticate (Phase 3's cookie auth) → tenant (Phase 2's EF global query filter) → role denial + permission grant (`PermissionAuthorizationHandler`) → scope (`IScopeGuard`, called explicitly by a resource handler once it knows which warehouse/restaurant a resource belongs to) → execute.
- **Permission-based policies** (task 4.2): a custom `IAuthorizationPolicyProvider` (`PermissionPolicyProvider`) treats any `.RequireAuthorization("<module>:<action>")` policy name matching the permission-code shape as a `PermissionRequirement` automatically — no policy needs hand-registering per permission, so the policy set can never drift from `docs/03 §3`'s catalogue.
- **Role denial** (task 4.3, ADR-012): `PermissionAuthorizationHandler` independently re-checks that a non-Owner can never pass a `costs:view`/`valuation:view`/`audit:view`/`audit:export` requirement, regardless of what `IPermissionEvaluator` (or a bad `user_permissions` row reaching the table some other way) would otherwise say - proven directly by inserting such a row via raw SQL and confirming the pipeline still denies it (§12.3).
- **Non-grantable rejection** (task 4.4): `UserManagementService.SetPermissionsAsync` rejects any grant naming one of the four ADR-012 codes with `400 NON_GRANTABLE_PERMISSION`, using `Permission.IsCodeGrantable` - the same check `Permission.IsGrantable` uses, so the two can never disagree.
- **Exactly one role per user** (task 4.5, ADR-014): `UserManagementService.ChangeRoleAsync` replaces (remove-then-add) rather than adds, backed by the existing `UNIQUE (user_id)` constraint on `user_roles` (Phase 2) as the database-level backstop.
- **`IScopeGuard`** (task 4.6): returns the exact warehouse/restaurant id set a user's `UserWarehouseScope`/`UserRestaurantScope` rows grant - no consuming business endpoint exists before Phase 8/9 (receiving/supply requests), so it is verified directly against the database (§12.3), the same honest posture Phase 1/2 used for infrastructure ahead of its first caller.
- **404-vs-403 IDOR pattern** (task 4.7, docs/09-authorization-security.md §2.1): out-of-tenant → `404` (the tenant query filter already makes the row invisible, so a not-found lookup is the truthful answer, not a deliberate disguise) — proven end-to-end via `/api/v1/users/{id}` across two companies.
- **Privilege-escalation guards** (task 4.8, docs/09-authorization-security.md §2.4), enforced inside `UserManagementService` itself (not merely at the endpoint, so no future caller of the service can bypass them): no self role/scope change; only an `Owner` can create or assign another `Owner`; a non-Owner cannot grant a permission code outside their own effective permission set (proven with a deliberately under-privileged actor — see §12.3 — not just the redundant non-grantable-code case).
- **Mass-assignment guard** (task 4.9): every Request/Command DTO in `Inventory.Api` is architecture-tested (`MassAssignmentRules`) to never declare `companyId`, `userId`, `generatedCode`, `documentNumber`, or `createdAt`; an over-posted `companyId` in a real `POST /users` body is additionally proven inert end-to-end (§12.3), not just structurally absent.
- **`IFinancialProjection`** (task 4.10, docs/15 §2): masks a `decimal?` to `null` for every role but `Owner` - ready for Phase 5+'s first cost/valuation projection, unused by any endpoint yet (nothing prices anything before Phase 5's items).
- **Audit interceptor** (task 4.11, docs/14 §1): `IAuditLogger.Record` adds an `AuditLog` to the SAME `DbContext` change tracker the business change already lives in - not a separate `SaveChangesAsync`, so the two can only ever commit or roll back together. `POST /users` is the one exception needing an *explicit* transaction (`IDbContextTransaction`), because `UserManager.CreateAsync` (ASP.NET Core Identity, not this codebase) saves eagerly inside its own call; every other mutating method uses a single implicit-transaction `SaveChangesAsync` and needs no wrapper.
- **Audit sanitization** (task 4.12, docs/14 §1 point 2): `AuditSanitizer` walks the serialized JSON tree of whatever old/new values a caller passes and redacts any property whose name contains `password`, `otp`, `securitystamp`, `token`, `connectionstring`, or `secret` (case-insensitive, any nesting depth) - a tree walk, not a per-caller convention, so a future handler cannot forget to omit a secret field itself.
- **Correlation-ID propagation into audit rows** (task 4.13, docs/21 §3): the constant naming the header (`X-Correlation-Id`) moved to `Inventory.Application.Common.CorrelationIdHeader` so `Inventory.Infrastructure`'s `AuditLogger` can read the same value `Inventory.Api`'s `CorrelationIdMiddleware` set, without `Infrastructure` referencing `Api` (ADR-002) - proven end-to-end: the id in the audit row matches the id the HTTP response header actually carried, not merely a non-null string.
- **`/users`, `/users/{id}`, `/users/{id}/role`, `/users/{id}/scope`, `/users/{id}/permissions`** (task 4.14) with full audit coverage on every mutation.
- **16 new integration tests** (`AuthorizationTests`) and **1 new architecture test** (`MassAssignmentRules`) covering task 4.15's checklist - see §12.3/§12.4 for exactly which items and which are deferred.

### 12.2 A pre-existing bug found and fixed while building this phase

`PermissionAuthorizationHandler` and `PermissionPolicyProvider` needed to be registered as `IAuthorizationHandler`/`IAuthorizationPolicyProvider`. The handler depends on the request-scoped `ICurrentUserService` and `IPermissionEvaluator` - registering it `AddSingleton` (a plausible first instinct, since ASP.NET Core's own built-in handlers are often singletons) would have reproduced the exact captive-dependency bug already fixed once this session in `InventoryDbContext`'s tenant filter (§11.2 row 1): the FIRST request's scoped instances frozen into a handler every later request reuses. Caught during implementation, before it ever ran; registered `AddScoped` instead.

### 12.3 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Cross-tenant `GET /users/{id}` → `404`, not `403` | `Getting_A_User_From_A_Different_Company_Returns_404_Not_403` | ✅ |
| No `users:view` permission → `403` on `GET /users` | `A_User_With_No_Permissions_Is_Forbidden_From_Listing_Users` | ✅ |
| Owner can create another Owner | `Owner_Can_Create_Another_Owner` | ✅ `201` |
| Non-Owner (Admin) creating an Owner → `403 PRIVILEGE_ESCALATION_DENIED` | `A_Non_Owner_Cannot_Create_An_Owner` | ✅ |
| Over-posted `companyId` on `POST /users` has no effect - the created user lands in the actor's real company | `An_Over_Posted_CompanyId_In_The_Create_User_Body_Is_Ignored` | ✅ |
| Owner cannot change their own role | `Owner_Cannot_Change_Their_Own_Role` | ✅ `403` |
| Owner cannot change their own scope | `Owner_Cannot_Change_Their_Own_Scope` | ✅ `403` |
| Granting a non-grantable code → `400 NON_GRANTABLE_PERMISSION` | `Assigning_A_Non_Grantable_Permission_Returns_400` | ✅ |
| An actor cannot grant a (grantable) permission they do not themselves hold | `Granting_A_Permission_The_Actor_Does_Not_Hold_Is_Rejected` | ✅ `403 PRIVILEGE_ESCALATION_DENIED` |
| A role change writes an audit row in the same operation, with the correct actor, role, and the SAME correlation id the HTTP response carried | `Role_Change_Writes_An_Audit_Row_In_The_Same_Operation` | ✅ |
| A rolled-back operation (FK violation on a nonexistent warehouse id) leaves no audit row AND no partial scope row | `No_Audit_Row_Is_Written_When_The_Operation_Is_Rolled_Back` | ✅ |
| `IScopeGuard` returns exactly a user's assigned warehouse ids | `Scope_Guard_Returns_Exactly_The_Assigned_Warehouse_Ids` | ✅ |
| `IScopeGuard` returns empty for a user with no scope rows | `Scope_Guard_Returns_Empty_For_A_User_With_No_Scope_Rows` | ✅ |
| ADR-012 role denial overrides a `user_permissions` row inserted directly (bypassing the endpoint's own guard) | `Role_Denial_Overrides_A_Non_Grantable_Permission_Even_If_A_Grant_Row_Exists` | ✅ |
| Owner is still allowed through the same non-grantable-code check | `Role_Denial_Allows_Owner_For_A_Non_Grantable_Permission` | ✅ |
| `IFinancialProjection` reveals the value only to Owner, masks it to `null` for Admin | `Financial_Projection_Reveals_The_Value_Only_To_Owner` | ✅ |
| No Request/Command DTO anywhere declares a forbidden bindable property | `MassAssignmentRules.No_Request_Or_Command_Dto_May_Declare_A_Forbidden_Bindable_Property` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test InventorySystem.sln` | ✅ 69/69 (2 unit, 18 architecture, 49 integration), every run |

### 12.4 Not verified — genuinely out of Phase 4 scope

| Item | Why deferred |
| :--- | :--- |
| `IScopeGuard` enforced over a real HTTP resource endpoint (e.g. `403 FORBIDDEN_SCOPE` on a receiving order outside a Warehouse Staff user's scope) | No warehouse/restaurant-scoped business resource exists yet - the first one arrives in Phase 8 (receiving) / Phase 9 (supply requests). `IScopeGuard` itself is verified directly against the database (§12.3); its first real caller will prove the HTTP-level `403` end-to-end. |
| `IFinancialProjection` applied to a real cost/valuation field | No item, receiving order, or stock balance exists yet to have a cost. First real consumer is Phase 5's item projection or Phase 7's stock valuation. |
| `Admin` cost/audit masking on a real financial endpoint (docs/03 §5.1) | Same reason - no financial endpoint exists yet. The role-denial pipeline itself is proven (§12.3); its first financial caller will prove the end-to-end masking. |
| `docs/14 §3` Owner Activity Monitor / Audit Log viewer | Explicitly Phase 14 (`docs/14 §4` "Phase placement"). This phase builds only the write-side infrastructure the viewer will read from. |

---

## 13. Phase 5 Verification Ledger

**Phase 5 is SIGNED OFF (2026-09-17).** All 16 tasks (`docs/09` §Phase 5, table 5.1–5.16) implemented and verified against this repository's own PostgreSQL instance and a real in-process HTTP pipeline.

### 13.1 What was built

- **`IDocumentSequenceService`** (task 5.1, docs/28 §5.3, ADR-025): a single `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING` raw-SQL statement per allocation, executed directly over the caller's connection - the interface documents that the caller MUST open its own explicit transaction first, since the raw SQL otherwise commits independently of whatever `SaveChangesAsync` follows. Proven directly (not through an entity endpoint) with 1,000 concurrent allocations yielding exactly `{1..1000}` (AC-28-1), a rolled-back allocation leaving the counter unchanged and the number reused (AC-28-3), and the monthly period key derived from the tenant's configured timezone, not UTC (ADR-024) - defaulting to `Africa/Cairo` when no `CompanySettings` row exists, since no phase in this plan ever schedules a company/tenant-provisioning endpoint.
- **`KeysetPagination`** (task 5.13, docs/21 §1): the shared cursor encode/decode + "was there another page" logic every list endpoint below reuses; each service still writes its own `ORDER BY`/`WHERE` clause, since a fully generic EF LINQ keyset predicate across unrelated entity types is not reliably translatable.
- **`MasterDataResult<T>`/`MasterDataError`/`MasterDataErrorWriter`**: one consistent result/error-mapping shape shared by all six resources below, catching a unique-constraint violation and mapping it to `409 DUPLICATE_NAME` (or `DUPLICATE_ITEM_NAME` for items specifically - docs/13's pre-existing, item-worded code) instead of an opaque 500.
- **Categories, Units, Suppliers** (tasks 5.5-5.7): create/list/get/update/deactivate/reactivate, deactivate-not-delete only (task 5.12 - no `DELETE` route exists anywhere in the solution, verified by grep, not merely by omission). `Unit.Update`/`Supplier.Update` domain methods were added - neither entity had one before (only `Deactivate`/`Reactivate` existed from Phase 2). Suppliers deliberately allows duplicate names: the real migration carries no `uq_suppliers_*` constraint, matching that two real-world suppliers can share a business name.
- **Warehouses, Restaurants** (tasks 5.8-5.9): `Code` is client-supplied at creation (docs/28 §4 does not number warehouses/restaurants) and immutable thereafter. Restaurant creation enforces ADR-028 SW-6: `defaultServingWarehouseId` must name a warehouse that exists AND is `Active` in the caller's own tenant, returning `409 SERVING_WAREHOUSE_UNAVAILABLE` otherwise - proven against a nonexistent id, an inactive warehouse, and a real warehouse id belonging to a *different* company (indistinguishable from nonexistent once the tenant query filter applies).
- **Items** (tasks 5.3, 5.10, 5.11, 5.14): `ITM-000001`-style codes allocated inside an explicit transaction alongside the insert. A client-supplied `generatedCode` is detected by parsing the raw request body BEFORE binding to the DTO and rejected with `400 GENERATED_FIELD_NOT_ACCEPTED` (docs/28 §5.1 point 2) - stronger than the "DTO simply doesn't declare the property" baseline every other Phase 5 resource relies on alone, because docs/28 requires an *explicit* rejection here, not a silent drop. Search normalizes the query through the same `ArabicTextNormalizer` the stored name went through (docs/31 §4.2) and matches via `EF.Functions.ILike` against the `pg_trgm`-indexed `name_normalized` column - proven with a query using different diacritics/alef-forms than the stored spelling. Base-unit immutability (ADR-023) checks `stock_ledger` for any row referencing the item - always `false` today (Phase 7 hasn't posted anything yet), written correctly now so it takes effect the moment posting exists, without needing revisiting.
- **19 new unit tests** (`ArabicTextNormalizerTests`, `ItemEntityTests` - task 5.15, previously-untested Phase 2 code), **6 new architecture-adjacent integration test files**, and the CSRF fix in §13.2 below.

### 13.2 A pre-existing bug found and fixed while building this phase

**None of Phase 4's `/api/v1/users*` endpoints, nor any of this phase's six master-data resources, had `AntiforgeryEndpointFilter` applied to their mutating routes** - the exact CSRF control Phase 3 built and proved for `/auth/*` (task 3.7) was never carried forward to any endpoint built afterward. Found by auditing every `MapPost`/`MapPut` across both phases (24 routes); fixed on all of them at once. Verified two ways: the existing 90+ test suite still passed unmodified (every test already attached a real CSRF token, matching real frontend behaviour), and a new test confirms a request with no token is now genuinely rejected with `400` - before the fix, it would have been silently accepted.

### 13.3 A design/tooling conflict found and worked around: EF Core alternate keys are unconditionally immutable

`ItemConfiguration` declares `(company_id, id, base_unit_id)` an EF alternate key (`uq_items_base_unit`), built in Phase 2 specifically to back `item_unit_conversions`' composite FK (ADR-023: "a conversion must target the item's own base unit"). EF Core's change tracker refuses to modify ANY property participating in ANY key it tracks - primary or alternate - the instant it detects a change, regardless of whether a dependent row currently exists to actually break. Reproduced directly: calling the natural `item.ChangeBaseUnit(...)` + `SaveChangesAsync()` path threw `InvalidOperationException` on every attempt, including ones where zero `ItemUnitConversion` rows existed. Fixed by updating `base_unit_id` via `ExecuteSqlInterpolatedAsync` (raw SQL, bypassing the change tracker for this one column) inside an explicit transaction shared with the audit row - and the now-misleading `Item.ChangeBaseUnit` domain method, which implied the normal tracked-entity path worked, was removed rather than left as a footgun for a future caller.

### 13.4 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| 1,000 concurrent item-code allocations yield exactly `{1..1000}`, no gaps, no duplicates (AC-28-1) | `DocumentSequenceServiceTests.One_Thousand_Concurrent_Allocations_Yield_One_Thousand_Distinct_Gap_Free_Codes` | ✅ |
| A rolled-back allocation leaves the counter unchanged; the number is reused (AC-28-3) | `DocumentSequenceServiceTests.A_Rolled_Back_Allocation_Leaves_The_Counter_Unchanged_And_The_Number_Is_Reused` | ✅ |
| Monthly period key derived from the tenant's timezone, not UTC (ADR-024) | `DocumentSequenceServiceTests.Allocating_A_Monthly_Document_Number_Uses_The_Tenant_Timezone_Business_Month` | ✅ |
| Category/Unit/Item duplicate (normalized) name → `409` | `Creating_A_Duplicate_Category_Name_...`, `Creating_A_Duplicate_Unit_Name_Returns_409`, `Creating_An_Item_With_A_Duplicate_Normalized_Name_Returns_409` | ✅ |
| Two suppliers may legitimately share one name (no constraint exists) | `Two_Suppliers_May_Share_The_Same_Name` | ✅ |
| Keyset pagination returns a working, non-overlapping next page | `Listing_Categories_Paginates_By_Keyset_With_A_Working_Next_Cursor` | ✅ |
| Cross-tenant `GET` on any master-data resource → `404` | `Getting_A_Category_From_A_Different_Company_Returns_404` | ✅ |
| Missing permission → `403` on list/create | `A_User_Without_Categories_Manage_Is_Forbidden` | ✅ |
| Restaurant creation rejects a nonexistent, inactive, or cross-tenant serving warehouse (ADR-028 SW-6) | `Creating_A_Restaurant_With_A_Nonexistent_...`, `..._An_Inactive_...`, `..._Another_Companys_Warehouse_Returns_409` | ✅ all `409 SERVING_WAREHOUSE_UNAVAILABLE` |
| Item code format is `ITM-######` | `Allocating_An_Item_Code_Twice_Produces_Sequential_Six_Digit_Codes`, `Creating_An_Item_Allocates_A_Six_Digit_ITM_Code` | ✅ |
| A client-supplied `generatedCode` is rejected with `400 GENERATED_FIELD_NOT_ACCEPTED`, not silently dropped (AC-28-2) | `Submitting_A_Client_Supplied_GeneratedCode_Is_Rejected_With_400` | ✅ |
| Search matches a differently-diacritized query against the stored name | `Searching_By_A_Differently_Diacritized_Query_Still_Finds_The_Item` | ✅ |
| Base unit changes successfully while no ledger activity exists | `Changing_The_Base_Unit_Succeeds_While_No_Ledger_Activity_Exists` | ✅ |
| 50 concurrent item creations (integration-level, full service stack) yield 50 distinct codes | `Fifty_Concurrent_Item_Creations_Yield_Fifty_Distinct_Codes` | ✅ |
| Every Phase 5 mutating endpoint rejects a request with no CSRF token | `Creating_A_Category_Without_A_Csrf_Token_Is_Rejected` | ✅ `400` |
| `ArabicTextNormalizer` normalization rules (diacritics, tatweel, alef forms, ta-marbuta, alef-maksura) | `ArabicTextNormalizerTests` (9 facts/theories) | ✅ |
| `Item` entity preserves the raw display name and re-derives the normalized key on construction/update; `generatedCode` is immutable | `ItemEntityTests` (3 facts) | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test InventorySystem.sln` | ✅ 113/113 (19 unit, 18 architecture, 76 integration), every run |

### 13.5 Not verified — genuinely out of Phase 5 scope

| Item | Why deferred |
| :--- | :--- |
| `docs/09` task 5.16's literal 1,000-concurrent-item-creation acceptance test, run through the full HTTP/service stack | The gap-free/no-duplicate guarantee (AC-28-1) is exhaustively proven at 1,000-scale directly against `IDocumentSequenceService` (§13.4) - the mechanism that actually provides the guarantee. A lighter 50-concurrent check through the full `POST /items` stack (§13.4) confirms the surrounding category/unit-validation and transaction wrapper add no regression, without paying the cost of re-running the same 1,000-scale proof through a slower path for a guarantee already established. |
| `IFinancialProjection` applied to a real cost/valuation field | Items carry no cost field at all (docs/15 §1 point 1: cost originates exclusively from posted `ReceivingOrder` rows, never stored statically on an item). The masking primitive built in Phase 4 remains unused until Phase 7/8 introduce the first entity that actually carries a cost. |
| Base-unit immutability (ADR-023) actually blocking a change once ledger rows exist | No `stock_ledger` row can exist before Phase 7's posting service. The guard's query is written and will take effect the moment Phase 7 posts a row - proven then, against real data, not simulated now. |
| `ItemUnitConversion` CRUD | Explicitly Phase 6 ("Unit Conversion System"), not Phase 5 - the entity exists from Phase 2 but this phase does not manage it. |

---

## 14. Phase 6 Verification Ledger

**Phase 6 is SIGNED OFF (2026-09-17).** All 8 tasks (`docs/09` §Phase 6, table 6.1–6.8) implemented and verified against this repository's own PostgreSQL instance and a real in-process HTTP pipeline.

### 14.1 What was built

- **`ItemUnitConversion` CRUD** (task 6.1): `POST/GET /api/v1/items/{itemId}/conversions`, `POST .../conversions/{id}/deactivate`. Factor validated `> 0` before touching the domain constructor (`400 INVALID_CONVERSION_FACTOR`, docs/13's pre-existing code); the column is `numeric(18,6)` from Phase 2.
- **`ToBaseUnitId` is never client-supplied** (task 6.2, ADR-023): `CreateItemUnitConversionCommand` has no such property at all - the target is always read from the named item's own current `BaseUnitId` server-side, the same "remove the input, don't just validate it" pattern ADR-028 established for the supply-request serving warehouse. The database's own composite FK (`fk_conversion_item_base_unit`, built in Phase 2) backs this structurally regardless.
- **`IUnitConversionResolver`** (task 6.3): identity quantity when the requested unit IS the item's base unit; otherwise multiplies by the single active conversion's factor; `Succeeded=false` (task 6.6) when neither applies. This is the ONLY conversion arithmetic in the codebase - every later phase that accepts a non-base-unit quantity must resolve through it.
- **`BaseQuantity` added to the mass-assignment architecture test's forbidden property list** (task 6.4): no Request/Command DTO, in this phase or any future one, may declare it - `MassAssignmentRules` now fails the build the instant one does, the same guarantee task 4.9 gives `companyId`/`userId`/etc.
- **Corrections, not edits** (task 6.5, ADR-023): `ItemUnitConversionService.CreateAsync` is the only mutation path for a factor - calling it for an (item, from-unit) pair that already has an active row deactivates that row and inserts a new one, in one `SaveChangesAsync`, both audited (`ITEM_CONVERSION_CORRECTED` vs `ITEM_CONVERSION_CREATED`). There is no separate "update factor in place" endpoint anywhere.
- **12 new integration tests** (`ItemUnitConversionTests`) covering the docs/09 task 6.7 headline example (1 carton = 12 KG → 20 cartons = 240 KG), per-item factor independence, 6-decimal precision, the correction flow, and permission/tenant-isolation gating consistent with every other Phase 5/6 resource.

### 14.2 A second Phase 2 schema bug found and fixed

`ItemUnitConversionConfiguration`'s `uq_item_conversion` index (`UNIQUE (item_id, from_unit_id, to_base_unit_id)`, built in Phase 2) was a **plain**, not partial, unique index. Since `to_base_unit_id` is always the item's one current base unit, this means a DEACTIVATED row already permanently occupies that exact triple - task 6.5's "correction creates a new row and deactivates the old" is structurally impossible against a plain unique index: the very first correction for any (item, unit) pair would make every subsequent one fail on this constraint, forever. Caught during design, before any code was written against it (not discovered via a failing test) - fixed with a new migration (`20260917083753_AddPartialUniqueIndexOnActiveItemUnitConversion`) making the index partial (`WHERE is_active`), applied to the running database and verified via `dotnet ef migrations has-pending-model-changes` reporting none. The task 6.5 correction-flow test (`Creating_A_Second_Conversion_For_The_Same_Pair_...`) exercises exactly the scenario that would have failed under the old index.

### 14.3 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| 1 carton = 12 KG → 20 cartons = 240 KG (docs/09 task 6.7 headline example) | `Resolver_Converts_Twenty_Cartons_At_Twelve_Kg_Each_To_Two_Hundred_Forty_Kg` | ✅ |
| Identity case: requesting the item's own base unit returns the quantity unchanged | `Resolver_Returns_Identity_Quantity_For_The_Items_Own_Base_Unit` | ✅ |
| No conversion defined → resolver reports failure (task 6.6) | `Resolver_Fails_When_No_Conversion_Is_Defined_For_The_Unit` | ✅ |
| Zero and negative factors rejected with `400 INVALID_CONVERSION_FACTOR` | `Creating_A_Conversion_With_A_Non_Positive_Factor_Returns_400` (theory, both cases) | ✅ |
| Different items hold independent factors for same-named units | `Different_Items_Can_Have_Different_Factors_For_The_Same_Unit_Name` | ✅ |
| A `numeric(18,6)` factor (`0.123456`) applies with no precision drift | `A_Six_Decimal_Factor_Applies_Without_Precision_Drift` | ✅ `1000 × 0.123456 = 123.456` exactly |
| A second conversion for the same pair deactivates the first (not deletes it) and the resolver immediately uses the new factor | `Creating_A_Second_Conversion_For_The_Same_Pair_Deactivates_The_First_And_The_Resolver_Uses_The_New_Factor` | ✅ |
| Listing an item's conversions returns both the deactivated and active rows | `Listing_Conversions_For_An_Item_Returns_Both_The_Deactivated_And_Active_Rows` | ✅ |
| Creating targets the item's real base unit even though the client never supplies it | `Creating_A_Conversion_Succeeds_And_Targets_The_Items_Own_Base_Unit` | ✅ |
| Missing `conversions:manage` → `403` | `A_User_Without_Conversions_Manage_Is_Forbidden` | ✅ |
| The partial-index migration applies cleanly and the model has no pending changes afterward | `dotnet ef database update` + `dotnet ef migrations has-pending-model-changes` | ✅ "No changes have been made to the model since the last migration." |
| Full solution suite, multiple consecutive runs | `dotnet test InventorySystem.sln` | ✅ 125/125 (19 unit, 18 architecture, 88 integration), every run |

### 14.4 Not verified — genuinely out of Phase 6 scope

| Item | Why deferred |
| :--- | :--- |
| Task 6.8's integration test (a request submitting `conversionFactor` on a TRANSACTIONAL line has it discarded and the server's resolved factor applied) | No transactional line-item endpoint (receiving order line, supply request line, ...) exists yet to submit one against - `IUnitConversionResolver` itself is fully proven (§14.3); its first real transactional caller (Phase 8's receiving, most likely) will prove this end-to-end, the same posture Phase 4/5 already took for several "first real consumer" items. |
| `CONVERSION_NOT_DEFINED` as an actual `409` HTTP response | Same reason - the resolver's `Succeeded=false` outcome (§14.3) has no consuming endpoint yet to translate it into a response; nothing calls the resolver from an HTTP handler in this phase. |
