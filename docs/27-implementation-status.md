# 27 — Progress & Implementation Status Tracking

> **Document ID:** SPEC-27
> **Status:** Live Implementation Register
> **Current State:** **Phase 1 CLOSED (2026-09-16).** Backend `dotnet build -warnaserror` -> 0 errors, 0 warnings across all 7 projects; `dotnet test` -> 23/23 passing (2 unit, 13 architecture, 8 integration). Frontend verified green (typecheck, lint, 3/3 tests, production build). All 5 architecture-rule mechanisms proven by temporary deliberate violation. CI installed at `.github/workflows/ci.yml`. **Phase 2 is next and remains blocked on PostgreSQL — see §4 for a material update to that blocker's context, raised to the project owner and not yet resolved.**
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
| **P2** | Database foundation, tenancy, initial migration | ⛔ **BLOCKED** | **PostgreSQL 16+ is not installed** (`docs/33 §4.3`). No substitute permitted (ADR-027). Composite tenant FKs must be in the initial migration. | — |
| **P3** | Authentication, sessions, CSRF, rate limiting | ⏳ Pending | Both OTP rate windows (ADR-026). | — |
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
| **1** | Architecture & Database (P1–P2) | 🔄 Half reached — P1 signed off, P2 blocked (§4) |
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

**All product decisions are closed.** The only outstanding blocker is environmental:

| Blocker | Blocks | Requirement |
| :--- | :--- | :--- |
| **PostgreSQL 16+ not installed *for this repository*** | **P2 onward** | Install PostgreSQL 16 or later, or get explicit direction on the item below. SQL Server, MySQL, and Oracle are present on the machine and are **explicitly rejected** (ADR-027). |

**Material update (2026-09-16, `docs/33 §4.3`/§7.4):** a PostgreSQL 16 process *is* running on `localhost:5432`, but it belongs to a separate, unrelated project directory (`D:\Invetory management`, not this repository) and was left running from a prior session on that other project. Its documented databases (`inventory_dev`/`inventory_test`, password `dev`) do not match this repository's expected database (`restaurant_inventory`, password `postgres`). **It has not been adopted or connected to.** This is raised to the project owner as an open question — not decided unilaterally — because connecting Phase 2 to an ambient database instance without confirming it is the intended one is exactly the kind of assumption ADR-027 exists to prevent. Until answered, PostgreSQL remains **not installed for this repository** and P2 remains blocked.

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
| **PostgreSQL** (for this repository) | **16+** | **NOT FOUND** — an unrelated project's instance answers on 5432; not adopted (`docs/33 §4.3`) | ❌ **BLOCKING P2** |

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
6. **Phase 1 marked complete; Checkpoint 1's Phase 1 half signed off.** Checkpoint 1's Phase 2 half remains open pending the PostgreSQL question in §4.
