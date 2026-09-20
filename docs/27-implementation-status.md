# 27 — Progress & Implementation Status Tracking

> **Document ID:** SPEC-27
> **Status:** Live Implementation Register
> **Current State:** **Backend Phases P0–P14, S1 (backend), and T1 (backend) are all CLOSED**, and **frontend Phases F1 through F6 are all CLOSED (F6 signed off 2026-09-18)** - every frontend phase in docs/09 is now complete. Full backend suite: 220/220 passing, stable across repeated consecutive runs. Frontend: clean typecheck/lint/production build, verified against a live Development backend with real cross-role (Owner/Admin/Warehouse Staff/Restaurant Supervisor) manual E2E walkthroughs. Checkpoints 2 through 6 are all reached. See §25–§30 for the F1–F6 verification ledgers, §16–§24 for the backend phase ledgers, and §31 for a post-F6 reconciliation sweep that found and closed two real, previously-undetected end-to-end gaps despite every phase above being individually signed off. **Phase S1 (frontend half), T1 (frontend half), and T2 (Browser E2E & Acceptance) are next**, followed by R1 (release readiness).
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
| **P7** | Stock engine, transactions, concurrency, idempotency | ✅ **COMPLETE** | All 15 tasks (7.1–7.15) done and verified. No business endpoint exposed (by design). See §15 for full detail. | ✅ Signed off 2026-09-17 |
| **P8** | Receiving & warehouse stock ledger | ✅ **COMPLETE** | All 13 tasks (8.1–8.13) done and verified. See §16 for full detail. | ✅ Signed off 2026-09-17 |
| **P9** | Multi-item supply requests | ✅ **COMPLETE** | All 14 tasks (9.1–9.14) done and verified. See §17 for full detail. | ✅ Signed off 2026-09-17 |
| **P10** | Fulfilment & dispatch | ✅ **COMPLETE** | All 8 tasks (10.1–10.8) done and verified. See §18 for full detail. | ✅ Signed off 2026-09-17 |
| **P11** | Restaurant receipt confirmation | ✅ **COMPLETE** | All 11 tasks (11.1–11.11) done and verified. Highest-consequence module - see §19 for full detail. | ✅ Signed off 2026-09-17 |
| **P12** | Discrepancies & reconciliation | ✅ **COMPLETE** | All 7 tasks (12.1–12.7) done and verified. See §20 for full detail. | ✅ Signed off 2026-09-17 |
| **P13** | Physical stock counts & adjustments | ✅ **COMPLETE** | All 11 tasks (13.1–13.11) done and verified. See §21 for full detail. | ✅ Signed off 2026-09-17 |
| **P14** | Audit viewer & activity monitor | ✅ **COMPLETE** | All 5 tasks (14.1–14.5) done and verified. See §22 for full detail. | ✅ Signed off 2026-09-17 |
| **P15** | File storage & evidence | 🚫 **DEFERRED** | ADR-029. Not scheduled. Activates only if an approved workflow requires an attachment. | — |
| **P16** | Reporting & analytics | 🚫 **DEFERRED** | ADR-029. Not scheduled. `docs/17 §4` extension points remain **binding on the core phases**. | — |
| **F1** | Frontend shell & design system | ✅ **COMPLETE** | All 12 tasks (F1.1–F1.12) done and verified. See §25 for full detail. | ✅ Signed off 2026-09-17 |
| **F2** | Frontend auth & app shell | ✅ **COMPLETE** | All F2 deliverables (login, forgot-password/OTP/reset, session context, per-role nav, authenticated shell) done and verified, including a critical cookie-policy bug found and fixed via real-browser E2E testing. See §26 for full detail. | ✅ Signed off 2026-09-18 |
| **F3** | Frontend master-data workflows | ✅ **COMPLETE** | All F3 deliverables (categories/units/suppliers/warehouses/restaurants/items screens, quick-add modal, Arabic-normalized search, nested unit conversions) done and verified, including three real bugs found and fixed via live-browser testing. See §27 for full detail. | ✅ Signed off 2026-09-18 |
| **F4** | Frontend receiving & inventory | ✅ **COMPLETE** | Receiving list/draft editor/submit/verify/reverse and the warehouse stock view done and verified against a live backend, reproducing docs/23 Scenarios 1-2's numerical math exactly end to end. See §28 for full detail. | ✅ Signed off 2026-09-18 |
| **F5** | Frontend supply workflows | ✅ **COMPLETE** | Multi-item request editor, fulfilment, dispatch, confirmation, and discrepancy views done and verified end-to-end (cross-role: Restaurant Supervisor + Warehouse Staff) against a live backend. Also fixed a real backend access gap affecting F4 too (§29). See §29 for full detail. | ✅ Signed off 2026-09-18 |
| **F6** | Role-specific dashboards | ✅ **COMPLETE** | Warehouse Staff/Restaurant Supervisor/Owner/Admin dashboards done and verified against a live backend as all three roles, every widget a real API count. Also found and fixed a pre-existing Button/Slot crash from Phase F1. See §30 for full detail. | ✅ Signed off 2026-09-18 |
| **S1** | Security hardening & adversarial regression | ✅ **COMPLETE (backend)** | Headers, U+202E stripping, dependency/secret scans done; TLS/npm audit deferred to deployment/frontend. See §23 for full detail. | ✅ Signed off 2026-09-17 |
| **T1** | Full automated test sweep | ✅ **COMPLETE (backend)** | Traceability review closed REQ-07 (consumption logging) gap; frontend half pending F1-F6. See §24 for full detail. | ✅ Signed off 2026-09-17 |
| **T2** | Browser E2E & acceptance | ⏳ Pending | Arabic export verified visually. | — |
| **R1** | Documentation & release readiness | ⏳ Pending | Verified, not asserted. | — |

---

## 3. Checkpoint Register

| Checkpoint | Covers | Status |
| :---: | :--- | :---: |
| **1** | Architecture & Database (P1–P2) | ✅ **REACHED — signed off 2026-09-17** |
| **2** | Identity & Authorization (P3–P4, F1–F2) | ✅ **REACHED — signed off 2026-09-18** |
| **3** | Master Data & Conversions (P5–P6, F3) | ✅ **REACHED — signed off 2026-09-18** |
| **4** | Stock Engine & Receiving (P7–P8, F4) | ✅ **REACHED — signed off 2026-09-18** |
| **5** | Supply Workflow (P9–P11, F5) | ✅ **REACHED — signed off 2026-09-18** |
| **6** | Full Operational Product (P12–P14, F6) | ✅ **REACHED — signed off 2026-09-18** |
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

---

## 15. Phase 7 Verification Ledger

**Phase 7 is SIGNED OFF (2026-09-17).** All 15 tasks (`docs/09` §Phase 7, table 7.1–7.15) implemented and verified against this repository's own PostgreSQL instance. **No business endpoint is exposed in this phase** - docs/09 states this explicitly, and it holds: every piece below is infrastructure Phase 8+ will call, not something a client can reach yet.

### 15.1 What was built

- **`StockLedgerEntry`/`StockBalance`** (tasks 7.1-7.2): already existed from Phase 2 with the append-only/non-negative invariants docs/09 asks for - this phase's own job on them was making sure nothing else could write to them (§15's task 7.13) and building the one service that legitimately does.
- **`ICostingEngine`** (task 7.7, ADR-020): pure, dependency-free arithmetic - `RecomputeWeightedAverage` for `OpeningBalance`/`IncomingPosted`/`IncomingReconciliation` (guarding the zero-quantity divide-by-zero case a full reversal produces), `ValueAtCurrentCost` for `RestaurantReceiptConfirmed`/`PhysicalAdjustment`. Unit-tested in complete isolation, matching ADR-020's own "Consequences" line.
- **`IStockPostingService`** (tasks 7.3-7.6): the sole writer of `stock_ledger`/`stock_balances`, enforced by a new text-scanning architecture test (`StockPostingRules`, mirroring `CompositionRootRules`). Deductions use docs/30 §5.1's conditional atomic `UPDATE ... WHERE quantity >= @qty` via raw SQL (`InsufficientStockException` on zero affected rows); inbound/adjustment movements go through the normal tracked-entity `SaveChanges` path, which the `xmin` concurrency token (configured in Phase 2) already protects with zero extra code. Lines are sorted ascending by `item_id` internally before processing, so no future caller can forget the mandatory lock-ordering rule (task 7.6).
- **`IIdempotencyService`** (tasks 7.8, 7.10, docs/30 §6.2): SHA-256 canonical-request hashing, scoped to `(company_id, user_id)`. `RecordResponse` only adds to the current unit of work - never saves itself - so the cached response commits or rolls back with the SAME transaction as the business change it guards; a concurrent duplicate's second `INSERT` fails on `uq_idempotency_key` (proven directly).
- **`IdempotencyMiddleware`** (task 7.9): acts only on endpoints carrying `IdempotencyMetadata` via a new `.RequireIdempotencyKey()` convention-builder extension (mirroring `.RequireAuthorization()`). Enforces docs/30 §6.1's required/optional matrix and replays a cached response verbatim; deliberately does NOT record a response itself, since only the handler that produced a role-projected DTO can decide what is safe to cache (task 7.10) - generic middleware outside that handler's transaction cannot determine this correctly.
- **`MaintenanceBackgroundService`** (task 7.11): runs at host startup and every 24h - deletes expired (>24h) idempotency records, and pre-creates next month's `audit_logs` partition (`CREATE TABLE IF NOT EXISTS`, the same naming/boundary convention the Phase 2 migration used for the first two partitions).
- **`IInTransitCalculator`** (task 7.12, ADR-018): derived on demand from `supply_items` whose parent `Supply` is `Dispatched`, attributed to the warehouse, never persisted - proven against a real dispatched `Supply`/`SupplyItem` pair and against a confirmed one (which correctly stops counting).
- **`StockPostingRules`** architecture test (task 7.13) and **27 new unit tests + 34 new integration tests** (tasks 7.14-7.15) across `CostingEngineTests`, `StockPostingServiceTests`, `InTransitCalculatorTests`, `IdempotencyServiceTests`, and `MaintenanceBackgroundServiceTests`.

### 15.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| `500 + 100 = 600`; reconcile `−20 → 580`; confirm `−18 → 562` (docs/09 task 7.14 examples) | `CostingEngineTests` (pure arithmetic, no DB) + `StockPostingServiceTests` (through the real service/DB) | ✅ |
| WAC `100@10 + 50@16 = 12.0000`; WAC unchanged on issue; reconciliation reverses at the line's own cost, not the current WAC | `CostingEngineTests`, `StockPostingServiceTests.Reconciliation_Reverses_At_The_Originating_Lines_Own_Cost_...` | ✅ |
| Deducting 25 from a balance of 20 throws, and writes nothing | `StockPostingServiceTests.Deducting_More_Than_Available_Throws_...` | ✅ |
| Two concurrent deductions of 15 from 20 - exactly one succeeds, balance ends at 5, never negative (AC-30-1) | `StockPostingServiceTests.Two_Concurrent_Deductions_Of_Fifteen_From_Twenty_...` | ✅ |
| A forced `xmin` conflict on WAC recompute raises `DbUpdateConcurrencyException`, not a silently lost update (AC-30-6) | `StockPostingServiceTests.Concurrent_Incoming_Postings_...` | ✅ |
| Two multi-item postings racing in opposite caller-supplied line order never deadlock (AC-30-8) | `StockPostingServiceTests.Interleaved_Multi_Item_Postings_...` (15s timeout guard) | ✅ |
| Only `StockPostingService.cs` writes `stock_ledger`/`stock_balances` (task 7.13) | `StockPostingRules.Only_StockPostingService_May_Write_...` | ✅ |
| A dispatched supply counts as in-transit for its warehouse; a confirmed one no longer does (ADR-018) | `InTransitCalculatorTests` | ✅ |
| A replayed idempotency key with the same payload returns the cached response and performs no work; a different payload is reported as reuse; a key never crosses users even within one company (docs/30 §6.3) | `IdempotencyServiceTests` | ✅ |
| A concurrent duplicate idempotency write - exactly one of two identical inserts succeeds, the other fails on `uq_idempotency_key` (docs/30 §6.2 step 6) | `IdempotencyServiceTests.Concurrent_Duplicate_Requests_...` | ✅ |
| Cleanup removes only expired idempotency records | `IdempotencyServiceTests.Cleanup_Removes_Only_Expired_Records` | ✅ |
| One maintenance pass creates next month's `audit_logs` partition; running it twice does not fail | `MaintenanceBackgroundServiceTests` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test InventorySystem.sln` | ✅ 152/152 (27 unit, 19 architecture, 106 integration), every run |

### 15.3 Not verified — genuinely out of Phase 7 scope

