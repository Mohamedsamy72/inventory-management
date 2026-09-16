# 27 — Progress & Implementation Status Tracking

> **Document ID:** SPEC-27
> **Status:** Live Implementation Register
> **Current State:** Phase 1 authored. Frontend **verified green**; backend **awaiting device-side build verification**.
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
| **P1** | Solution architecture & infrastructure | 🔄 **AUTHORED — AWAITING VERIFICATION** | All 17 tasks authored. Frontend verified green (typecheck, lint, 3 tests, production build). **Backend build and tests not yet executed** — no .NET toolchain is reachable from the assisting session (`docs/33 §7.2`), so `build-verify.bat` must be run on the machine. CI workflow delivered but not installed (protected path). | Not signed off |
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
| **1** | Architecture & Database (P1–P2) | ⏳ Not reached |
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
| **PostgreSQL 16+ not installed** | **P2 onward** | Install PostgreSQL 16 or later. SQL Server, MySQL, and Oracle are present on the machine and are **explicitly rejected** (ADR-027). |

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
| **1.1** | Execute `docs/33 §6` verification; record literal output in `docs/33 §7` | 🔄 **PARTIAL** | Run 1 confirmed SDK `10.0.302`, runtimes `10.0.10`, `dotnet ef` `10.0.3`, Node `v22.20.0`, and that `dotnet`/`node` resolve on `PATH`. Run 1 aborted on a defect in the runner script (`docs/33 §7.0.2`); **v2 delivered, Run 2 pending** for npm, psql, git, docker and the `where` checks. |
| **1.1a** | Pin SDK in `global.json` | ✅ **DONE** | `10.0.302`, `rollForward: latestPatch`. Confirmed by executed command; the "two SDKs" inference was **corrected** — only one is registered (`docs/33 §7.0.4`). |
| **1.1b** | `PATH` resolution | 🔄 **PARTIAL** | `dotnet` and `node` **confirmed** on `PATH` — both ran as bare commands. `npm`, `git`, `psql`, `docker` pending Run 2. **No `PATH` change made**: none is needed for what is confirmed, and an edit without evidence would be a guess. |
| **1.1c** | Container runtime check | ⚠️ **PARTIAL** | Docker **absent from disk** (both Program Files trees). `docker --version` in Run 2 confirms. Not a Phase 1 blocker; it decides the Phase 2 integration-test host. |
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
| **1.17** | CI pipeline | ⚠️ **DELIVERED, NOT INSTALLED** | `.github/workflows/` is a protected path that remote tooling may not write. Content delivered as `ci-workflow.yml` at the repository root with move instructions. |

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
| Node.js | ≥ 20.9 | present at `D:\Nodejs`; **22.x inferred** from npm 10.9.3 | ⚠️ |
| npm | bundled | 10.9.3 | ✅ |
| Git | any | present | ✅ |
| **PostgreSQL** | **16+** | **NOT FOUND** | ❌ **BLOCKING P2** |

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

**Phase 1 is NOT signed off.** Files exist; that is not the same thing as a phase being complete (`docs/09 §7`, item 17).

### 9.1 Verified — executed, output observed

| Check | Command | Result |
| :--- | :--- | :---: |
| Frontend typecheck | `npm run typecheck` | ✅ clean |
| Frontend lint | `npm run lint` | ✅ clean (after scoping the RTL rule — it was matching its own message strings) |
| Frontend unit tests | `npm run test` | ✅ 3/3 passed |
| Frontend production build | `npm run build` | ✅ compiled in 6.8s, 3 static pages |
| Toolchain (partial) | `verify-env.bat` Run 1 | ✅ SDK, runtimes, `dotnet ef`, Node |

These ran in the assisting session's Linux workspace on Node 22.22.2 / npm 10.9.7. The device has Node **v22.20.0** — same major, and `package-lock.json` is committed, so the dependency graph is identical. Re-running on the device confirms it.

### 9.2 Not Verified — authored but never executed

| Check | Why not |
| :--- | :--- |
| `dotnet restore` | **No .NET toolchain is reachable.** The device has a working SDK but the session cannot execute there; the session's own workspace has no `dotnet`, and every Microsoft download host (`dot.net`, `aka.ms`, `packages.microsoft.com`, `api.nuget.org`) is blocked by egress policy — each was probed. |
| `dotnet build -warnaserror` | Same. |
| `dotnet test` (all three projects) | Same. |
| Architecture tests proven by deliberate violation | Requires a passing build first. `docs/09` Phase 1 risk note requires this; it is **outstanding**. |
| `run-local.bat` / `check-local.bat` / `stop-local.bat` | Windows batch; never executed. |
| Playwright E2E | Requires a running frontend; scheduled for Phase T2. |

### 9.3 Package Restore Risk

Every .NET package version was pinned against the device's **actual NuGet cache**, so these restore offline: `Microsoft.AspNetCore.OpenApi` 10.0.11, `Npgsql` 10.0.3, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `NetArchTest.Rules` 1.3.2, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11.

**One package is not cached: `Serilog.AspNetCore` 9.0.0.** It needs a fetch from nuget.org on first restore. If the machine is offline, or if that version does not resolve, this is the check expected to fail first.

### 9.4 To Close Phase 1

1. Run `verify-env.bat` (Run 2) — completes Task 1.1.
2. Run `build-verify.bat` — exercises §9.2.
3. Fix whatever the log reports; repeat.
4. Move `ci-workflow.yml` → `.github/workflows/ci.yml`.
5. Prove each architecture test with a temporary deliberate violation.
6. Only then mark Phase 1 complete and sign off Checkpoint 1's Phase 1 half.