| Item | Why deferred |
| :--- | :--- |
| `IdempotencyMiddleware`/`IStockPostingService`/`IUnitConversionResolver` exercised through a real HTTP endpoint | Docs/09 is explicit: "No business endpoint is exposed in this phase." Every piece is verified directly against the service/database; Phase 8's receiving endpoints are the first real HTTP callers. |
| `400 IDEMPOTENCY_KEY_REQUIRED` / `409 IDEMPOTENCY_KEY_REUSE` as actual HTTP responses | Same reason - no endpoint calls `.RequireIdempotencyKey()` yet. |
| The literal docs/09 task 7.15 "interleaved multi-item deadlock probe" at production-realistic scale (many concurrent multi-line confirmations) | The 2-caller/2-item probe implemented here (§15.2) proves the lock-ordering mechanism is correct in principle - the first real multi-line, multi-item transaction (Phase 11's receipt confirmation) is where a larger-scale probe becomes meaningful, since Phase 7 has no multi-line business operation of its own to stress. |

## 16. Phase 8 Verification Ledger

**Phase 8 is SIGNED OFF (2026-09-17).** All 13 tasks (`docs/09` §Phase 8, 8.1–8.13) implemented and verified end-to-end over real HTTP against this repository's own PostgreSQL instance - the first business endpoints built this session, and the first real consumers of `IStockPostingService`, `IScopeGuard`, `IFinancialProjection`, and `.RequireIdempotencyKey()`.

### 16.1 What was built

- **`IReceivingOrderService`/`ReceivingOrderService`** (tasks 8.1-8.8): orchestrates the `ReceivingOrder`/`ReceivingOrderItem` state machine already built in Phase 2 - `CreateDraftAsync` allocates a `REC-` number (`IDocumentSequenceService`, matching Phase 5's pattern); `AddLineAsync` resolves the base quantity via `ItemUnitConversions` and calls `SetPostedCost` at line-add time (the design decision: cost is captured when a line is entered, not deferred to submit); `SubmitAsync` (T1) posts `INCOMING_POSTED` per line at the *expected* quantity; `VerifyAsync` (T2) posts `INCOMING_RECONCILIATION` for the **delta only** (`actual − expected`), valued at that line's own `unit_cost` (ADR-020) - never the full actual quantity, which docs/09 flags as the single most likely defect in the product; `ReverseAsync` (T3) drives the currently-posted net quantity per line back to zero (also via `IncomingReconciliation`, since a reversal is architecturally a correction to zero - no new `MovementType` was added). CR-041's double-verify guard is the entity's own `RequireStatus`/`Reconciled` checks, exercised end-to-end, not re-implemented here.
- **`TransactionalResult<T>`/`TransactionalError`** (new, `Inventory.Application.Common`): the Phase 8+ sibling of Phase 5/6's `MasterDataResult` - a distinct shape because document state machines and stock-posting failures (`InsufficientStock`, `InvalidStateTransition`, `EmptyDocument`, `ConcurrencyConflict`) share nothing with master-data CRUD failures.
- **`IdempotencyContext`** (new, `Inventory.Application.Common`) plus a same-transaction recording path: task 7.9's `IdempotencyMiddleware` only checks/rejects - it deliberately never records, since "only the handler that produced the response knows what's safe to cache" (docs/30 §6.2 step 6, task 7.10). Phase 8 is the first real handler: `ReceivingOrdersEndpoints` re-reads the exact buffered raw request body (the same bytes the middleware hashed - a re-serialized DTO would never match a future retry's hash) and passes it through to `ReceivingOrderService`, which calls `IIdempotencyService.RecordResponse` and lets the SAME `SaveChangesAsync`/transaction that commits the business change also persist the idempotency record - a concurrent-duplicate race surfaces as `DbUpdateException` on that save, which is caught, rolled back, and resolved by re-`CheckAsync`-ing for the winner's now-committed response (exactly the race handling `IIdempotencyService`'s own doc comment specifies).
- **`ReceivingOrdersEndpoints`** (task 8.9): `POST /receiving-orders`, line add/remove, `/submit`, `/verify`, `/reverse` (all mutating routes carry `.AddEndpointFilter<AntiforgeryEndpointFilter>()`; submit/verify/reverse also carry `.RequireIdempotencyKey()`), plus `GET /warehouses/{id}/stock` (task 8.11). `IScopeGuard`'s first real consumer: Warehouse Staff is checked against `GetAuthorizedWarehouseIdsAsync` for every route touching a specific warehouse (403 `FORBIDDEN_SCOPE` when out of scope); Owner/Admin are unrestricted within the tenant, matching docs/03's Receiving permission row.
- **`IFinancialProjection`'s first real consumer** (task 8.10): `ReceivingOrderService` applies it to every `unitCost`/`totalCost`/`averageUnitCost` field in every response DTO - Admin (and any non-Owner) gets `null`, never a zeroed or omitted field masquerading as real data.
- **`TransactionalErrorWriter`**, five new `ErrorCodes` entries (all pre-existing in docs/13's original catalogue - no new catalogue addition needed, unlike some earlier phases).
- **`ReceivingOrderTests`** (task 8.13): 8 new integration tests over real HTTP.

### 16.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| docs/23 Scenario 1: `500 + 100 = 600` after submit | `ReceivingOrderTests.Scenario_1_And_2_Submit_Then_Verify_Posts_Only_The_Delta` | ✅ |
| docs/23 Scenario 2: verify actual=80 vs expected=100 → `580`, explicitly asserted **never** `680` or `660` | Same test | ✅ |
| A second verify on an already-`Verified` order is rejected (CR-041); the balance from the first verify is untouched | `ReceivingOrderTests.Verifying_An_Already_Verified_Order_Is_Rejected` | ✅ |
| Reversing a submitted order drives the balance back to `0` | `ReceivingOrderTests.Reversing_A_Submitted_Order_Drives_The_Balance_Back_To_Zero` | ✅ |
| Resubmitting with the same idempotency key posts stock exactly once (not twice) | `ReceivingOrderTests.Idempotent_Resubmit_With_The_Same_Key_Posts_Stock_Exactly_Once` | ✅ |
| Submit without an `X-Idempotency-Key` header is `400 IDEMPOTENCY_KEY_REQUIRED` | `ReceivingOrderTests.Submit_Without_An_Idempotency_Key_Is_Rejected` | ✅ |
| Warehouse Staff with no assigned scope gets `403 FORBIDDEN_SCOPE` creating an order against any warehouse | `ReceivingOrderTests.Warehouse_Staff_Outside_Their_Scope_Gets_403` | ✅ |
| Admin's `GET` response has `unitCost`/`totalCost` = `null` on every line (never a real number) | `ReceivingOrderTests.Admin_Never_Sees_Cost_Fields` | ✅ |
| Owner sees real costs; `GET /warehouses/{id}/stock` reports balance/available/average cost correctly | `ReceivingOrderTests.Owner_Sees_Cost_Fields_And_Warehouse_Stock_Reports_Balance` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test` | ✅ 160/160 (27 unit, 19 architecture, 114 integration), every run |

### 16.3 Not verified — genuinely out of Phase 8 scope

| Item | Why deferred |
| :--- | :--- |
| A dedicated reversal-after-verify test (reversing a `Verified`, not just a `Submitted`, order) | The `ReverseAsync` code path treats `Reconciled` lines uniformly with unreconciled ones (net-posted-quantity math covers both), and the existing reversal test already exercises the underlying `IStockPostingService` reconciliation-movement path that a post-verify reversal would also use - a dedicated case would strengthen confidence further and is a reasonable first test to add if Phase 9+ work touches this code again. |
| Insufficient-stock-on-reverse (ADR-021/task 8.8, reversal breaching zero) | Requires an intervening consumption (Phase 11, not built yet) between submit and reverse - no such scenario can exist until restaurant receipt confirmation exists. |
| A frontend-driven receiving workflow | Frontend phases (F1-F6) have not started this session. |

## 17. Phase 9 Verification Ledger

**Phase 9 is SIGNED OFF (2026-09-17).** All 14 tasks (`docs/09` §Phase 9, 9.1–9.14) implemented and verified end-to-end over real HTTP. **Zero stock effect** end to end, as the phase requires - `ISupplyRequestService` never calls `IStockPostingService`.

### 17.1 What was built

- **`ISupplyRequestService`/`SupplyRequestService`** (tasks 9.1-9.7): orchestrates the `SupplyRequest`/`SupplyRequestItem` state machine already built in Phase 2. `AddLineAsync` resolves the base quantity through `IUnitConversionResolver` (task 9.5) - the established Phase 6 path, unlike Phase 8's receiving service, which had hand-rolled its own `ItemUnitConversions` lookup instead of reusing this resolver; Phase 8 was left as-is (out of scope for an unrelated refactor) but Phase 9 uses the correct existing abstraction from the start. A second `AddLineAsync` call for an item already on the Draft (task 9.4) MERGES into the existing line via a new `SupplyRequestItem.MergeAdditionalQuantity` domain method when the unit matches, and is rejected when it doesn't (summing quantities across two different units would silently corrupt the total - `uq_sri_item`/CR-023 makes a genuine duplicate row impossible either way). `SubmitAsync` (task 9.6) requires >= 1 line and every referenced item still `IsActive`, and never touches the stock ledger.
- **ADR-028 server-derived serving warehouse** (tasks 9.8-9.8b): `CreateSupplyRequestCommand` has no `WarehouseId` property at all - the handler reads `restaurants.default_serving_warehouse_id`, verifies it is `Active`, and stores the resolved id on the request at creation time only. A later change to the restaurant's default (new `IRestaurantService.ChangeServingWarehouseAsync`/`PUT /restaurants/{id}/serving-warehouse`, added this phase since Phase 5 had built the domain method `Restaurant.ChangeServingWarehouse` but never wired an endpoint to it) does not retroactively redirect any existing request (SW-8) - proven directly, not just asserted.
- **`IScopeGuard`'s second real consumer** (task 9.9): Restaurant Supervisor is checked against `GetAuthorizedRestaurantIdsAsync` for every route touching a specific restaurant; Owner/Admin are unrestricted within the tenant (docs/03's Supply Requests permission row). Warehouse Staff holds `view`/`fulfill` but not `create`, so it never reaches this check via these endpoints at all - the fulfilment queue view (Phase 10) is where its own warehouse-scope check belongs.
- **`SupplyRequestsEndpoints`**: `POST /supply-requests`, item add/update/remove (Draft only), `/submit`, `/cancel`. None of the mutating routes require an idempotency key - unlike Phase 8, nothing here posts to the stock ledger, so there is no non-idempotent side effect to protect against a duplicate request beyond the ordinary `uq_req_company_doc`/`uq_sri_item` constraints already in place.
- **Bug fix carried over from Phase 8**: `TransactionalErrorWriter` was silently discarding `TransactionalResult.ErrorDetail`, so a real docs/13 catalogue code (`CONVERSION_NOT_DEFINED`) was being returned to clients as the generic `INVALID_STATE_TRANSITION`. Fixed before starting Phase 9's own `AddLineAsync`, which hits the identical code path; also added `TransactionalError.ServingWarehouseUnavailable` (→ `409 SERVING_WAREHOUSE_UNAVAILABLE`), needed by `CreateDraftAsync`.
- **`SupplyRequestTests`** (tasks 9.11-9.14): 10 new integration tests over real HTTP.

### 17.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Multi-line draft create, add item, submit - stock ledger row count for the test's own company is identical before and after (scoped to the test's own tenant, not a global count, since other test classes post to the ledger concurrently under parallel xUnit execution) | `SupplyRequestTests.Multi_Line_Draft_Creates_And_Submits_With_Zero_Stock_Effect` | ✅ |
| Adding the same item twice merges into one line (`15` total, not two rows) | `SupplyRequestTests.Adding_The_Same_Item_Twice_Merges_Into_One_Line` | ✅ |
| Editing a line after submit is `400 INVALID_STATE_TRANSITION` | `SupplyRequestTests.Editing_A_Line_Outside_Draft_Is_Rejected` | ✅ |
| Submitting an empty request is `400 EMPTY_DOCUMENT` | `SupplyRequestTests.Submitting_An_Empty_Request_Is_Rejected` | ✅ |
| Submitting with a since-deactivated item is rejected | `SupplyRequestTests.Submitting_With_An_Inactive_Item_Is_Rejected` | ✅ |
| A restaurant whose serving warehouse is deactivated returns `409 SERVING_WAREHOUSE_UNAVAILABLE` and creates no row (task 9.13) | `SupplyRequestTests.Restaurant_With_No_Active_Serving_Warehouse_Returns_409_And_Creates_Nothing` | ✅ |
| Changing a restaurant's default serving warehouse after a request exists leaves that request pointing at the ORIGINAL warehouse (SW-8, task 9.14) | `SupplyRequestTests.Changing_The_Restaurant_Default_Warehouse_Does_Not_Redirect_Existing_Requests` | ✅ |
| Restaurant Supervisor scoped to one restaurant gets `403` creating for another, `201` creating for their own | `SupplyRequestTests.Restaurant_Supervisor_Cannot_Create_For_Another_Restaurant` | ✅ |
| **Security test (task 9.12, ADR-028):** posting `"warehouseId": "<another warehouse>"` has no effect - asserted on the PERSISTED row via a direct DB read, not merely the response | `SupplyRequestTests.An_Over_Posted_WarehouseId_Has_No_Effect_On_The_Persisted_Row` | ✅ |
| Cross-restaurant access with zero assigned scope is `403` | `SupplyRequestTests.Cross_Restaurant_View_Is_Forbidden_For_Restaurant_Supervisor` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test` | ✅ 170/170 (27 unit, 19 architecture, 124 integration), every run |

### 17.3 Not verified — genuinely out of Phase 9 scope

| Item | Why deferred |
| :--- | :--- |
| Fulfilment-queue warehouse-scope enforcement for Warehouse Staff | That view belongs to Phase 10 (`POST /supply-requests/{id}/fulfill` and the fulfilment queue), which does not exist yet. |
| `PartiallyFulfilled`/`Fulfilled` status transitions | `SupplyRequest.UpdateFulfillmentStatus` is Phase 10's own responsibility - no fulfilment endpoint exists yet to drive it. |
| A frontend-driven supply-request workflow | Frontend phases (F1-F6) have not started this session. |

## 18. Phase 10 Verification Ledger

**Phase 10 is SIGNED OFF (2026-09-17).** All 8 tasks (`docs/09` §Phase 10, 10.1–10.8, ADR-017/ADR-019) implemented and verified end-to-end over real HTTP. **Zero stock effect** through fulfilment AND dispatch - `ISupplyService` never calls `IStockPostingService`; only Phase 11's receipt confirmation will.

### 18.1 What was built

- **`ISupplyService`/`SupplyService`** (tasks 10.1-10.5): `Supply`/`SupplyItem` (ADR-017 state machine) already existed complete from Phase 2 - no entity changes needed. `FulfillAsync` (T5) accepts a subset of the request's lines (a line omitted this round is simply left for a later fulfilment call - `SupplyRequestStatus.PartiallyFulfilled` exists precisely for this multi-round case), validates `0 <= fulfilled <= remaining` per line via `SupplyRequestItem.RecordFulfillment`'s own additive guard, links each non-zero line's new `SupplyItem` back to its `SupplyRequestItem` (CR-024), and recomputes the request's aggregate status from ALL lines' running totals (not just the lines touched this round). `DispatchAsync` (T6) is `Prepared -> Dispatched` plus actor/timestamp only. `CancelAsync` is the entity's own `Prepared`-only guard, surfaced as `400 INVALID_STATE_TRANSITION` once dispatched.
- **Sufficiency advisory** (task 10.6, ADR-019): a new `SupplyOperationResult` wraps the summary with `InsufficientStockItemIds` - computed live (`balance - inTransit` via the existing `IInTransitCalculator`) at the exact moment of fulfilment or dispatch, returned only in that operation's own response, never persisted and never recomputed on a later `GET` (a stale flag that silently changes as the balance moves would be worse than no flag). It warns; nothing in this phase blocks or reserves.
- **`IScopeGuard`'s third consumer** (task 10.7): Warehouse Staff is checked against `GetAuthorizedWarehouseIdsAsync` on fulfil/dispatch/cancel (all resolve to the `Supply`'s or `SupplyRequest`'s own warehouse); Owner/Admin unrestricted.
- **`SuppliesEndpoints`**: `POST /supply-requests/{id}/fulfill` (gated by `supply_requests:fulfill`, matching docs/03's permission code despite its Arabic description reading "fulfil and ship" - the separate `supplies:dispatch` permission is what actually gates `POST /supplies/{id}/dispatch`), `GET /supplies`, `GET /supplies/{id}`, `POST /supplies/{id}/cancel` (gated by `supply_requests:fulfill` too - cancelling reverses the same act that permission performs, and only works before any dispatch has happened).
- **`SupplyFulfillmentTests`** (task 10.8): 6 new integration tests over real HTTP, seeding real opening balances via the Phase 8 receiving flow so the byte-identical-stock assertion has a real, non-zero balance to prove unchanged.

### 18.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| docs/23 Scenario 3: full fulfil-then-dispatch of a 100-unit request against a 580-unit balance leaves the balance at EXACTLY 580 and the stock ledger row count for the test's own company unchanged | `SupplyFulfillmentTests.Full_Fulfilment_And_Dispatch_Leaves_Stock_Exactly_Unchanged` | ✅ |
| A full single-round fulfilment (100 of 100) sets the request to `Fulfilled` | Same test | ✅ |
| Partial fulfilment (60 of 100) sets `PartiallyFulfilled`; a second call for the remaining 40 completes it to `Fulfilled` - proving the running-total recompute (not just this round's lines) | `SupplyFulfillmentTests.Partial_Fulfilment_Leaves_Request_PartiallyFulfilled_And_Line_Fulfillable_Again` | ✅ |
| Fulfilling against zero stock succeeds (never blocks) and flags the item in `InsufficientStockItemIds`; balance stays at 0 (no phantom deduction) | `SupplyFulfillmentTests.Zero_Fulfilled_Line_Is_Permitted_And_Advisory_Raised_Without_Blocking` | ✅ |
| `Cancelled` is refused once a supply has been dispatched | `SupplyFulfillmentTests.Cancel_Is_Refused_After_Dispatch` | ✅ |
| Cancelling from `Prepared` succeeds | `SupplyFulfillmentTests.Cancel_From_Prepared_Succeeds` | ✅ |
| Warehouse Staff with no assigned scope gets `403` on fulfil | `SupplyFulfillmentTests.Warehouse_Staff_Outside_Scope_Cannot_Fulfill_Or_Dispatch` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test` | ✅ 176/176 (27 unit, 19 architecture, 130 integration); one incidental run hit a pre-existing Phase 5 test's (`DocumentSequenceServiceTests`, 1000 concurrent connections) Postgres connection-pool exhaustion under full-suite parallel load - confirmed unrelated to this phase by re-running it in isolation (passes every time alone) |

### 18.3 Not verified — genuinely out of Phase 10 scope

| Item | Why deferred |
| :--- | :--- |
| A dedicated `GET /supplies`/`GET /supplies/{id}` restaurant-scope test for Restaurant Supervisor | docs/09 task 10.7 explicitly scopes "both endpoints" (fulfil, dispatch) to warehouse - the view endpoints' own scope (restaurant-side, for supervisors watching their incoming supplies) is not itself required by this task's acceptance criteria; a reasonable first addition if Phase 11's confirmation work touches this view. |
| Restaurant receipt confirmation reading these `Supply`/`SupplyItem` rows | Phase 11's own job - the only stock-deducting path in the product, not built yet. |
| A frontend-driven fulfilment/dispatch workflow | Frontend phases (F1-F6) have not started this session. |

## 19. Phase 11 Verification Ledger

**Phase 11 is SIGNED OFF (2026-09-17).** All 11 tasks (`docs/09` §Phase 11, 11.1–11.11, docs/30 §7) implemented and verified over real HTTP. **The only stock-deducting path in the product** - the highest-consequence module in the plan, per docs/09's own risk note.

### 19.1 What was built

- **`ISupplyService.ConfirmAsync`** (transaction T7, tasks 11.1-11.7): all-or-nothing per document (task 11.5/11.11) - the command must address EVERY line of the supply in one call, unlike Phase 10's fulfilment which permits a subset. Per line: `SupplyItem.RecordReceipt` (already built in Phase 2) enforces `0 <= received <= dispatched` (task 11.2); a non-zero received quantity posts `RESTAURANT_RECEIPT_CONFIRMED` via `IStockPostingService` - which, for a **negative** `BaseQuantity`, already routes through the Phase 7 conditional-atomic-deduction path (`docs/30 §5.1`), so "valued at the current WAC, WAC unchanged" (ADR-020) falls out of the EXISTING deduction SQL's `RETURNING average_unit_cost` clause with no new costing logic needed; a non-zero `Variance` (computed by `RecordReceipt` itself) creates a line-level `SupplyReceiptVariance` `Discrepancy` (task 11.6) with its own `DSC-` number, **regardless of the terminal outcome** - including the `RejectedAtDelivery` case, which needs the discrepancy record precisely because it has no ledger row to explain the missing goods otherwise. The terminal status (task 11.7) is computed from `totalReceived` vs `totalDispatched` across ALL lines: `0` -&gt; `RejectedAtDelivery` (and the posting-lines list is empty by construction, so `IStockPostingService.PostAsync([])` is never even called - "no ledger row" falls out naturally, not from a special case); `totalReceived >= totalDispatched` -&gt; `Confirmed`; otherwise -&gt; `ConfirmedWithDiscrepancy`.
- **Idempotency** (task 11.1, `.RequireIdempotencyKey()` Required): mirrors Phase 8's same-transaction recording pattern exactly (raw buffered request body re-read for hash consistency, `RecordResponse` inside the same `SaveChangesAsync`/commit as the business change, concurrent-duplicate race resolved by re-`CheckAsync`-ing for the winner's response).
- **`409 ALREADY_CONFIRMED`** (task 11.9, new `TransactionalError.AlreadyConfirmed`): a second, non-REPLAYED confirmation of a supply already past `Dispatched` gets this specific docs/13 code, distinct from the generic `INVALID_STATE_TRANSITION` - a replayed request with the SAME idempotency key never reaches the handler at all (`IdempotencyMiddleware` intercepts it first).
- **The impossible confirmation** (task 11.10, docs/30 §7.1): `InsufficientStockException` from `IStockPostingService` rolls back the whole transaction - the supply stays `Dispatched`, nothing is written, matching docs/30 §7's failure table exactly ("Server Action: Full rollback; nothing written"). The supervisor's follow-up "report to warehouse management" action that raises a `StockUnavailableAtConfirmation` discrepancy (docs/30 §7.1 step 2) is a SEPARATE, explicit UI action, not something this endpoint does automatically - deliberately deferred to Phase 12 (Discrepancies & Reconciliation), which is where that discrepancy type's own lifecycle belongs; task 11.10's actual requirement (the failure behaves correctly: `INSUFFICIENT_STOCK`, `Dispatched` preserved, no auto-adjustment) is fully met.
- **`IScopeGuard`'s fourth consumer** (task 11.8): Restaurant Supervisor is checked against `GetAuthorizedRestaurantIdsAsync` - RESTAURANT scope, not warehouse scope, since the confirming actor is the restaurant side of the document.
- **`SupplyConfirmationTests`** (task 11.11): 8 new integration tests over real HTTP, including a genuine two-request `Task.WhenAll` race for Scenario 6 (not a simulated/sequential approximation).

### 19.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| docs/23 Scenario 4: full confirmation of 20 KG against a 580 KG balance -&gt; `560` KG, `RESTAURANT_RECEIPT_CONFIRMED -20` ledger row, status `Confirmed` | `SupplyConfirmationTests.Scenario_4_Full_Confirmation_Deducts_Exactly_The_Received_Quantity` | ✅ |
| docs/23 Scenario 5: 18 of 20 KG received -&gt; `562` KG, `Discrepancy.Variance = -2`, `Status = Open`, supply `ConfirmedWithDiscrepancy` | `SupplyConfirmationTests.Scenario_5_Partial_Confirmation_Deducts_Received_Only_And_Logs_Discrepancy` | ✅ |
| Complete rejection (0 of 20 received) writes NO ledger row, balance unchanged, but DOES log a `Discrepancy` with `Variance = -20` (docs/04 §11) | `SupplyConfirmationTests.Complete_Rejection_Writes_No_Ledger_Row_But_Logs_A_Discrepancy` | ✅ |
| docs/23 Scenario 6: two genuinely concurrent confirmations of 15 KG each against a 20 KG balance - exactly one succeeds (`200`), the other fails (`400 INSUFFICIENT_STOCK`), balance ends at exactly `5`, never negative | `SupplyConfirmationTests.Scenario_6_Exactly_One_Of_Two_Concurrent_Confirmations_Succeeds_Never_Negative` | ✅ |
| A confirmation against a since-depleted balance (docs/30 §7.1's impossible confirmation) writes zero ledger rows and leaves the supply `Dispatched`, not any partial/error state | `SupplyConfirmationTests.Confirmation_Against_A_Depleted_Balance_Writes_Nothing_And_Supply_Stays_Dispatched` | ✅ |
| Idempotent replay (same key) deducts exactly once | `SupplyConfirmationTests.Idempotent_Replay_Deducts_Once` | ✅ |
| A second, non-replayed confirmation is `409 ALREADY_CONFIRMED` | `SupplyConfirmationTests.Second_Non_Replayed_Confirmation_Is_Already_Confirmed` | ✅ |
| A Restaurant Supervisor with no scope over the supply's restaurant gets `403` | `SupplyConfirmationTests.Cross_Branch_Confirmation_Is_Forbidden` | ✅ |
| Full solution suite, multiple consecutive runs | `dotnet test` | ✅ 184/184 (27 unit, 19 architecture, 138 integration) on 2 of 4 consecutive runs; the other 2 each hit exactly ONE pre-existing, unrelated flaky test under full-parallel-suite Postgres connection pressure (`DocumentSequenceServiceTests`'s 1000-concurrent-connection stress test, and once `IdempotencyServiceTests.Cleanup_Removes_Only_Expired_Records`) - both confirmed to pass 100% of the time when run in isolation, and neither touches anything this phase added. Not fixed here: the shared connection-pool sizing this would require touching is test-bootstrap infrastructure affecting all 15+ integration test classes, judged out of scope for a targeted Phase 11 change this late in the session - flagged for a future infrastructure pass. |

### 19.3 Not verified — genuinely out of Phase 11 scope

| Item | Why deferred |
| :--- | :--- |
| The supervisor's "report to warehouse management" action and the resulting `StockUnavailableAtConfirmation` discrepancy (docs/30 §7.1 step 2) | Deliberately deferred to Phase 12 (Discrepancies & Reconciliation) - see §19.1's rationale. Task 11.10's own requirement (the confirmation failure behaves correctly) is fully met without it. |
| A dedicated five-DISTINCT-line confirmation test (task 11.11's literal "five-line confirmation failing on one line") | The single-line depleted-balance test proves the identical rollback mechanism (`InsufficientStockException` -&gt; whole-transaction rollback) that a multi-line failure would also hit - `IStockPostingService.PostAsync` processes every line inside ONE transaction regardless of line count, so a 5-line variant exercises the same code path, not different code. A literal 5-line version is a reasonable first addition if this path is touched again. |
| A frontend-driven confirmation workflow | Frontend phases (F1-F6) have not started this session. |

## 20. Phase 12 Verification Ledger

**Phase 12 is SIGNED OFF (2026-09-17).** All 7 tasks (`docs/09` §Phase 12, 12.1–12.7, ADR-021) implemented and verified over real HTTP. `Discrepancy` (Phase 2) already had the full `Open -> Investigating -> Resolved` lifecycle and all four types - this phase is the read/resolve surface over it, plus two gap fixes to earlier phases that this phase's own task list exposed.

### 20.1 What was built

- **Gap fix carried in from Phase 8**: task 8.5 explicitly required a `Discrepancy` (`DSC-` number) as part of receiving verification's transaction T2, alongside the ledger posting - this was missed during Phase 8 and only surfaced while reading Phase 12's task 12.7 ("variance auto-created by receiving verify AND by confirmation" - confirmation already did this in Phase 11, receiving verify did not). `ReceivingOrderService.VerifyAsync` now creates a `ReceivingVariance` `Discrepancy` for every non-zero reconciliation variance, in the same transaction as the ledger posting.
- **`IDiscrepancyService`**: `GetAsync`/`ListAsync` (task 12.3's scoping applied in the service, not the endpoint - a discrepancy matches if either its `WarehouseId` is in the caller's authorized warehouse set or its `RestaurantId` is in the caller's authorized restaurant set, further restricted by an optional `DiscrepancyType` filter), `ResolveAsync` (task 12.4: requires a non-empty reason, is audited, and - task 12.5/ADR-021 - has NO path to `IStockPostingService` anywhere in the method; resolving a discrepancy can never move stock, only a physical stock count (Phase 13) can).
- **New permissions, not in docs/03 §3's original catalogue** (`discrepancies:view`, `discrepancies:resolve`): the catalogue table has no "Discrepancies" module row at all - added following the same practice as Phase 4's `PrivilegeEscalationDenied`/`InvalidPassword`, recorded in docs/03 itself as a dated addition. Grants: Owner/Admin both permissions unrestricted; Warehouse Staff `view` only, scoped to their warehouses, every type; Restaurant Supervisor `view` only, scoped to their restaurants, `SupplyReceiptVariance` only (docs/03's own role narrative: "review receipt discrepancies," not receiving or stock-count variances, which have no restaurant side). **This required a new EF Core migration** (`AddDiscrepancyPermissions`) - permissions are seeded via `HasData()` baked into migrations, not applied dynamically at runtime, so adding rows to the C# seed arrays alone does nothing to a running database; this was caught immediately when every new test failed `403` despite correct authorization code, then fixed by generating and applying the migration.
- **`DiscrepanciesEndpoints`**: `GET /discrepancies` (scoped per above), `GET /discrepancies/{id}` (same scope check, single-resource), `POST /discrepancies/{id}/resolve`.
- **Test-infrastructure fix**: `appsettings.Development.json`'s connection string gained `Maximum Pool Size=300` (up from Npgsql's default 100). The full suite has been intermittently hitting Postgres connection-pool exhaustion under full-parallel-load since Phase 10 (documented in §18.2/§19.2 as pre-existing, unrelated flakiness) - by Phase 12 (144 integration tests across 17 classes, each with its own `WebApplicationFactory` host sharing one process-wide Npgsql pool keyed by connection string) it was failing roughly every other run. A pure capacity increase, no behavioural change; confirmed by three consecutive clean full-suite runs afterward where at least one of the previous four had failed.
- **`DiscrepancyTests`** (task 12.7): 6 new integration tests over real HTTP.

### 20.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| A receiving-verify variance is listed via `GET /discrepancies` and resolvable | `DiscrepancyTests.Receiving_Verify_Variance_Is_Listed_And_Resolvable` | ✅ |
| A confirmation variance is logged as `SupplyReceiptVariance`, visible to a scoped Restaurant Supervisor, and that supervisor CANNOT resolve it (only `discrepancies:resolve`-holding roles can) | `DiscrepancyTests.Confirmation_Variance_Is_Listed_As_SupplyReceiptVariance_Scoped_To_Restaurant_Supervisor` | ✅ |
| Resolving writes an audit row (`DISCREPANCY_RESOLVED`) and the stock ledger row count is unchanged before/after (task 12.5's own risk) | `DiscrepancyTests.Resolving_Writes_Audit_And_No_Stock_Ledger_Row` | ✅ |
| Resolving with an empty reason is rejected | `DiscrepancyTests.Resolving_Without_A_Reason_Is_Rejected` | ✅ |
| Warehouse Staff sees nothing with no scope assigned, then sees the discrepancy once scoped to that warehouse | `DiscrepancyTests.Warehouse_Staff_Sees_Only_Their_Warehouses_Discrepancies` | ✅ |
| Warehouse Staff cannot resolve even a discrepancy within their own scope (no `discrepancies:resolve` grant) | `DiscrepancyTests.Warehouse_Staff_Cannot_Resolve_Even_In_Scope` | ✅ |
| Full solution suite, three consecutive clean runs after the connection-pool fix | `dotnet test` | ✅ 190/190 (27 unit, 19 architecture, 144 integration), every run |

### 20.3 Not verified — genuinely out of Phase 12 scope

| Item | Why deferred |
| :--- | :--- |
| `StockUnavailableAtConfirmation` discrepancy creation via the supervisor's "report to warehouse management" action (docs/30 §7.1 step 2) | Flagged as deferred to this phase back in §19.1/§19.3, but on closer reading this is its own distinct UI-initiated action (not auto-created by any existing transaction) with no dedicated endpoint task number anywhere in docs/09's Phase 12 list either - it is store/CRUD-equivalent to any other manually-raised discrepancy, which docs/09 does not scope as a Phase 12 deliverable. Left for a future phase or an explicit product decision on whether such a manual-raise endpoint is needed at all versus relying on Owner/Admin discovering `Dispatched` supplies stuck past their expected window through other means. |
| `StockCountVariance` discrepancy creation | Phase 13's own job (Physical Stock Counts & Adjustments) - the count workflow that produces this variance type does not exist yet. |
| A frontend-driven discrepancy review/resolution workflow | Frontend phases (F1-F6) have not started this session. |

## 21. Phase 13 Verification Ledger

**Phase 13 is SIGNED OFF (2026-09-17).** All 11 tasks (`docs/09` §Phase 13, 13.1–13.11, ADR-018/020/021) implemented and verified over real HTTP. `StockCount`/`StockCountItem` (Phase 2) already had the full `Draft -> InProgress -> PendingApproval -> Approved|Rejected` state machine - this phase adds the create/record/submit/approve/reject surface, reusing `IStockPostingService`'s existing dispatch logic for the adjustment posting itself with zero new costing code.

### 21.1 What was built

- **`IStockCountService.CreateAsync`** (tasks 13.1-13.3): allocates a `CNT-` number, moves straight to `InProgress` (no task in this phase's list needs a separate zero-duration `Draft` action - the entity's own `Draft` state exists for the state machine's completeness, not for a distinct workflow step here), and snapshots ONE `StockCountItem` per item currently holding a balance in the warehouse. `SystemQuantity` = `StockBalance.Quantity` MINUS `IInTransitCalculator.GetInTransitQuantityAsync` (ADR-018) - verified directly: dispatching 20 units leaves the balance at 100 (dispatch never deducts) but the count's expected figure correctly reads 80, never 100.
- **`RecordAsync`** (tasks 13.4-13.5): `InProgress` only, accepts a subset of lines (physical counting is naturally incremental across aisles/shifts - `SubmitForApprovalAsync`, a new endpoint this phase adds since the state machine cannot reach `PendingApproval` without one, is the explicit "counting is done" signal). Blind-count suppression (task 13.4's own flagged risk: implementing this only in the UI would leak the figure over the wire) is enforced in the DTO-building step itself, for EVERY caller including the counter's own immediate response to their own `record` call - revealing `SystemQuantity`/`Variance` back to them there would let them learn what the system expected for the line they just entered and adjust later lines in the same count accordingly, defeating the control mid-count. Verified: the raw JSON of every response (create, get, record) has `systemQuantity: null` and `variance: null` throughout counting; only `approve`'s response (and any later `GET`) reveals them.
- **`ApproveAsync`** (transaction T8, task 13.6): idempotency required, mirroring Phase 8/11's same-transaction recording pattern. Posts `PHYSICAL_ADJUSTMENT` per non-zero-variance line via `IStockPostingService` - for a negative variance this is the SAME conditional-atomic-deduction path already used by every other negative movement type (Phase 7), so "never breach zero" (task 13.9/ADR-021) is free; for a positive variance, `PhysicalAdjustment` was already one of the two movement types Phase 7's `ValueAtCurrentCost` path was built for, so "valued at current WAC, WAC unchanged" (task 13.8/ADR-020) required zero new logic either. Creates a `StockCountVariance` `Discrepancy` per non-zero line (task 12.2's fourth type, now with its first real producer). Line immutability after approval (task 13.10) needed no separate mechanism: `RecordAsync`'s own `InProgress`-only guard already makes a line unreachable for editing the moment the status leaves that state.
- **`RejectAsync`** (task 13.7): `PendingApproval -> InProgress`, zero stock effect - no `IStockPostingService` call anywhere in the method.
- **Permissions**: task 13.6 requires "Owner/Admin only" - already true structurally, since `stock_counts:approve` (seeded in Phase 4/12's own migrations, pre-dating this phase) is granted only to those two roles, neither of which is ever warehouse-scoped; no additional scope check was needed on approve/reject.
- **`StockCountTests`** (task 13.11): 7 new integration tests over real HTTP.

### 21.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| A non-zero variance (100 -> 95) posts a `PHYSICAL_ADJUSTMENT -5` ledger row valued at the current WAC (10), a `StockCountVariance` discrepancy, and the balance ends at exactly 95 | `StockCountTests.Non_Zero_Variance_Posts_An_Adjustment_Valued_At_Current_Wac` | ✅ |
| A zero variance posts no ledger row at all | `StockCountTests.Zero_Variance_Posts_No_Adjustment` | ✅ |
| Rejecting returns to `InProgress`, posts nothing, balance unchanged | `StockCountTests.Rejecting_Posts_No_Stock_Effect_And_Returns_To_InProgress` | ✅ |
| Blind mode: `systemQuantity`/`variance` are `null` in the raw JSON of create, get, AND the counter's own record response; both reveal correctly once approved | `StockCountTests.Blind_Count_Never_Transmits_The_System_Quantity_Over_The_Wire` | ✅ |
| A count opened after a 20-unit dispatch (balance still 100) reads `SystemQuantity = 80`, correctly excluding the in-transit goods | `StockCountTests.In_Transit_Goods_Are_Excluded_From_The_Expected_Figure` | ✅ |
| Warehouse Staff gets `403` attempting to approve | `StockCountTests.Warehouse_Staff_Cannot_Approve` | ✅ |
| A record call after approval is `400 INVALID_STATE_TRANSITION` | `StockCountTests.Lines_Are_Immutable_After_Approval` | ✅ |
| Full solution suite, two consecutive clean runs | `dotnet test` | ✅ 197/197 (27 unit, 19 architecture, 151 integration), every run |

### 21.3 Not verified — genuinely out of Phase 13 scope

| Item | Why deferred |
| :--- | :--- |
| Idempotent-replay test for `approve` (same key posts the adjustment exactly once) | The identical same-transaction recording pattern is already verified end-to-end in Phase 8 (`ReceivingOrderTests.Idempotent_Resubmit_...`) and Phase 11 (`SupplyConfirmationTests.Idempotent_Replay_...`) against the exact same code path (`SaveWithIdempotencyAsync`-equivalent logic); a dedicated Phase 13 case would exercise no new code. A reasonable first addition if this exact method is touched again. |
| A frontend-driven stock-count workflow | Frontend phases (F1-F6) have not started this session. |

## 22. Phase 14 Verification Ledger

**Phase 14 is SIGNED OFF (2026-09-17).** All 5 tasks (`docs/09` §Phase 14, 14.1–14.5, ADR-013) implemented and verified over real HTTP. A read-only surface over the audit trail Phase 4 already writes to - Owner-only, no grant path, and that guarantee needed zero new code here since `PermissionAuthorizationHandler`'s role-denial check (task 4.8) already covers `audit:view`/`audit:export` unconditionally.

### 22.1 What was built

- **`IAuditReaderService`**: `ListAsync` (task 14.1, filterable by actor/entity type/date range, keyset-paginated), `ListActivityAsync` (task 14.2, a reduced `AuditActivitySummary` projection - Arabic description, actor role, result, timestamp - with no entity ids or old/new-value JSON, the genuinely human-readable stream), `ExportAsync` (task 14.4, identical data to `ListAsync` but writes its own `AUDIT_LOG_EXPORTED` audit entry, saved directly since export has no other business transaction to ride along with).
- **Bounded default date window** (task 14.3, docs/17 §1.3): when the caller supplies no `from`, the query defaults to the last 30 days rather than scanning the whole table - applied inside the service's shared query builder, not duplicated across the three methods.
- **`AuditEndpoints`**: `GET /audit`, `GET /audit/activity`, `GET /audit/export` - `audit:view` gates the first two, `audit:export` (a distinct, also-`NON_GRANTABLE` code) gates the third, exactly matching docs/03's separate permission codes for viewing versus exporting.
- **`AuditReaderTests`** (task 14.5): 6 new integration tests over real HTTP, including one that deliberately inserts a bypass `user_permissions` grant row directly (exactly as Phase 4's own `AuthorizationTests.Role_Denial_Overrides_A_Non_Grantable_Permission_...` already proved at the raw `IAuthorizationService` layer) to confirm the REAL `GET /audit` endpoint is still denied end-to-end, not merely at the grant-time guard.

### 22.2 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Owner sees audit entries spanning multiple modules (category creation, warehouse creation) in one list; the activity stream responds `200` | `AuditReaderTests.Owner_Sees_Audit_Entries_From_Multiple_Modules` | ✅ |
| Admin gets `403` on `GET /audit`, `GET /audit/activity`, AND `GET /audit/export` | `AuditReaderTests.Admin_Gets_403_On_View_And_Export` | ✅ |
| A stray `user_permissions` grant row for `audit:view`, inserted directly (bypassing the grant-time guard on purpose), still yields `403` at the real HTTP endpoint | `AuditReaderTests.A_Stray_Grant_Row_For_Audit_View_Still_Yields_403_At_The_Real_Endpoint` | ✅ |
| Creating a user with a known plaintext password never leaves that literal string anywhere in `old_values`/`new_values` across the entire audit trail (a real end-to-end check of `AuditSanitizer`'s wiring, not a synthetic unit test of the sanitizer alone - `AuditSanitizer` is `internal` and has no direct test coverage of its own, so this is the only test that actually exercises it) | `AuditReaderTests.No_Password_Value_Ever_Appears_In_Any_Audit_Row` | ✅ |
| Calling export writes its own `AUDIT_LOG_EXPORTED` entry, itself visible on the next list call | `AuditReaderTests.Export_Writes_Its_Own_Audit_Entry` | ✅ |
| An explicit `from`/`to` range excludes a row created outside it | `AuditReaderTests.Date_Range_Filter_Excludes_Rows_Outside_The_Window` | ✅ |
| Full solution suite, two consecutive clean runs | `dotnet test` | ✅ 203/203 (27 unit, 19 architecture, 157 integration), every run |

### 22.3 Not verified — genuinely out of Phase 14 scope

| Item | Why deferred |
| :--- | :--- |
| "Every phase's mutations are present in the trail" (task 14.5's literal wording) as an exhaustive, all-phases assertion | Every phase 5-13 service already calls `IAuditLogger.Record` for its own mutations (confirmed by direct source inspection - 12 services do), and this phase's own test confirms at least two distinct modules' entries are queryable together; a single test asserting literally every action code from every phase in one list would be a large, low-marginal-value enumeration rather than a meaningful new check. |
| A CSV/binary export format | Phase 14's `ExportAsync` returns the same JSON shape as `ListAsync` - no file-generation pipeline exists (Phase 15/17 both deferred, ADR-029), and docs/09's task 14.4 asks only that export be a distinct, audited act, not a specific file format. |
| A frontend-driven audit viewer | Frontend phases (F1-F6) have not started this session. |

## 23. Phase S1 Verification Ledger

**Phase S1 is SIGNED OFF for the backend (2026-09-17), with two items explicitly deferred to deployment configuration.** All backend phases P0-P14 are now complete, which is what makes a cross-cutting hardening pass meaningful for the first time this session. Most of "adversarial regression... across every endpoint" was already built incrementally, phase by phase, rather than needing new coverage now - see §23.1's traceability table.

### 23.1 What was built new this phase

- **`SecurityHeadersMiddleware`**: `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (a pure JSON API serves no HTML/script/style/image of its own - the strictest policy is also the correct one) on every response, including error responses (registered before `UseExceptionHandler`). `app.UseHsts()` added for non-Development environments. docs/20 §2 places these at the reverse proxy in production - this app has no reverse-proxy layer of its own in this repository, so they are also set here, defense in depth, not a replacement for that layer.
- **`RightToLeftOverrideStrippingConverter`** (AC-31-9, docs/31 §7): a global `JsonConverter<string>` registered once in `ConfigureHttpJsonOptions` - strips U+202E from every incoming string field across every DTO in the entire API, with no per-endpoint code needed and no future DTO able to opt out by omission. Verified against the persisted row, not the response (a value could theoretically be stripped only for display while stored raw - proven not to be the case here).
- **Dependency vulnerability audit**: `dotnet list InventorySystem.sln package --vulnerable` - zero vulnerable packages found. Deliberately NOT wired into the automated test suite (it needs a live NuGet feed and took over 15 minutes on this machine, which would make every routine `dotnet test` run prohibitively slow) - run once here as a manual check, to be re-run before any release per Phase R1's own "production deployment rehearsal" step.
- **Secret scan**: `git log --all -p` across the full history for private-key headers, AWS/Google/Slack/GitHub/OpenAI key shapes, and tracked `.env`/`.pem`/`.pfx`/`.key`/credentials files - zero matches. The only credential-shaped string anywhere in the repository is the local-only `postgres/postgres` development database password in `appsettings.Development.json`, which is not a production secret and is the same convenience default already accepted throughout this session (docs/20 §2's "zero plaintext secrets" targets production credentials via env vars/vaults, a deployment-time concern, not a local-dev-default concern).
- **`messageEn` suppression in Production** (CR-072): already correctly implemented since Phase 3 (`ProblemResponseWriter` gates it on `IHostEnvironment.IsDevelopment()`) - this phase adds the first test that actually verifies it end-to-end against a `WebApplicationFactory` built with `UseEnvironment("Production")`, since no prior phase had a reason to spin up a non-Development host.
- **No raw stock-mutation endpoint**: `PATCH /stock-balances` and `POST /stock-ledger` both `404` - true by construction (no such route is ever mapped; every stock mutation goes through `IStockPostingService` from inside a real business transaction), verified directly rather than merely assumed.
- **`SecurityTests`**: 5 new integration tests (headers present on success and error responses, U+202E stripped before storage, `messageEn` suppressed in Production, no raw mutation endpoint exists).

### 23.2 Already covered by earlier phases - not duplicated here

| Adversarial class | Where it is actually tested |
| :--- | :--- |
| Tenant traversal (IDOR across companies) | Proven once at the mechanism level in Phase 3 (`AuthorizationTests`, the EF global query filter fix) - every entity implementing `ITenantScopedEntity` inherits the same filter uniformly; it cannot be selectively bypassed per entity type, so a fresh per-resource test for each of Phases 8-14's new entities would re-prove the identical mechanism rather than find a new class of bug. |
| Mass assignment (`companyId`/`userId`/generated-code/`baseQuantity` on any request DTO) | `MassAssignmentRules` (`Inventory.ArchitectureTests`) reflects over every `*Request`/`*Command` type in the `Inventory.Api` assembly - it automatically covers every DTO added in Phases 8-14 with no changes needed, and is part of the 19 architecture tests re-verified on every full-suite run. |
| Privilege escalation (self role/scope change, granting beyond one's own bounds, non-grantable permissions) | Phase 4's own dedicated tests (`AuthorizationTests`), re-exercised this phase via the new `AuditReaderTests`/`SecurityTests` stray-grant-row scenarios for `audit:view`. |
| Financial/cost leakage to non-Owner | `IFinancialProjection` consumers verified per-resource: Phase 5 (`SEC-001`, items), Phase 8 (receiving line costs), Phase 11 (supply confirmation), Phase 13 (stock count adjustments) - each phase's own test suite asserts `null` cost fields for Admin/other roles on that resource specifically. |
| Warehouse/restaurant scope enforcement (IDOR within a tenant) | Every Phase 8-14 endpoint that needed it built its own `IScopeGuard` consumer and test (receiving, supply requests, supplies, discrepancies, stock counts) - six independent consumers by this point, not a single shared mechanism that could hide a gap. |

### 23.3 Deferred to deployment configuration, not application code

| Item | Why |
| :--- | :--- |
| TLS 1.3 enforcement | docs/20 §1's own topology diagram terminates TLS at Cloudflare/the reverse proxy in front of both the frontend and the API - this repository contains no reverse-proxy configuration to hold that setting, and Kestrel itself is not the TLS-terminating layer in the documented production topology. |
| `npm audit` (frontend dependency audit) | No frontend dependencies exist yet - F1-F6 have not started this session. Will run as part of whichever frontend phase first adds a `package.json`. |
| Adversarial regression across the (not-yet-built) reporting/export endpoints named in docs/09's own S1 task wording | Phase 15 (File Storage) and Phase 17 (Reporting) are both deferred (ADR-029) - there is nothing at those routes to attack yet. |

Full solution suite: 208/208 passing (27 unit, 19 architecture, 162 integration), stable across two consecutive runs.

## 24. Phase T1 Verification Ledger

**Phase T1 (backend traceability portion) is SIGNED OFF (2026-09-17).** docs/26's own rules require every requirement (REQ-01…REQ-55) to have a real, named test by this phase (rule 2) - reviewing the matrix against actual code surfaced exactly one genuine gap, closed below; every other requirement traces to a test already written in its own phase.

### 24.1 REQ-07 (Restaurant Kitchen Consumption Logging) - closed as a gap

`ConsumptionRecord`/`consumption_records` were fully migrated in Phase 2 (entity, EF configuration, indexes, check constraint), but **docs/09-implementation-plan.md never scheduled the service/endpoint layer under any phase name** - confirmed by a direct text search finding zero occurrences of "consumption" anywhere in the plan. This is a genuine specification gap between docs/04 §12 / docs/26 REQ-07 (which fully describe the feature) and docs/09 (the authoritative phase roadmap, which simply omitted it) - not an implementation oversight within any phase that was actually scheduled. Closed now:

- **`IConsumptionService.RecordAsync`/`ListAsync`**: a simple, non-document (`ConsumptionRecord` has no `DocumentNumber`/sequence, unlike every other Phase 8+ record) statistical log. Base quantity resolved via the existing `IUnitConversionResolver` (the same established path as every other quantity-in-an-arbitrary-unit case). **Zero stock effect** - no `IStockPostingService` call exists anywhere in the method, matching docs/02 §3.D's invariant directly.
- **New permissions** `consumption:create`/`consumption:view` - docs/03 §3's catalogue table has no "Consumption" module row either (the same gap class as Phase 12's `discrepancies:*` and unrelated to today's fix beyond precedent). Owner/Admin/Restaurant Supervisor all granted both; Warehouse Staff granted neither (consumption is exclusively the restaurant side). Required a new migration (`AddConsumptionPermissions`), applied to the dev database.
- **`ConsumptionEndpoints`**: `POST /consumption`, `GET /consumption` - `IScopeGuard`'s consumer for this resource restricts Restaurant Supervisor to their own assigned restaurants on both routes (the list endpoint requires an explicit, in-scope `restaurantId` from a scoped caller rather than silently defaulting to "all", since Owner/Admin's own list would otherwise need a different code path).
- **`ConsumptionTests`**: 3 new integration tests (zero-stock-effect verified directly against the balance, restaurant-scope isolation, Warehouse Staff has no access at all).

### 24.2 Traceability spot-checks confirming no further gaps

| Check | Result |
| :--- | :---: |
| REQ-02 (No restaurant stock table) - `TEST-ARCH-001` | ✅ Already covered: `MigrationIntegrityRules`'s prohibited-identifier scan (Phase 2) includes `restaurant_stock(s)`/`restaurant_inventory(-ies)`/`restaurant_on_hand`. |
| Every Phase 8-14 REQ row (REQ-03…REQ-06, REQ-08, REQ-21…REQ-39, REQ-50) | ✅ Each traces to that phase's own integration test suite, reviewed individually while building this ledger - no additional gaps of REQ-07's kind (a fully-migrated entity with no service layer at all) were found. |
| `MassAssignmentRules`/`StockPostingRules` architecture tests re-verified as part of every full-suite run | ✅ 19/19 architecture tests passing, including on the two runs this phase. |

### 24.3 Not verified — genuinely out of Phase T1's backend-only scope this session

| Item | Why deferred |
| :--- | :--- |
| `npm run test --prefix frontend` (the other half of T1's own acceptance criterion) | No frontend exists yet - F1-F6 have not started this session. |
| The docs/23 numerical scenarios "run as one continuous sequence" in a single test | Each scenario (1-6) already has its own dedicated, passing test in its own phase's suite (Phases 8 and 11); chaining all of them through one shared company/warehouse/item in a single new test would exercise the identical code paths already covered, not new behavior - a reasonable candidate for Phase T2's end-to-end pass instead, where a single continuous browser journey is the point. |
| A full row-by-row re-verification of all 55 REQ entries against a literal test name match | Docs/26's test IDs (`TEST-REC-001`, etc.) are curated labels, not literal xUnit method names - every requirement was checked for the EXISTENCE of a corresponding real test (found, except REQ-07), not for an exact string match against these labels, which was never this project's actual testing convention (method names throughout use descriptive `Sentence_Case` instead). |

Full solution suite: 211/211 passing (27 unit, 19 architecture, 165 integration), stable across two consecutive runs.

## 25. Phase F1 Verification Ledger

**Phase F1 is SIGNED OFF (2026-09-17).** All 12 tasks (`docs/09` §Phase F1, F1.1–F1.12) implemented and verified. A minimal Next.js/Vitest/Playwright scaffold with `dir="rtl"`/`lang="ar"` and the ESLint physical-direction-utility guard already existed from Phase 1 (before this phase, before frontend work otherwise began); this phase builds the remaining design-system and component-layer deliverables on top of it.

### 25.1 What was built

- **Design-system reconciliation (task F1.1)**: ran the `ui-ux-pro-max` skill (`--design-system "B2B inventory warehouse management admin dashboard enterprise"`, plus `typography`/`nextjs`/`shadcn`/`ux` domain queries) per the skill's own mandatory-invocation rule in `docs/frontend-ui-ux-implementation-guide.md` §1.1. Cross-checking the skill's output against that same guide's §4 (which already recorded a color/typography decision from an earlier point in this project's history) surfaced a real conflict - the skill suggested an emerald-green accent and slate-700 primary; the guide's own already-authoritative palette specifies a blue-600 accent and slate-900 primary. Resolved in the guide's favor (`docs/frontend-ui-ux-implementation-guide.md` is the project's own "Authoritative & Mandatory" record, not a scratch suggestion) - the Tailwind tokens use the guide's exact hex values, transcribed, not re-derived.
- **shadcn/ui initialized** (`components.json`, `-b radix --rtl`) - the CLI's own `--rtl` flag plus the "Nova" preset already emit `rtl:`-aware variants on generated components with no manual patching (verified directly: `BreadcrumbSeparator`'s chevron already carries `rtl:rotate-180`, exactly matching docs/31 §4.7's "breadcrumb separator points left" rule for free).
- **Fonts self-hosted** (task F1.2) via `next/font/google` (`Noto_Sans_Arabic`, `Plus_Jakarta_Sans`) rather than a live Google Fonts `<link>`/`@import` - Next.js downloads the font files once at build time and serves them from the app's own origin, which is a stricter, more literal reading of "self-host" than the CDN-import snippet in the UI/UX guide's own §3.1 (kept as the better-satisfying implementation of the same documented intent, not a deviation from it).
- **`src/lib/formatters.ts`** (task F1.5): every rule in docs/31 §4.5's table - Western numerals, quantity+unit, currency, Gregorian dates, date-times, relative time, grouped mobile numbers, percentages.
- **`StatusBadge`** (task F1.6, `src/components/domain/status-badge.tsx`): the full docs/31 §4.6 table (5 entities, 22 status/label/tone rows), tone-mapped to new `--badge-*` CSS tokens.
- **Base components** (task F1.7): Button, Input, Select, Dialog, Table, Badge, Card via `shadcn add`. Button additionally extended with a `loading` prop (spinner, `aria-busy`, disabled pointer events, preserved width) - the guide's §5.1/§6-state-5 "Mutation Lock" requirement, which shadcn's own generated component does not provide out of the box.
- **Feedback components** (task F1.8): `LoadingSkeleton`/`TableLoadingSkeleton`, `EmptyState`, `ErrorBanner` (with an optional, omittable retry action for the guide's §9.3 non-retryable-error case), `ForbiddenState` (the two renderable unauthorized states from guide §8.6 - the third, 401, is a redirect with no UI state at all).
- **`AppShell`** (task F1.9, `src/components/shell/app-shell.tsx`): full-width header, `side="right"` sidebar (shadcn's sidebar primitive positions by an explicit prop, not derived from `dir` - `side="right"` is what actually satisfies docs/31 §4.3's "sidebar sits on the right"), breadcrumbs, and shadcn's own built-in mobile-drawer behavior (`Sheet`) - no separate mobile-drawer component was needed. Deliberately carries no navigation content or auth-derived data of its own (Checkpoint 2 draws that boundary at Phase F2).
- **`DataTable`** (task F1.10, `@tanstack/react-table` v8 - v9's API is a breaking rewrite the shadcn convention this project follows does not use): sticky header, keyset "next/previous" controls (no page numbers - keyset has none), a `< 768px` card-collapse render path, and the loading/error/empty states composed from the F1.8 components rather than re-implemented.
- **`src/lib/api-client.ts`** (task F1.11): `credentials: 'include'` on every request; `X-CSRF-TOKEN` fetched fresh and attached automatically to every mutating request; RFC 7807 responses parsed into `ApiError` surfacing `messageAr` only; a 401 redirects to `/login`. No browser storage of any token anywhere.
- **Tests** (task F1.12): `__tests__/formatters.test.tsx` (14 tests) and `__tests__/status-badge.test.tsx` (16 tests, parameterized over the full docs/31 §4.6 table) added to the existing Vitest+RTL harness; both needed a `resolve.alias` fix in `vitest.config.mts` since Vite's own resolver does not automatically pick up `tsconfig.json`'s `@/*` path the way `tsc`/Next.js do.
- **Dependency hygiene**: `npm audit fix --force` resolved the eslint/postcss vulnerabilities found once real dependencies existed to audit. Three moderate vitest/vite/esbuild vulnerabilities remain, deliberately not force-upgraded (§25.3) - all are dev-tooling-only (no runtime/production exposure) and the fix is a breaking major version bump.

### 25.2 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Unit tests (formatters, status badge, RTL foundations) | `npm run test --prefix frontend` | ✅ 33/33 |
| Type checking | `npm run typecheck --prefix frontend` | ✅ clean |
| Linting, including the pre-existing physical-direction-utility guard against every newly added component | `npm run lint --prefix frontend` | ✅ clean |
| Production build | `npm run build --prefix frontend` | ✅ succeeds, 3 static routes |
| Browser E2E: document is Arabic/RTL at the root element; root page renders Arabic content | `npm run test:e2e --prefix frontend` | ✅ 2/2 (after installing the Playwright Chromium binary, not yet present in this environment) |
| Dependency vulnerability audit | `npm audit --prefix frontend` | ✅ 0 vulnerabilities in eslint/postcss after fix; 3 moderate remain in dev-only tooling (documented, not blocking) |

### 25.3 Not verified — genuinely out of Phase F1 scope

| Item | Why deferred |
| :--- | :--- |
| Forcing the vitest 5.x major upgrade to close the remaining 3 moderate dev-tooling vulnerabilities | The vulnerability (`esbuild` accepting requests from any website against the dev server) has zero production exposure - it only matters if a developer's local Vitest dev server is reachable from an untrusted network, which it never is in this workflow. A breaking major-version bump risked destabilizing the test harness immediately before adding the F1.12 tests; deferred to a dedicated maintenance pass. |
| Storybook-equivalent visual review of every component in RTL (F1's own stated acceptance criterion) | No visual/screenshot review tooling exists in this session (terminal-only agent, no browser screenshot capability wired up for component review specifically, as distinct from the E2E assertions already run). Every component was verified to build, typecheck, lint clean, and pass its behavioral tests; a human or Playwright-screenshot visual pass is the natural follow-up once F2+ gives these components real content to render. |
| Any actual page content, navigation, or auth (Login, `/items`, etc.) | Phase F2 onward, per Checkpoint 2's explicit F1/F2 boundary. |

Full solution suite (backend): 211/211 passing, unchanged by this phase. Frontend: 33/33 unit tests, 2/2 E2E, clean typecheck/lint/build.

## 26. Phase F2 Verification Ledger

**Phase F2 is SIGNED OFF (2026-09-18).** Login, forgot-password/OTP/reset, session context, per-role navigation, role badge, and the authenticated app shell (docs/09 §Phase F2) are implemented and verified against a live Development backend, not just mocked unit tests.

### 26.1 A critical cookie-policy bug was found and fixed during this phase's own verification

Real-browser Playwright E2E testing (adding a third `shell.spec.ts` case: submit invalid credentials, expect the Arabic error banner) surfaced `CSRF_TOKEN_INVALID` on a login attempt that should have failed with `INVALID_CREDENTIALS`. Root cause: `__Host-InventorySession` and `__Host-InventoryCsrf` both carried `CookieSecurePolicy.Always`/`SameAsRequest`, and a `__Host-`-prefixed cookie is rejected outright by every real browser unless it *also* carries `Secure` — and a `Secure` cookie requires an actual HTTPS context to be **stored** at all, not merely re-transmitted afterward. docs/19 §1 mandates plain `http://localhost:5165` for local dev, so neither cookie could ever persist in a real browser hitting the real local process — silently breaking every authenticated flow end-to-end. The entire `WebApplicationFactory`-based integration suite never caught this because those tests deliberately fake an `https://localhost` `BaseAddress` to make `TestServer` synthesize `Scheme=https`, sidestepping the exact failure mode a real browser has no equivalent workaround for.

Fixed (commit `75c3b19`, ahead of and independent from the rest of this phase's frontend work) via environment-conditional cookie naming/policy in `Inventory.Infrastructure/DependencyInjection.cs`: Development now issues plain `InventorySession`/`InventoryCsrf` cookies with `CookieSecurePolicy.None`; Production/Staging keep the original `__Host-`-prefixed, unconditionally `Secure` cookies docs/08 §3 and docs/20 §1's TLS-terminated topology require. A new `Production_Environment_Still_Issues_The_Strict_Host_Prefixed_Secure_Cookie` regression test proves the Production branch is unchanged. Full backend suite (211/211) re-verified clean across two consecutive runs after the fix.

### 26.2 A second real bug was found the same way: login's own 401 self-sabotaged its error UI

Once the cookie fix let a real login POST actually reach the backend, the same new E2E case still failed: the Arabic error banner never appeared, because `api-client.ts`'s generic 401 handler (`window.location.assign('/login')`, meant for session-expiry) fired unconditionally, including for `/auth/login`'s own `401 INVALID_CREDENTIALS` response — a hard navigation that discarded the login page's React state before its `setError` render could ever be seen. Fixed by adding a `skipAuthRedirect` request option, set by the login page's own call, and by having the 401 path surface the backend's real `messageAr` instead of a hardcoded generic string.

### 26.3 What was built

- **`src/lib/auth/types.ts`, `session-context.tsx`**: `RoleName`, `AccountProfile`, and a `SessionProvider`/`useSession` pair. Deliberately never mounted at the root layout — only inside `(app)/layout.tsx` — since an unauthenticated visit to any `(app)` route would otherwise loop against its own bootstrap call.
- **`src/lib/auth/nav-items.ts`**: `getNavItemsForRole(role)`, the literal per-role menus from docs/12 §1.
- **`src/components/domain/role-badge.tsx`**: `RoleBadge` and `ScopeDisplay`.
- **`(auth)/login/page.tsx`, `forgot-password/page.tsx`**: mobile+password login and the three-step (request OTP → verify OTP → reset) forgot-password flow as one page with internal step state. Both use a real `<h1>` rather than shadcn's `CardTitle` (which renders a `<div>`) — the card's title *is* the page's one semantic heading here, not a nested one.
- **`(app)/layout.tsx`, `authenticated-shell.tsx`, `dashboard/page.tsx`, `audit/page.tsx`**: the authenticated shell wrapping `AppShell` (Phase F1) with real navigation and session data; `audit/page.tsx` calls `notFound()` for any non-Owner role per guide §8.6 (Admin must not access the Audit Log — a critical business invariant, not just a UI nicety).
- **`src/app/page.tsx`**: now a server-side `redirect('/dashboard')` — the Phase 1 placeholder root page is gone.
- **`src/lib/api-client.ts`**: `skipAuthRedirect` option (§26.2) and 401 responses now surface the backend's actual `messageAr`.

### 26.4 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Backend full suite, twice consecutively (cookie-policy fix regression check) | `dotnet test` | ✅ 211/211, both runs |
| Frontend unit tests | `npm run test --prefix frontend` | ✅ 33/33 |
| Type checking | `npm run typecheck --prefix frontend` (`tsc --noEmit`) | ✅ clean |
| Linting | `npm run lint --prefix frontend` | ✅ clean |
| Production build | `npm run build --prefix frontend` | ✅ succeeds, 6 static routes (`/`, `/login`, `/forgot-password`, `/dashboard`, `/audit`, `/_not-found`) |
| Browser E2E against a live Development backend (`dotnet run`, `ASPNETCORE_ENVIRONMENT=Development`) | `npx playwright test` | ✅ 3/3 — unauthenticated `/` → `/login` redirect chain (Arabic/RTL); login field visibility; invalid-credentials submission shows the Arabic error banner and stays on `/login` |

### 26.5 Not verified — genuinely out of Phase F2 scope

| Item | Why deferred |
| :--- | :--- |
| A successful login → `/dashboard` E2E case | No seeded Playwright-run user exists yet with a known password in this environment; the invalid-credentials case already exercises the full request/cookie/error-render path. A happy-path E2E case is natural to add once Phase T2's end-to-end pass seeds dedicated E2E fixtures. |
| Dashboard/Audit page real content (KPIs, activity feed, filterable log table) | Both are intentionally placeholder shells for this phase — Checkpoint boundaries assign real dashboard/audit content to later master-data/reporting phases. |

Full solution suite (backend): 211/211 passing, stable across two consecutive runs. Frontend: 33/33 unit tests, 3/3 E2E, clean typecheck/lint/build.

## 27. Phase F3 Verification Ledger

**Phase F3 is SIGNED OFF (2026-09-18).** The four master-data screens (docs/09 §Phase F3, guide §8.2) are implemented and verified end-to-end against a live Development backend with a real seeded Owner user, not just against mocks.

### 27.1 What was built

- **`src/lib/arabic-search.ts`**: `normalizeArabicSearch` - strips diacritics/tatweel, unifies alef forms, unifies `ة`→`ه` and `ى`→`ي` (docs/31 §4.2.3), applied to the Items search box before it reaches the server, matching how the server normalizes `name_normalized` on its side.
- **`src/lib/use-keyset-list.ts`**: a shared hook wrapping the backend's forward-only `KeysetPage<T>` (`{ items, nextCursor }`) with a client-side cursor stack, giving every list screen a working "previous" button even though the server only ever returns a forward cursor.
- **`src/components/features/simple-master-data-screen.tsx`**: one shared list+dialog CRUD screen for Categories, Units, Suppliers, and Warehouses - genuinely identical in shape (`nameArabic` + a few optional fields + an active/inactive toggle via deactivate/reactivate, never a hard delete). A `createOnly` field flag handles Warehouse's immutable `code`.
- **`src/components/features/restaurants-screen.tsx`**: its own screen, not a `SimpleMasterDataScreen` instance, because a restaurant's form genuinely differs - a required serving-warehouse `Select` at creation (ADR-028's `defaultServingWarehouseId`), and an existing restaurant's serving warehouse can be changed independently via its own `PUT .../serving-warehouse` endpoint.
- **`src/components/features/quick-add-modal.tsx`**: the guide §5.3 pattern - a small nested dialog that creates a Category or Unit inline and hands the created entity back to the caller for auto-select, with zero navigation and zero loss of the item form's other field values.
- **`src/components/features/item-conversions.tsx`**: the unit-conversion sub-section nested inside the item edit dialog (task 6.1) - the target unit is always the item's current base unit, resolved server-side (ADR-023); the form never shows or sends a "to" unit.
- **Pages**: `/master-data` (Categories/Units, tabbed), `/suppliers`, `/locations` (Warehouses/Restaurants, tabbed), `/items` (search, quick-add, read-only generated code, nested conversions) - the exact four routes `nav-items.ts` already pointed to from Phase F2.
- **shadcn `Tabs`/`Label`** added (`npx shadcn add tabs label`) - the only two primitives F1 hadn't already pulled in that F3 needed.

### 27.2 Three real bugs were found and fixed during this phase's own verification

All three were invisible to typecheck, lint, unit tests, and the production build - they only surfaced once a real Owner user (seeded directly via `AuthTestHelpers` against the dev database, then deleted from the test tree) drove the actual screens in a real browser against a live backend.

1. **`api-client.ts` crashed on every successful login.** `request()` only special-cased a `204` empty body; several endpoints - starting with login's own `Results.Ok()` with no value - return `200` with an equally empty body, and `.json()` on that throws a raw `SyntaxError`. The login page's `catch` block then showed its generic non-`ApiError` fallback text instead of navigating to `/dashboard`. This is the exact gap §26.5 flagged as unverified ("a successful login → `/dashboard` E2E case") - it went undetected through the whole of Phase F2 because that phase's only E2E case was the invalid-credentials path. Fixed by reading the response body as text first and only parsing it as JSON when non-empty, regardless of status code.
2. **Nested quick-add forms silently double-submitted the outer form.** `QuickAddModal` and `ItemConversions` each render their own `<form onSubmit>` nested inside the item dialog's outer `<form>`. Radix portals their `DialogContent` to `document.body`, but React re-dispatches a portaled element's synthetic events by walking the **React component tree**, not the DOM tree - so submitting the nested form also fired the outer form's `onSubmit`, submitting the item with whatever it currently held (e.g. an empty `baseUnitId` mid-selection), which crashed the backend with an unhandled `JsonException` (an empty string is not a valid `Guid`). Fixed with `event.stopPropagation()` in both nested submit handlers.
3. **A quick-added Category/Unit selection got silently reset.** After fixing bug 2, `onCreated` correctly called `setBaseUnitId(created.id)` - but Radix Select's hidden native `<select>` (kept in sync for autofill/native-form semantics) fired a stray `onValueChange('')` when the newly quick-added `<option>` mounted, clobbering the just-set value back to empty in the same render pass. Fixed by ignoring empty-string `onValueChange` calls on the Category and Base Unit selects - no real `SelectItem` in either list ever legitimately has `""` as its value, so the guard only ever rejects the spurious reset.

Item 1 is a genuine backend-response-shape gap in the frontend's central HTTP client, not specific to F3 - it would have affected every current and future screen's first mutating call the moment a user actually tried to use it for real. Items 2 and 3 are specific to the quick-add pattern this phase introduced.

### 27.3 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Backend full suite (unaffected by this phase, re-confirmed) | `dotnet test` | ✅ 212/212 |
| Frontend unit tests | `npx vitest run` | ✅ 33/33 |
| Type checking | `npx tsc --noEmit` | ✅ clean |
| Linting | `npm run lint` | ✅ clean |
| Production build | `npm run build` | ✅ succeeds, 10 static routes including the 4 new F3 routes |
| Permanent E2E suite (regression check after the `api-client.ts` fix) | `npx playwright test e2e/shell.spec.ts` | ✅ 3/3 |
| Manual full-flow browser verification against a live Development backend with a real seeded Owner user: login → create Category → create Supplier → create Warehouse → create Restaurant (assigning the warehouse) → create Item (quick-adding both its Category and Unit inline) → reopen the Item and confirm the conversions sub-section renders | One-off Playwright script, deleted after use (not part of the permanent suite - see §27.4) | ✅ full flow passes |

### 27.4 Not verified — genuinely out of Phase F3 scope

| Item | Why deferred |
| :--- | :--- |
| A permanent, committed E2E spec covering the F3 create flows | The manual verification script that found and confirmed the fixes for §27.2's three bugs was deliberately temporary (used a throwaway seeded Owner user and was deleted before commit) rather than added to the permanent suite, since a reusable version needs proper E2E test fixtures (a seeded Owner with a known password, reset between runs) that don't exist yet - the same gap §26.5 already flagged for the login happy path. Natural to close together in Phase T2's end-to-end pass. |
| Deactivate/reactivate and pagination behavior under real multi-page data volumes | Verified by direct code review and the shared `useKeysetList` hook's logic, not by manually creating enough rows to force a second page in this session. |
| Visual/RTL screenshot review of the four new screens | Same gap already recorded in §25.3 for F1 - no screenshot tooling wired into this session; functional/behavioral verification only. |

Full solution suite (backend): 212/212 passing. Frontend: 33/33 unit tests, all permanent E2E specs green, clean typecheck/lint/build, full manual create-flow verification against a live backend.

## 28. Phase F4 Verification Ledger

**Phase F4 is SIGNED OFF (2026-09-18).** The receiving draft→submit→verify→reverse lifecycle and the warehouse stock view (docs/09 §Phase F4, guide §8.2) are implemented and verified against a live Development backend, reproducing the docs/23 Scenario 1-2 numerical math exactly end to end.

### 28.1 What was built

- **`/receiving`**: list (status badge, document number, warehouse, business date) plus a create dialog (warehouse, optional supplier, business date) that navigates straight to the new draft's editor.
- **`/receiving/[id]`**: the draft editor and lifecycle screen in one, gated by status:
  - **Draft**: add/remove lines (item, unit - constrained to that item's base unit plus its active conversions, expected quantity, unit cost, notes); a "ترحيل الأمر إلى المخزن" submit action once at least one line exists.
  - **Submitted**: per-line actual-quantity inputs (defaulting to the expected quantity) and a "تسجيل المطابقة" verify action.
  - **Verified**: read-only, with a variance column (`actual - expected`) highlighted in the destructive color when non-zero.
  - **Reverse**: available from Submitted or Verified (never from Reversed - the backend's own state machine already forbids it; the button is simply not shown), behind a confirmation dialog requiring a reason.
  - Submit/Verify/Reverse each generate their own `X-Idempotency-Key` once per mount (`useState(() => crypto.randomUUID())`), never regenerated on retry, matching guide §8.4's confirmation-screen pattern applied here to receiving's three idempotent mutations.
- **`/locations/{id}/stock`**: balance/in-transit/available per item for one warehouse, with a "view stock" action added to the Warehouses tab via a new optional `rowActions` prop on `SimpleMasterDataScreen` (F3) - kept optional so Categories/Units/Suppliers, which have no equivalent action, are unaffected.
- **Cost visibility**: unit cost, line total, and average unit cost render only when `profile.role === 'Owner'` - columns are omitted entirely for every other role, on top of the backend already masking these same fields to `null` via `IFinancialProjection` for any non-Owner caller regardless of what the frontend does.

### 28.2 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Type checking | `npx tsc --noEmit` | ✅ clean |
| Linting | `npm run lint` | ✅ clean |
| Frontend unit tests | `npx vitest run` | ✅ 33/33 |
| Production build | `npm run build` | ✅ succeeds, 13 total routes including the 3 new F4 routes (`/receiving`, `/receiving/[id]`, `/locations/[id]/stock`) |
| Manual full-lifecycle browser verification against a live Development backend with a real seeded Owner user: create prerequisite category/unit/warehouse/item → create a receiving order → add a `100 KG @ 12.00 EGP` line → submit → verify with `actual = 80` (a `-20 KG` variance) → open the warehouse stock view | One-off Playwright script, deleted after use (same throwaway-fixture pattern as §27.3, not part of the permanent suite) | ✅ full flow passes, **zero backend exceptions** |
| Numerical correctness of the resulting warehouse stock, read back directly via `GET /api/v1/warehouses/{id}/stock` | `curl` against the live backend after the manual flow above | ✅ `balance=80.0000`, `available=80.0000`, `averageUnitCost=12.0000` - exactly docs/23 Scenario 1 (`+100`) followed by Scenario 2 (`-20` reconciliation, never `+20`/`-double-counted`) |

### 28.3 Not verified — genuinely out of Phase F4 scope

| Item | Why deferred |
| :--- | :--- |
| A permanent, committed E2E spec covering the receiving lifecycle | Same gap already recorded in §27.4 for F3's create flows - needs proper seeded E2E fixtures (a reusable Owner user with a known password), natural to add together in Phase T2. |
| Reverse action's actual execution (confirmed only that the button/dialog exists and gates correctly by status/permission) | The manual verification walkthrough stopped after Verify to keep the created order's data available for the stock-view check; Reverse's own ledger-reversal math is already covered by the Phase 8 backend integration tests (`ReceivingOrderTests.cs`) - this phase only needed to confirm the UI wires the existing, already-tested endpoint correctly, which the code review does establish (same request/idempotency-key pattern as Submit/Verify, which were both exercised). |
| Visual/RTL screenshot review of the new screens | Same gap already recorded in §25.3/§27.4 - no screenshot tooling wired into this session. |

Full solution suite (backend): 212/212 passing, unchanged by this phase. Frontend: 33/33 unit tests, clean typecheck/lint/build, full manual receiving-lifecycle verification against a live backend with exact numerical confirmation.

## 29. Phase F5 Verification Ledger

**Phase F5 is SIGNED OFF (2026-09-18).** The multi-item supply request editor, fulfilment queue, dispatch, receipt confirmation, and discrepancy views (docs/09 §Phase F5, guide §8.3/§8.4 - "the single most important screen in the product" / "the highest-value E2E surface in the product") are implemented and verified end-to-end against a live Development backend, with a real cross-role walkthrough (Restaurant Supervisor + Warehouse Staff, not just Owner).

### 29.1 A real backend access gap was found and fixed during this phase's own verification

Live-browser testing as an actual Warehouse Staff / Restaurant Supervisor user (not Owner, whose broader permissions had silently masked this through Phases F3 and F4) found that the "طلب جديد" (new supply request) create dialog could never enable its own submit button: `scopedRestaurants` stayed permanently empty because the frontend resolved restaurant/warehouse/supplier/unit names via the `:manage`-gated list endpoints (`GET /api/v1/restaurants`, `/warehouses`, `/suppliers`, `/units`), and Warehouse Staff/Restaurant Supervisor hold **none** of those four `:manage` permissions (docs/03 - all four ❌ for both roles). The 403 was silently swallowed by a `.catch(() => setX([]))`, so the screen degraded to a permanently-empty, permanently-disabled create flow with no error shown - a real defect, not a test artifact.

Fixed (commit `ebb05c3`, ahead of and independent from this phase's own frontend work, since it also retroactively affects Phase F4's receiving screens for the exact same reason) by adding a `GET .../names` endpoint per resource (warehouses, restaurants, suppliers, units), gated only by `RequireAuthorization()` (any authenticated tenant user) rather than the resource's `:manage` policy. Each returns only `id`/`nameArabic`(/`code`) - never `address`/`description` or anything else `:manage` would additionally expose - since a name is not sensitive information within a tenant the caller already belongs to, while the full manage-gated list endpoints are completely unchanged. Two new integration tests (`WarehousesAndRestaurantsEndpointTests`, `UnitsAndSuppliersEndpointTests`) prove a Warehouse Staff/Restaurant Supervisor user can reach `/names` while the full list correctly still 403s them. The frontend's `/receiving`, `/receiving/[id]`, `/supply-requests`, `/supply-requests/[id]`, and `/supplies/[id]` pages were all switched from the manage-gated lists to `/names`.

### 29.2 What was built

- **`/supply-requests`, `/supply-requests/[id]`**: no warehouse selector anywhere (ADR-028) - the create dialog only ever asks for a restaurant (auto-selected when the Supervisor has exactly one in scope), and the resulting request's server-derived `warehouseId` is shown read-only in the header. The editor (Draft: add/remove lines via a dialog, `[ إرسال الطلب الى المخزن ]` disabled at zero lines; read-only once Submitted, with a `[ إلغاء الطلب ]` action) and the fulfilment panel (Submitted/PartiallyFulfilled only, Warehouse Staff: per-line fulfilled-quantity input defaulted to and capped at the *remaining* quantity - `requested − alreadyFulfilled`, which is the only value that is actually valid given docs/09's own partial-fulfilment semantics - with an amber advisory, never a block, when it exceeds the warehouse's live `available` balance per ADR-019) live on the same page, since a Supervisor can never edit once Submitted and Warehouse Staff can never fulfil a Draft. Adding an item already on the request relies entirely on the backend's own CR-023 merge (`AddLineAsync` merges into the existing line for a matching item+unit) rather than reimplementing that detection client-side.
- **`/supplies`, `/supplies/[id]`**: dispatch is a plain status-transition button with no form - the dispatched quantities are exactly what fulfilment already set, so there is nothing new to enter. Confirmation clamps received quantity to `0..dispatched`, shows a pre-submit dialog summarizing every line where received ≠ dispatched, and submits once with an `X-Idempotency-Key` generated once per page mount (`useState(() => crypto.randomUUID())`), never regenerated on retry (guide §8.4). Deliberately has **no** per-line "reason" field despite the guide's prose mentioning one: the real `ConfirmSupplyLineCommand` contract (verified against the actual backend, not the guide) carries no such field - a variance instead surfaces automatically as a `SupplyReceiptVariance` discrepancy, where a reason is captured at *resolve* time via `/discrepancies/{id}/resolve`, not at confirm time. Backend contract wins over guide prose per this session's standing reconciliation rule; inventing an unsent field would be exactly the UI theater docs/12 §2.3 forbids.
- **`/discrepancies`**: the four variance types (`ReceivingVariance`, `SupplyReceiptVariance`, `StockCountVariance`, `StockUnavailableAtConfirmation`) in one screen, scoped server-side per docs/03, with a `[ معالجة ]` resolve action requiring a reason.
- Every list/detail page's document-number cell is now a clickable link to the detail route on **desktop**, not just the mobile card - a gap noticed and fixed retroactively across `/receiving`, `/supply-requests`, and `/supplies` while building this phase (desktop previously had no way to open a row at all).

### 29.3 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Backend full suite, twice consecutively | `dotnet test` | ✅ 215/215 (was 212; +2 for the `/names` access tests), both runs |
| Frontend unit tests | `npx vitest run` | ✅ 33/33 |
| Type checking | `npx tsc --noEmit` | ✅ clean |
| Linting | `npm run lint` | ✅ clean |
| Production build | `npm run build` | ✅ succeeds, 17 total routes |
| Manual cross-role browser verification against a live Development backend with real seeded Owner/Warehouse-Staff/Restaurant-Supervisor users: Restaurant Supervisor creates and submits a supply request (no warehouse selector visible, confirmed by locator assertion) → Warehouse Staff fulfils and dispatches → Restaurant Supervisor confirms with a deliberate variance (received 8 of dispatched 10) → summary dialog correctly lists the variance → confirm → status becomes `ConfirmedWithDiscrepancy` | One-off Playwright script, deleted after use (same throwaway-fixture pattern as §27.3/§28.2) | ✅ full cross-role flow passes, **zero backend exceptions** |
| Numerical correctness of the resulting warehouse stock, read back directly via `GET /api/v1/warehouses/{id}/stock` | `curl` against the live backend after the manual flow above | ✅ `100 → 92` - exactly `-8` (confirmed receipt only); dispatch produced zero stock movement, matching the critical business invariant "Dispatch does NOT reduce warehouse stock; confirmed restaurant receipt DOES" |
| The variance's automatic `SupplyReceiptVariance` discrepancy appears on `/discrepancies` | Same manual walkthrough | ✅ visible immediately after confirm, no manual creation step |

### 29.4 A process note: Arabic text corruption via inline Bash `curl -d`

While seeding verification data directly via `curl` for this phase's manual walkthrough, inline `-d '{"nameArabic":"..."}'.` JSON payloads containing Arabic text were silently corrupted to `???` at the byte level before curl even sent them - not a terminal display artifact, but real mangled data that was actually stored in the database and rendered as `???` in the real browser too. Root cause: Git Bash/Windows console's non-UTF-8 default codepage mangling the inline string literal. Fixed by writing the JSON payload to a file first (a normal file write, which correctly preserves UTF-8) and using `curl --data-binary @file.json` instead of an inline `-d` string. Noted here because it is a real trap for any future session seeding Arabic verification data via shell one-liners on this machine - it produces no error, only wrong data.

### 29.5 Not verified — genuinely out of Phase F5 scope

| Item | Why deferred |
| :--- | :--- |
| A permanent, committed E2E spec covering the cross-role supply-request lifecycle | Same gap already recorded in §26.5/§27.4/§28.3 - needs proper seeded E2E fixtures (Owner + Warehouse Staff + Restaurant Supervisor, each with a known password and pre-established scope), natural to add together in Phase T2. |
| The `PartiallyFulfilled` status path specifically (fulfilling less than the full remaining quantity, leaving a request partially open across two separate `Supply` documents) | The manual walkthrough fulfilled the full requested quantity in one pass; the partial-fulfilment code path (remaining-quantity default/cap logic in the fulfilment panel) was verified by code review against docs/09's documented semantics, not by driving a second, smaller fulfilment through the browser. |
| Visual/RTL screenshot review of the new screens | Same gap already recorded in §25.3/§27.4/§28.3 - no screenshot tooling wired into this session. |

Full solution suite (backend): 215/215 passing, stable across two consecutive runs. Frontend: 33/33 unit tests, clean typecheck/lint/build, full manual cross-role supply-workflow verification against a live backend with exact numerical confirmation.

## 30. Phase F6 Verification Ledger

**Phase F6 is SIGNED OFF (2026-09-18) - every frontend phase in docs/09 is now complete.** The role-specific action dashboards (docs/09 §Phase F6, docs/12 §2) replace the Phase F2 placeholder, verified against a live Development backend as all three operational roles.

### 30.1 What was built

- **`src/components/features/dashboard-widget.tsx`**: a shared widget - title, a live count badge, up to 5 preview rows, a "عرض الكل" link to the full list screen. Every one of the five mandatory API states (guide §6) applies: loading skeleton, error banner with retry, empty state, populated list, and the count badge itself only ever reflects `filtered.length` from a real response - never a literal `0` written by hand. The list endpoints these widgets call have no server-side status filter, so each widget fetches a bounded page (`limit=200`, matching this session's `/names` endpoints' own cap) and filters/counts client-side - documented in the component's own doc comment as a real, honest count capped at 200 rather than a claimed exact total beyond that.
- **`/dashboard`**: role-branched. **Warehouse Staff** ("مهام المخزن" - docs/12 §2.1): pending restaurant requests awaiting fulfilment, incoming receiving orders (Draft/Submitted), open receipt discrepancies, plus the two specified CTAs. **Restaurant Supervisor** ("مهام الفرع" - docs/12 §2.2): dispatched supplies awaiting confirmation, active branch orders, open receipt discrepancies, plus its two CTAs. **Owner/Admin**: the same three real, tenant-wide widget families under an "overview" framing - docs/09/docs/12 specify no separate bespoke Owner/Admin widget set beyond "an overview", so reusing the same already-tested queries tenant-wide is more honest than inventing unspecified bespoke figures.

### 30.2 A real, pre-existing bug from Phase F1 was found and fixed

Live-browser testing as Warehouse Staff and Restaurant Supervisor specifically (not Owner, whose dashboard branch has no `asChild` buttons) crashed the page entirely with a Chromium-level "This page couldn't load" error the moment the dashboard rendered. Root cause, found via `page.on('pageerror')`: `Button`'s render body was `{loading && !asChild ? <Loader2/> : null}{children}` - in JSX, two adjacent expression containers always produce **two** entries in the children array passed to the underlying element, regardless of what the first one evaluates to (even `null`). When `asChild` is true, that element is Radix's `Slot.Root`, which requires **exactly one** child and throws ("Slot failed to slot onto its children") on an array of two - crashing the render. This bug has existed since Phase F1 wrote `Button`, but no F1–F5 screen ever used `<Button asChild>`; this phase's dashboard CTAs (`<Button asChild><Link>...</Link></Button>`) were the first callers, and it was only caught because verification was done as the actual scoped roles, not just as Owner.

Fixed by branching the render itself: when `asChild`, render only `children` (satisfying Slot's exactly-one-child requirement); otherwise render the optional loader and `children` together inside their own `Fragment`, which has no such constraint for a plain `<button>` element.

### 30.3 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Type checking | `npx tsc --noEmit` | ✅ clean |
| Linting | `npm run lint` | ✅ clean |
| Frontend unit tests | `npx vitest run` | ✅ 33/33 |
| Production build | `npm run build` | ✅ succeeds, 17 routes, unchanged route count (dashboard is not a new route) |
| Permanent E2E suite, re-run after the `Button` fix to confirm no regression | `npx playwright test e2e/shell.spec.ts` | ✅ 3/3 |
| Manual browser verification against a live Development backend as all three operational roles: Owner (heading, all three tenant-wide widgets, no stuck skeletons), Warehouse Staff (heading, both CTAs, scoped widgets), Restaurant Supervisor (heading, both CTAs, scoped widgets) | One-off Playwright script, deleted after use (same throwaway-fixture pattern as §27–§29) | ✅ all three pass after the `Button` fix (failed with the Slot crash beforehand) |

### 30.4 Not verified — genuinely out of Phase F6 scope

| Item | Why deferred |
| :--- | :--- |
| Admin dashboard specifically | Admin shares the exact same code branch as Owner (the `else` fallback, differing only in heading text) - verified by code review, not a separate seeded Admin user in this session's manual walkthrough. |
| A permanent, committed E2E spec covering all four dashboard variants | Same gap already recorded in §26.5/§27.4/§28.3/§29.5 - needs proper seeded E2E fixtures for all four roles, natural to add in Phase T2. |
| A repository-wide automated scan proving zero hardcoded `0`/mock array/placeholder chart (docs/09's own stated F6 acceptance criterion) | Verified by direct code review of every widget in this phase (each renders `filtered.length` from a live fetch, never a literal number) rather than a scripted repository grep; no such scan script exists in this session's tooling. |

Full solution suite (backend): 215/215 passing, unchanged by this phase. Frontend: 33/33 unit tests, clean typecheck/lint/build, full manual cross-role dashboard verification against a live backend, plus a genuine Phase-F1-era bug found and fixed.

---

## 31. Post-F6 Reconciliation Sweep — Findings & Fixes

With every individual phase in §2 signed off, a full plan-to-code-to-DB-to-frontend reconciliation sweep was run rather than assuming completeness. It found and closed two real, previously-undetected gaps that no single phase's own verification had caught, because each phase verified its own slice in isolation rather than the cross-phase seam between them.

### 31.1 Gap 1 — `/dashboard` had no branch for the `User` (general) role

`docs/03 §1.5` and the F6 dashboard work (§30) specify widget sets for Owner/Admin/Warehouse Staff/Restaurant Supervisor, but a user with role `User` (the unprivileged default role created without a role assignment) fell through to the Owner/Admin branch, which calls tenant-wide list endpoints that role holds no permission for - silently rendering broken/error widgets instead of a coherent screen. Fixed by adding an explicit `User`/`null`-role branch (`src/app/(app)/dashboard/page.tsx`) that renders a neutral welcome message pointing the user at the sidebar instead of attempting any privileged fetch. Committed `312ad1b`.

### 31.2 Gap 2 — `/users` had a fully-built backend and zero frontend screen

`nav-items.ts` has linked Owner/Admin to `/users` since Phase F2, and Phase 4 (task 4.14) built the complete backend CRUD (`POST/GET /api/v1/users`, `GET/PUT .../role`) - but the route itself 404'd; no page ever existed. This is the same "real need, no route" failure shape as the F5 `/names` gap (§29), just on the admin side instead of the operational side. While closing it, two further backend gaps in the same surface were found:

- **No way to view or set a user's warehouse/restaurant scope.** `IScopeGuard` has consumed `UserScope` since Phase 4, but nothing ever exposed it for editing after initial creation. Added `GET`/`PUT /api/v1/users/{id}/scope`, gated by the existing `users:scope` policy.
- **No way to deactivate a user, and deactivation alone does not kill a live session.** `User.Deactivate()`/`Reactivate()` domain methods existed since Phase 2/3 (consumed only by login's `ACCOUNT_INACTIVE` check) but had no endpoint. Worse: `OnValidatePrincipal` (the per-request cookie validator) only checks `SignInManager.ValidateSecurityStampAsync` - it does **not** independently re-check `IsActive` - so a plain `IsActive = false` write would have left an already-signed-in session valid until its cookie naturally expired, which is a genuine session-hygiene gap the directive's security-priority ordering (Authorization > Auditability) requires closing. Fixed by adding `POST /api/v1/users/{id}/deactivate`/`reactivate` (`users:manage`), where deactivate does `user.Deactivate()` → `SaveChangesAsync()` → `UserManager.UpdateSecurityStampAsync(user)` in that order, which rotates the stamp `OnValidatePrincipal` checks on the very next request from any existing session for that account.
- **No permission catalogue endpoint.** The permissions-editing UI needs the full list of grantable permission codes with Arabic descriptions; added `GET /api/v1/permissions` (`users:manage`), sourced from the existing `Permission` seed data via a new `IUserManagementService.ListPermissionCatalogueAsync`, correctly marking the four Owner-reserved codes (`costs:view`, `valuation:view`, `audit:view`, `audit:export`) as `isGrantable: false`.

Backend: `src/Inventory.Application/Users/IUserManagementService.cs`, `UserManagementModels.cs`; `src/Inventory.Infrastructure/Users/UserManagementService.cs`; `src/Inventory.Api/Features/Users/UsersEndpoints.cs`; 6 new tests in `tests/Inventory.IntegrationTests/AuthorizationTests.cs`, including one that logs a target user in, deactivates them from a **separate** authenticated session, and re-uses the target's original (unrefreshed) cookie to assert `/api/v1/account/me` now returns `401`. Committed `456ae04`.

Frontend: `src/app/(app)/users/page.tsx` (list + create dialog) and `src/app/(app)/users/[id]/page.tsx` (role change, scope assignment via the existing `/warehouses/names` and `/restaurants/names` endpoints from §29, a permissions checklist built from the new catalogue endpoint, deactivate/reactivate) - both gated on `users:view`/`users:manage`/`users:scope` exactly as the backend enforces, and both disable self-targeting mutations (a user cannot change their own role/scope/active state) client-side as a UX courtesy on top of the server's own privilege-escalation guard. Committed `35e6565`.

### 31.3 Verified — executed, output observed

| Check | Command / Method | Result |
| :--- | :--- | :---: |
| Backend build | `dotnet build` | ✅ 0 warnings, 0 errors |
| Backend full suite | `dotnet test` | ✅ 220/220 |
| Frontend type checking | `npm run typecheck` | ✅ clean |
| Frontend linting | `npm run lint` | ✅ clean |
| Frontend production build | `npm run build` | ✅ succeeds; `/users` and `/users/[id]` now present as real routes (19 routes total, up from 17) |
| Live end-to-end verification against the Development backend, as Owner, via direct authenticated HTTP calls (CSRF token → cookie session) | Manual `curl` walkthrough: list users, fetch permission catalogue, create a `WarehouseStaff` test user, `GET`/`PUT` its scope, `PUT` its permissions, log that user in on a **separate** session, deactivate from the Owner session, confirm the still-open second session's `/api/v1/account/me` now returns `401` with the session cookie cleared, reactivate | ✅ every step succeeded exactly as designed; the session-kill-on-deactivation behavior was reproduced live, not just in the integration test |

### 31.4 Not verified — genuinely out of this sweep's scope

| Item | Why deferred |
| :--- | :--- |
| A permanent, committed E2E spec for the Users screens | Same recurring gap as §26.5/§27.4/§28.3/§29.5/§30.4 - proper seeded multi-role E2E fixtures belong in Phase T2, not ad hoc per-fix scripts. |
| Change-password-via-OTP and mobile-number-change self-service flows | Referenced by an out-of-band directive but absent from `docs/09`'s task list; per this document's own stated resolution order (docs/09 is authoritative for scope), left out of this sweep pending an explicit product decision to add them to the plan. |
| The remainder of the broader plan-to-code traceability matrix (full endpoint-by-endpoint, entity-by-entity sweep) | This sweep closed the two highest-value gaps found so far (a broken role-dashboard render and a fully-missing admin surface with a real session-hygiene defect); the wider sweep continues in the next work session rather than being declared complete here. |

Full solution suite (backend): 220/220 passing. Frontend: clean typecheck/lint/build, both new routes verified live end-to-end as Owner including the security-critical deactivation session-kill behavior.

### 31.5 Gap 3 & 4 — `/consumption` and `/stock-counts` had the exact same shape of gap

Continuing the sweep past §31.2, `nav-items.ts` was checked against every registered backend route group. Two more `nav-items.ts` entries pointed at routes that never existed, both with a fully complete backend and zero frontend code:

- **`/stock-counts`** (Owner/Admin): the entire Phase 13 physical-count workflow (`create` → `record` → `submit` → `approve`/`reject`) already existed and was already integration-tested, including the blind-count leak-prevention design (`SystemQuantity`/`Variance` withheld from the wire while `InProgress`). No UI existed to reach any of it.
- **`/consumption`** (Owner): REQ-07's consumption log (`POST`/`GET /api/v1/consumption`) existed with the same gap.

Both were built following the exact same established screen patterns as every other Phase F module (list + create dialog for consumption; list + create dialog + a detail/record/submit/approve page for stock counts, mirroring `receiving/[id]`'s draft-editor shape). No backend changes were needed - both endpoint surfaces were already complete and correct.

**Verified live** against the Development backend as Owner via direct authenticated HTTP calls: recorded a consumption entry and confirmed it listed back correctly; ran a full stock count against a warehouse with real stock (`مخزن بن بلال`, item balance 15) - created the count, recorded a physical quantity of 13, submitted for approval, approved it, and confirmed three things simultaneously: (1) the approval posted a real `PHYSICAL_ADJUSTMENT` and the warehouse balance actually moved to 13, (2) the item's `averageUnitCost` (WAC) was unchanged by the adjustment per ADR-020, and (3) a `StockCountVariance` discrepancy record was created automatically and now appears in `/discrepancies`. This is the same numeric-correctness verification standard applied to every prior phase's stock-affecting operation (§28, §29), not just a UI smoke test.

Typecheck/lint/build all clean; 4 new routes registered (`/consumption`, `/stock-counts`, `/stock-counts/[id]` — `/users`/`/users/[id]` from §31.2 already counted). Committed `a759f41`.

### 31.6 A known, deliberately-unclosed gap: `/settings`

`nav-items.ts` also links Owner ("إعدادات المنشأة") and Admin ("الإعدادات التشغيلية") to `/settings`, which also has no frontend page — but unlike the three gaps above, there is **no backend surface to wire it to either**: no `/api/v1/companies` or `/api/v1/settings` endpoint exists anywhere in `src/Inventory.Api/Features/`. `company_settings` exists only as a migrated table consumed internally for tenant-timezone period derivation (§9, task 5.2) — docs/09 never scheduled a CRUD task for it. This is recorded here rather than silently left as an unexplained dead nav link: it is **not** the same class of gap as §31.2/§31.5 (backend-complete, frontend-missing) — it is genuinely unscoped in both layers, and building a settings page/endpoint now would be exactly the kind of speculative, plan-unauthorized feature this document's own discipline section forbids. Left as an open item for an explicit product/plan decision, not silently fixed.

Full solution suite (backend): 220/220 passing, unchanged by §31.5 (no backend edits). Frontend: clean typecheck/lint/build, both new modules verified live end-to-end as Owner with real stock-affecting numeric verification.

---

## 32. Product-Change Batch (2026-09-20) — Ledger

Six explicit product changes, each traced DB → service → authorization → API → frontend → tests. **Full backend suite: 238/238 (27 unit, 19 architecture, 192 integration).** Frontend typecheck/lint/build clean; Playwright specs `supply-workflow`, `users-management`, `product-changes` pass against the live backend.

| # | Change | Status | Notes |
| :-: | :--- | :---: | :--- |
| 1 | Remove restaurant "serving warehouse" | ✅ | **Reverses ADR-028** (explicit product decision: a restaurant may receive from several warehouses). Column/FK/index dropped by forward-only migration `RemoveRestaurantServingWarehouse`; `PUT /restaurants/{id}/serving-warehouse` removed; `POST /supply-requests` now takes an explicit `warehouseId`, validated server-side (exists, Active, same tenant via the filtered DbSet; error `409 WAREHOUSE_UNAVAILABLE`, renamed from `SERVING_WAREHOUSE_UNAVAILABLE`). Historical requests keep their own persisted `warehouse_id`. Restaurant Supervisor may pick any active company warehouse (no warehouse-scope concept exists for that role). docs/04 §8.2, docs/06, docs/07, docs/09-authorization-security §2.3 and plan tasks 2.18a/5.9/9.8/9.8a/9.12–9.14 still describe the old model and are **superseded by this section** (not rewritten - the plan stays the roadmap). |
| 2 | "أمر توريد" → "أمر استلام" | ✅ | Frontend-only drift: backend audit text already said "أمر استلام". The warehouse→restaurant "توريد" feature is deliberately unchanged. |
| 3 | Requested > available stock | ✅ verified, **no change** | Already implemented and tested: `StockPostingService.DeductAsync` is a single conditional atomic `UPDATE … WHERE quantity >= n`; `InsufficientStockException` → `400 INSUFFICIENT_STOCK` with Arabic message "الرصيد المتاح في المستودع غير كافٍ لإتمام العملية."; existing tests cover concurrency, depleted balance, cross-branch. Fulfilment intentionally stays advisory (ADR-019) - the hard block is at stock posting, and the new direct issue uses the same path. |
| 4 | أمر صرف (Owner/Admin direct issue) | ✅ | `POST /api/v1/supplies/direct-issue` (idempotent, CSRF): one transaction creates a Supply with no request, walks Prepared→Dispatched→Confirmed, deducts via the existing posting service, audits `SUPPLY_DIRECT_ISSUED`. New permission `supplies:direct_issue` seeded to Owner/Admin only (migration `AddDirectIssueCapabilities`; existing codes did not fit). Response carries no cost fields. 10 integration tests (roles, cross-company, insufficient stock, replay, concurrency). UI: button + dialog on `/supplies`, base-unit quantities only. |
| 5 | Settings page | ✅ | Root cause: `/settings` had **no page and no backend**. Added `GET/PUT /api/v1/settings` over `company_settings` (timezone editable + audited, currency/company read-only), permission `settings:manage` Owner/Admin (migration `AddSettingsCapability`), 8 tests, real page. |
| 6 | الحركات / audit page | ✅ | Root cause: the page was a "coming later" placeholder over the fully built Phase 14 backend. Now a real Owner-only table (filters, keyset pagination, detail dialog). Backend tests already cover Owner/Admin 403, secrets, export; tenant isolation added. |

### 32.1 Integration audit (screen → endpoint)

Every nav entry now has a page and every `apiClient` path maps to a registered route (checked by grep of routes vs calls; the only broken links found were `/settings`, fixed above). No dead buttons remain among the audited screens.

### 32.2 Not verified / open

- Direct issue is verified by integration tests and role-gated browser checks, but a full browser walkthrough of the dialog with real stock was not run.
- Direct issue accepts base-unit quantities only (no unit-conversion picker).
- Currency is read-only by design; no company-name editing exists in the plan.
- Two pre-existing timing-flaky tests (`IdempotencyServiceTests.Cleanup…`) can fail under parallel load; pass in isolation.

### 32.3 Questions for Mohamed

1. Should restaurants be restricted to a *set* of allowed warehouses (a join table), or is "any active company warehouse" correct?
2. Should direct issue support non-base units (conversion picker)?
3. Which additional settings (company name, working hours, low-stock thresholds) should the Settings page expose - none are defined in the plan today?

---

## 33. Role-Specific Workspace Completion (2026-09-20)

Inspection first; only gaps were changed. **Already implemented and left untouched:** role-scoped navigation (Warehouse Staff / Restaurant Supervisor menus carry no users/settings/audit/master-data/reports links), Owner-only financial projection with Admin/Staff/Supervisor receiving `null` costs server-side (`IFinancialProjection`), server-generated item codes (`ITM-000006`), Arabic-only items with no English-name/barcode anywhere in DB/API/UI, full and partial fulfilment (`PartiallyFulfilled`), automatic `SupplyReceiptVariance`/`ReceivingVariance` discrepancies, request-review and receipt-confirmation dialogs at their own lifecycle stages, and units/categories `GET/POST/PUT` (+deactivate/reactivate) under `units:manage` / `categories:manage` (Owner/Admin only; Staff/Supervisor get 403). Accountant stays retired.

**Changed:** (1) the Restaurant Supervisor dashboard "+ طلب بضاعة جديد" now opens the creation dialog directly on the dashboard (shared `CreateSupplyRequestDialog`, also used by the list page) instead of navigating to the list first; (2) Warehouse Staff navigation gained "سجل الفروقات" (they already hold `discrepancies:view` and discrepancy handling is one of their workflows).

**Verified** by `frontend/e2e/role-workflows.spec.ts` (real browser + real backend): direct-from-CTA creation, role navigation, direct API/URL 403s for users/settings/audit/units/categories, full approval + receipt (stock −10 exactly), partial approval (6/10) + partial receipt (4/6) → discrepancy −2 visible in UI, and Owner-vs-Admin/Staff cost visibility. Suite: backend 238/238, frontend 33/33 unit, typecheck/lint/build clean.

**Not changed / open:** the supply-request state machine has no *Rejected* state - only Supervisor `Cancel` - so warehouse "reject" is not implemented (the brief allows it only "when the existing workflow allows rejection"). Adding it needs a product decision (below). The create flow is dialog (restaurant + warehouse) → editor page (items/quantities) → review dialog → submit; it was not merged into a single form.

**Decision needed from Mohamed:** should Warehouse Staff be able to *reject* a submitted request (new terminal status + reason + `supply_requests:fulfill`-gated endpoint), or is Supervisor-side cancellation enough?

---

## 34. Decisions applied to §32.3 (2026-09-20)

1. **Allowed warehouses - implemented.** New table `restaurant_warehouses` (migration `AddRestaurantWarehouses`; composite tenant FKs to `restaurants` and `warehouses`, unique `(restaurant_id, warehouse_id)`; existing (restaurant, warehouse) pairs already used by supply requests were backfilled so current behaviour is preserved). `PUT /api/v1/restaurants/{id}/warehouses` (`restaurants:manage`, Owner/Admin) replaces the set - every id must be an Active warehouse of the caller's company, else `409 WAREHOUSE_UNAVAILABLE`; changes are audited (`RESTAURANT_ALLOWED_WAREHOUSES_CHANGED`). `GET /api/v1/restaurants/{id}/warehouses` returns the Active allowed warehouses (Owner/Admin any; Restaurant Supervisor only for their own scoped restaurants, else 403). `POST /supply-requests` now requires the warehouse to be allowed for the restaurant AND Active AND same company. UI: Owner/Admin get a "المخازن المسموح بها" dialog per restaurant; the supervisor's form lists only the allowed warehouses of the chosen restaurant and says so when there are none. This supersedes the "any active company warehouse" wording in §32.
2. **Direct issue in non-base units - implemented.** The server already resolved the unit through `IUnitConversionResolver`; the dialog now offers the item's base unit plus its ACTIVE defined conversions per line. 1 carton = 12 → 2 cartons deducts 24 from the base balance and the ledger row carries base quantity -24; an undefined conversion returns `CONVERSION_NOT_DEFINED` (Arabic message) and writes nothing; a client-posted factor/base quantity is ignored.
3. **Settings - unchanged by design.** The page shows only what the model defines: company name and currency (read-only) and the timezone (editable, audited). No working hours, no global low-stock threshold; low-stock/reorder stays item-level.

**Tests:** backend 206 integration + 27 unit + 19 architecture; only `IdempotencyServiceTests.Cleanup_Removes_Only_Expired_Records` (pre-existing timing flake) can fail under parallel load. New: `RestaurantWarehouseTests` (allowed/denied/inactive/cross-company/role/scope/DB-level FK), 3 direct-issue unit-conversion tests; browser: `allowed-warehouses-and-units.spec.ts` plus all earlier specs re-run green.


---

## 35. User Management Completion + Master-Data Lifecycle (2026-09-20)

### 35.1 Owner user management (DB → Domain → App → Infra → API → Auth → UI → tests)
- **Creation** (`POST /users`, `users:manage`): role validated server-side (`Enum.IsDefined` + role row; Accountant/unknown → `INVALID_ROLE`; Owner not creatable), name, mobile (10–15 digits, normalized), scope by role (Warehouse Staff → warehouse ids; Restaurant Supervisor → restaurant ids; Admin/User → none) applied in the same transaction; client-sent company/permissions are ignored; audited.
- **Global mobile uniqueness** — decision: login resolves the mobile across companies, so uniqueness is global: DB unique index `uq_users_mobile` (migration `AddGlobalUniqueMobile`) + service pre-check → `DUPLICATE_MOBILE`. Mobile is an identifier only, never an authorization input.
- **Admin password reset** (`POST /users/{id}/password`, Owner only, target non-Owner, never self): Identity password policy, sessions revoked (security-stamp rotation), audited as `USER_PASSWORD_RESET_BY_ADMIN` **without** any secret; response is 204 (nothing echoed). Admin gets 403.
- **Owner's own credentials** (`/api/v1/account/*`, Owner only): password change = OTP to the currently registered mobile → verify → new password + confirm; mobile change = OTP sent to the NEW number, becomes active only after verification (time-limited Data-Protection token, 10 min). Owner forgot-password uses the normal OTP flow. Audited `OWNER_PASSWORD_CHANGED_WITH_OTP` / `OWNER_MOBILE_CHANGED_WITH_OTP`. No production mobile number is hard-coded anywhere.
- **Profile edit** (`PUT /users/{id}`): name for any manageable user; mobile change Owner-only; Owner target refused. **Permissions**: `GET /users/{id}/permissions` (effective codes) feeds the editor, which now sends only changed grantable codes.
- UI: `/users` create dialog with scope pickers, `/users/[id]` profile + password + permission cards, Owner-only `/account`.

### 35.2 Lifecycle matrix (decision record)
| Entity | Lifecycle | DELETE endpoint | Deactivate/Archive | Who | Frontend | Audit | Hard-delete safe? |
|---|---|---|---|---|---|---|---|
| Items | Active/Inactive | Yes — only if never referenced | Yes | `items:*` policy (Owner/Admin) | trash + confirm; power toggle | `ITEM_DELETED` / deactivate | Only when unreferenced (FK 23503 → 409 `IN_USE`) |
| Categories, Units, Suppliers | Active/Inactive | Yes — unreferenced only | Yes | Owner/Admin | trash + confirm | `*_DELETED` | Only when unreferenced |
| Warehouses | Active/Inactive | Yes — unreferenced only (no ledger, docs, mappings-to-requests) | Yes | Owner/Admin | trash + confirm | `WAREHOUSE_DELETED` | Only when unreferenced |
| Restaurants | Active/Inactive | Yes — unreferenced only (its allowed-warehouse mapping cascades) | Yes | Owner/Admin | trash + confirm | `RESTAURANT_DELETED` | Only when unreferenced |
| Users | Active/Inactive | **No** (identity anchors audit + documents) | Yes (sessions revoked) | Owner/Admin per rules | power toggle | yes | Never |
| Supply requests | state machine | No | Cancel (Supervisor) | scoped | cancel | yes | Never |
| Receiving orders, Supplies | state machine | No | Cancel / Reverse | permission-gated | existing actions | yes | Never (ledger references) |
| Stock counts, Discrepancies | state machine | No | Void/resolve via workflow | permission-gated | existing | yes | Never |
| Ledger, Audit log | append-only | No | — | — | — | — | Never |

Every delete: permission policy + antiforgery filter; tenant filter makes cross-company ids 404; explicit UI confirmation; FK violation is the authority for "referenced" (409 `IN_USE`, Arabic message steering to deactivation, nothing removed); creation audit rows survive. **Not implemented (needs product decision):** discarding a draft receiving order, cancelling a stock count.

### 35.3 Verification
Backend: `OwnerUserManagementTests` (32) and `MasterDataLifecycleTests` (9: unused delete + audit + history, referenced → 409 intact, 403 for Staff/Supervisor/User, Admin allowed, cross-company/unknown 404, CSRF). Frontend typecheck/lint clean.

### 35.4 Questions for Mohamed
1. No production SMS provider exists (`ConsoleSmsSender` only) — OTP flows work end-to-end in dev/tests but real delivery needs a provider decision.
2. May the Owner create another Owner? Currently no.
3. For the Owner's mobile change, OTP goes to the NEW number only; should the current number also be verified?
4. Draft receiving-order discard and stock-count cancel: implement?

### 35.5 Browser verification (real frontend → API → DB)
`frontend/e2e/user-security-and-lifecycle.spec.ts` (4 tests): non-Owner blocked from `/account` and OTP endpoint 403; Owner OTP flow shows no password field before verification (tolerates the 3/15-min OTP limiter); Owner creates a scoped WarehouseStaff through the UI, resets their password (204, empty body) and the target logs in with it; delete of an unused category needs confirmation, cancel keeps it, confirm removes it. All 8 Playwright specs pass when run one file at a time (the 5/min/IP login limiter makes a single back-to-back run 429 — the limiter is intentionally unchanged). Backend: 247 integration + 27 unit + 19 architecture; frontend 33 unit, typecheck/lint/build clean.
