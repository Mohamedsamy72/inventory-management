# 33 — Verified Development Environment Matrix

> **Document ID:** SPEC-33
> **Topic:** The actually-installed toolchain on the development machine, verified rather than assumed
> **Status:** Authoritative — Living Register
> **Verification date:** 2026-09-10 (Run 1, partial), 2026-09-16 (Run 2, complete)
> **Machine:** `koshary` — Windows (`win32`, `x64`)
> **Governs:** ADR-027, OD-009 (closed)

---

## 1. Purpose

OD-009 required that the target stack be **verified on the development machine**, not assumed. This document records what was actually found, how it was found, what is missing, and what remains unverified. It exists so that no later phase discovers a missing prerequisite at the moment it needs it.

## 2. Scope

The toolchain required to build, migrate, run, and test the product locally. Production infrastructure is `docs/20`.

## 3. Verification Method & Its Limits

**The device shell was unavailable during verification** — the isolated Linux workspace on the device failed to start, so `dotnet --version`, `node --version`, and `psql --version` could not be executed.

Versions were therefore established by **reading the filesystem directly**: SDK, runtime, NuGet-cache, and global-tool installations are laid out as version-named directories, which is authoritative for *what is installed*. This method has two limits, both recorded honestly below:

1. It cannot read a binary's own version resource, so **Node.js's exact version is inferred, not read**.
2. It cannot confirm **`PATH` membership** — a tool present on disk is not necessarily invocable as a bare command.

Both are closed by Phase 1 Task 1.1, which runs the version commands directly once a shell is available.

---

## 4. Verified Matrix

### 4.1 Backend — Satisfied

| Component | Required (OD-009) | Installed | Location | Status |
| :--- | :--- | :--- | :--- | :---: |
| **.NET SDK** | 10 | **10.0.302** — the only registered SDK (see §7.0.4) | `C:\Program Files\dotnet\sdk` | ✅ **EXECUTED** |
| **.NET Runtime** | 10 | **10.0.10** (also 8.0.29) | `C:\Program Files\dotnet\shared\Microsoft.NETCore.App` | ✅ **EXECUTED** |
| **ASP.NET Core Runtime** | 10 | **10.0.10** (also 8.0.29) | `C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App` | ✅ **EXECUTED** |
| **.NET Runtime** (detail) | — | `8.0.29` also present; `Microsoft.WindowsDesktop.App` `8.0.29` / `10.0.10` also present | — | ✅ **EXECUTED** |
| **EF Core** | 10 | **10.0.11** (also 10.0.7, 10.0.5, 10.0.4, 10.0.3) | NuGet cache | ✅ **VERIFIED** (disk) |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | EF Core 10-compatible | **10.0.3** | NuGet cache | ✅ **VERIFIED** (disk) |
| **`dotnet-ef` CLI** | matching EF Core | **10.0.3** | invoked as `dotnet ef` | ✅ **EXECUTED** |
| **Git** | any | **2.47.0.windows.1** | `C:\Program Files\Git` | ✅ **EXECUTED** (Run 2) |

The backend stack is **fully satisfied**. The Npgsql provider and `dotnet-ef` are already in the local NuGet cache, so Phase 1 and Phase 2 can proceed without a package restore over the network.

### 4.2 Frontend — Satisfied, One Value Inferred

| Component | Required | Installed | Location | Status |
| :--- | :--- | :--- | :--- | :---: |
| **Node.js** | ≥ 20.9 (Next.js 16 minimum) | **v22.20.0** | `D:\Nodejs` | ✅ **EXECUTED** — inference confirmed exactly |
| **npm** | bundled | **10.9.3** | `D:\Nodejs\node_modules\npm` | ✅ **EXECUTED** (Run 2) |

**Inference resolved (Run 1).** `node --version` returned **`v22.20.0`** — the 22.x inference from bundled npm 10.9.3 and the binary's 2025-09-23 date was exactly right. The value is now read, not deduced.

**Non-standard location — `PATH` fully settled (Run 2).** Node is installed at `D:\Nodejs`, not the default `C:\Program Files\nodejs`. Both `node` and `npm` are confirmed to resolve as bare commands (`where node` → `D:\Nodejs\node.exe`; `where npm` → `D:\Nodejs\npm`, `D:\Nodejs\npm.cmd`). No `PATH` edit was needed.

### 4.3 Database — **RESOLVED (2026-09-16)**

| Component | Required | Installed | Status |
| :--- | :--- | :--- | :---: |
| **PostgreSQL** | **16+** | **16.15**, portable, dedicated to this repository | ✅ **INSTALLED** |

**Installed at `C:\pg-inventory-system\`, not under a `.local_postgres\` folder inside this repository, and on port `5433`, not the PostgreSQL default `5432`.** Both deviations were forced by facts discovered during installation — see `docs/19 §1` for the full explanation and rationale:

1. This repository's root path (`D:\سيستم المخازن`) contains Arabic characters. PostgreSQL's Windows `initdb` fails to start from any non-ASCII path (its internal restricted-token re-exec mangles the path via an ANSI code page — reproduced directly: `invalid binary "D:\????? ???????\...": No such file or directory`). No subfolder of this repository is usable; the binaries and data directory were placed at the plain-ASCII `C:\pg-inventory-system\` instead.
2. Port 5432 was already held by an unrelated project's own PostgreSQL instance (see the paragraph below) — untouched, per explicit project-owner instruction. This repository's instance uses port `5433` instead, configured in `postgresql.conf` and reflected in `appsettings.Development.json`.

Database `restaurant_inventory` created; superuser password set to `postgres`, matching `appsettings.Development.json`. Confirmed reachable: `psql -h localhost -p 5433 -U postgres -d restaurant_inventory` connects; `\l` lists the database.

**Context — the unrelated sibling instance on port 5432 (Run 2 finding, still true, still not touched).** `netstat` shows a separate process bound to `127.0.0.1:5432` / `[::1]:5432`: `postgres.exe` at `D:\Invetory management\.local_postgres\pgsql\bin\postgres.exe`, started 2026-09-10, serving data directory `D:\Invetory management\.local_postgres\data` — a portable PostgreSQL instance belonging to a different, unrelated project directory, not this repository. Per explicit project-owner direction (2026-09-16): leave it alone, install a fresh instance dedicated to this repository instead. That is what §4.3 above now describes; the sibling instance was never connected to or modified.

**Other database engines are installed and are NOT substitutes.** The machine has Microsoft SQL Server (plus SSMS 22 and a `C:\SQL2025` directory), MySQL (standalone and inside a XAMPP installation at `D:\Installed apps`), and Oracle. **None of these may be used.** ADR-001 selected PostgreSQL deliberately, and the specification depends on PostgreSQL-specific behaviour that these engines do not provide:

- `xmin` as the optimistic-concurrency token (ADR-022, `docs/29` DB-01);
- `INSERT … ON CONFLICT … RETURNING` for gap-free sequence allocation (`docs/28 §5.3`);
- `jsonb` for audit `old_values`/`new_values` (`docs/06`);
- `PARTITION BY RANGE` for the 7-year audit retention (`docs/29 §4.5`);
- `pg_trgm` GIN indexing for Arabic substring search (`docs/29 §5.4`);
- `COLLATE "ar-x-icu"` for Arabic sorting (`docs/31 §4.8`);
- `gen_random_uuid()`, `NUMERIC(18,4)` semantics, and rule/trigger-based append-only enforcement.

**The architecture is not downgraded and no substitute is adopted.** PostgreSQL 16 or later must be installed before Phase 2. This is the single outstanding environment prerequisite.

### 4.4 Optional / Not Required

| Component | Note |
| :--- | :--- |
| Docker | **Installed as of Run 2 (2026-09-16), contradicting the 2026-09-10/11 "absent from disk" finding.** `docker --version` → `29.6.2` (build `dfc4efb`); CLI at `C:\Users\User\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe` (Docker Desktop). **The engine is not currently running** — `docker info` fails with `failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`, i.e. the CLI is present but Docker Desktop itself has not been started. Optional — Testcontainers-based integration tests (`docs/18 §1`) require the engine, not just the CLI, to be running. **Decided in Phase 2; not a Phase 1 blocker.** |
| Playwright browsers | Installed by `npx playwright install` during Phase T2. |

---

## 5. Requirements

| ID | Requirement |
| :--- | :--- |
| **ENV-1** | The backend targets .NET 10 / ASP.NET Core 10 / EF Core 10. No downgrade is permitted without a superseding ADR. |
| **ENV-2** | The database is PostgreSQL 16+. No other engine is permitted, whatever is already installed on the machine. |
| **ENV-3** | The frontend targets Next.js 16 / React 19 / TypeScript / Tailwind CSS on Node.js ≥ 20.9. |
| **ENV-4** | Phase 1 Task 1.1 re-verifies every row of §4 by executing the version commands, and updates this document with the executed output. |
| **ENV-5** | Phase 2 does not begin until §4.3 reports PostgreSQL 16+ installed. |
| **ENV-6** | Any version drift discovered later is recorded here and, if it changes an architectural choice, in a new ADR. |

## 6. Phase 1 Task 1.1 — Commands To Run

Once a shell is available, these confirm every inferred and unverified value:

```
dotnet --version
dotnet --list-sdks
dotnet --list-runtimes
dotnet ef --version
node --version
npm --version
psql --version
git --version
docker --version
```

Record the literal output in §7.

### 6.1 Runner Script

`verify-env.bat` at the repository root runs every command in §6 plus the `where` PATH-resolution checks, and writes the literal output to `env-verification.log` beside itself. It installs nothing, changes nothing, and touches no project file — the log is its only write.

```
D:\سيستم المخازن\verify-env.bat     ->  D:\سيستم المخازن\env-verification.log
```

The log is read back verbatim into §7. It is evidence, not a deliverable, and is regenerated on demand.

### 6.2 PATH Resolution Checks

```
where dotnet
where node
where npm
where psql
where git
where docker
```

A tool present on disk is not necessarily invocable as a bare command. `where` is what distinguishes "installed" from "usable", and Node's non-default location at `D:\Nodejs` makes this check material rather than ceremonial.

## 7. Executed Verification Log

**Status: ✅ FULLY EXECUTED — Run 1 (2026-09-11, partial) + Run 2 (2026-09-16, complete). Every §6 command and §6.2 `where` check now has literal, executed output. PostgreSQL remains the sole missing prerequisite; Docker's status changed from absent to present-but-not-running; one unplanned finding (an unrelated project's PostgreSQL instance occupying port 5432) is recorded in §4.3 and is explicitly not adopted.**

### 7.0 Run 1 — 2026-09-11 03:12 (`verify-env.bat` v1)

#### 7.0.1 Literal Output

```
===========================================================================
 PHASE 1 - TASK 1.1 : ENVIRONMENT VERIFICATION LOG
 Spec: docs/33-verified-environment-matrix.md section 6
===========================================================================
 Machine      : KOSHARY
 User         : User
 Timestamp    : Fri 09/11/2026  3:12:24.31
===========================================================================

---------------------------------------------------------------------------
$ dotnet --version
---------------------------------------------------------------------------
10.0.302

---------------------------------------------------------------------------
$ dotnet --list-sdks
---------------------------------------------------------------------------
10.0.302 [C:\Program Files\dotnet\sdk]

---------------------------------------------------------------------------
$ dotnet --list-runtimes
---------------------------------------------------------------------------
Microsoft.AspNetCore.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
Microsoft.AspNetCore.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
Microsoft.NETCore.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
Microsoft.NETCore.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
Microsoft.WindowsDesktop.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
Microsoft.WindowsDesktop.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

---------------------------------------------------------------------------
$ dotnet ef --version
---------------------------------------------------------------------------
Entity Framework Core .NET Command-line Tools
10.0.3

---------------------------------------------------------------------------
$ node --version
---------------------------------------------------------------------------
v22.20.0

---------------------------------------------------------------------------
$ npm --version
---------------------------------------------------------------------------
The system cannot find the batch label specified - RUN
The process cannot access the file because it is being used by another process.
   [ ... the same line repeats 52 times ... ]

Verification complete.
Log written to: D:\????? ???????\env-verification.log
```

#### 7.0.2 Why Run 1 Aborted — Defect in the Runner, Not the Machine

`npm` on Windows is `npm.cmd`, a **batch file**. A batch file invoked from another batch file **without `call`** transfers control permanently instead of returning. v1 invoked commands as `%CMD%` inside a `:RUN` subroutine, so control passed to `npm.cmd` and never came back — the subroutine context was destroyed (`The system cannot find the batch label specified - RUN`), the log handle was left contended, and every command after `node --version` was lost.

**Fix (v2):** the subroutine was removed entirely — the script is now linear with no labels, and `.cmd` shims (`npm`, `dotnet ef`) are invoked with `call`. Removing the label removes the failure mode rather than patching one instance of it.

This is a defect in a temporary verification helper. It touched no specification, no project file, and no business rule.

#### 7.0.3 Values Confirmed by Run 1

| Command | Literal result | Effect |
| :--- | :--- | :--- |
| `dotnet --version` | `10.0.302` | ✅ Confirms .NET 10 SDK and the `global.json` pin. |
| `dotnet --list-sdks` | `10.0.302` **only** | ⚠️ **Correction** — see §7.0.4. |
| `dotnet --list-runtimes` | ASP.NET Core `10.0.10`, .NET `10.0.10` (+ `8.0.29`, + WindowsDesktop) | ✅ Confirms both required runtimes. |
| `dotnet ef --version` | `10.0.3` | ✅ Confirms the CLI is **invocable**, not merely present on disk. |
| `node --version` | `v22.20.0` | ✅ Resolves the inference exactly, and proves `node` **is on `PATH`** — it ran as a bare command. |

#### 7.0.4 Correction to §4.1 — Only One SDK Is Registered

§4.1 reported "10.0.302 (also 10.0.102)" from the `C:\Program Files\dotnet\sdk` directory listing. `dotnet --list-sdks` reports **10.0.302 alone**.

A directory under `sdk\` is not by itself a registered SDK — a partially removed or superseded install can leave the folder behind. **The executed command is authoritative and the filesystem inference was wrong.** This is exactly the class of error §3 warned the method was subject to, which is why §6 exists.

**Consequence for `global.json`:** the pin remains correct and is now *better* founded — `10.0.302` is the only SDK present, so the pin documents reality rather than disambiguating a conflict. `rollForward: latestPatch` is retained so a servicing patch inside the `10.0.3xx` band is picked up without a file change. §8 edge case 1 is corrected accordingly.

### 7.1 Run 2 — Executed 2026-09-16

Run 2 executed. `verify-env.bat` v2 itself was not double-clicked (it ends in an interactive `pause`, unsuitable for the assisting session); every command it would have run was executed directly instead, and the literal output recorded in `env-verification.log` and §7.4 below. This supplies the values Run 1 lost: `npm --version`, `psql --version`, `git --version`, `docker --version`, and all six §6.2 `where` checks — plus one unplanned finding (§4.3) that none of those commands were designed to surface.

### 7.2 Earlier Blocker — Why a Script Was Needed At All

Two execution routes exist to this machine, and both are currently closed:

| Route | Result |
| :--- | :--- |
| Device shell (`device_bash`) | `Workspace unavailable — the isolated Linux environment on this device failed to start.` Persistent across every attempt on 2026-09-10 and 2026-09-11. |
| Computer use (terminal automation) | Terminals and IDEs resolve at **`click` tier only** — visible and clickable, but keystrokes and paste are refused. A command cannot be typed into them. |

Filesystem inspection — the method used for §4 — establishes *what is installed* because SDKs, runtimes, NuGet packages, and global tools are laid out as version-named directories. It cannot establish *what a shell resolves*, which is precisely what §6.2 exists to check.

**No value in §4 was invented, and no version was assumed forward.** Where an executed command has now contradicted a filesystem inference, the command wins and the inference is corrected in place — see §7.0.4.

### 7.3 Verification State After Run 1

| Item | State | Basis |
| :--- | :---: | :--- |
| .NET SDK version | ✅ **CONFIRMED** | `dotnet --version` → `10.0.302` |
| SDKs registered | ✅ **CONFIRMED** (corrected) | `dotnet --list-sdks` → `10.0.302` only |
| .NET / ASP.NET Core runtimes | ✅ **CONFIRMED** | `dotnet --list-runtimes` → both `10.0.10` |
| `dotnet ef` invocable | ✅ **CONFIRMED** | `dotnet ef --version` → `10.0.3` |
| Node.js exact version | ✅ **CONFIRMED** | `node --version` → `v22.20.0` |
| `node` on `PATH` | ✅ **CONFIRMED** | It ran as a bare command |
| `dotnet` on `PATH` | ✅ **CONFIRMED** | It ran as a bare command |
| npm version | ✅ **CONFIRMED** | `npm --version` → `10.9.3` |
| `npm` on `PATH` | ✅ **CONFIRMED** | `where npm` → `D:\Nodejs\npm(.cmd)` |
| Git | ✅ **CONFIRMED** | `git --version` → `2.47.0.windows.1` |
| Docker | ⚠️ **CLI PRESENT, ENGINE NOT RUNNING** (reverses "absent from disk") | `docker --version` → `29.6.2`; `docker info` fails to reach the engine |
| PostgreSQL (installed for this repo) | ❌ Missing *at the time of Run 2* — no `psql` on `PATH`, no engine under this repo's control | `where psql` fails; no PostgreSQL under any searched path. **Superseded later the same day: see §4.3 — PostgreSQL 16.15 installed at `C:\pg-inventory-system\`, port 5433, reachable via its own `psql.exe`.** |
| Port 5432 | ⚠️ **UNPLANNED — occupied by an unrelated project's instance** | See §4.3; not this repository's database, not assumed usable |

### 7.4 Run 2 Literal Output

See `env-verification.log` (repository root, regenerated on demand, gitignored per §9) for the full literal transcript. Summary of new values obtained:

```
$ npm --version          -> 10.9.3
$ psql --version         -> [not found; no psql on PATH or disk]
$ git --version          -> git version 2.47.0.windows.1
$ docker --version       -> Docker version 29.6.2, build dfc4efb

$ where npm    -> D:\Nodejs\npm
                  D:\Nodejs\npm.cmd
$ where psql   -> INFO: Could not find files for the given pattern(s).
$ where git    -> C:\Program Files\Git\mingw64\bin\git.exe
                  C:\Program Files\Git\cmd\git.exe
$ where docker -> C:\Users\User\AppData\Local\Programs\DockerDesktop\resources\bin\docker
                  C:\Users\User\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe
```

Unplanned addition, outside the §6 command list — run because CLI presence does not confirm the engine is usable:

```
$ docker info
...
Server:
failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine:
check if the path is correct and if the daemon is running: open
//./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.
```

And the port-5432 finding (§4.3), via `netstat -ano | findstr :5432` followed by a PID lookup:

```
TCP    127.0.0.1:5432   0.0.0.0:0   LISTENING   31848
TCP    [::1]:5432       [::]:0      LISTENING   31848

PID 31848 = D:\Invetory management\.local_postgres\pgsql\bin\postgres.exe
            -D "D:/Invetory management/.local_postgres/data" -p 5432
            (started 2026-09-10 11:35:18)
```

## 8. Edge Cases

1. **Exactly one SDK is registered: `10.0.302`** (corrected in §7.0.4 — a leftover `10.0.102` directory exists but `dotnet --list-sdks` does not report it). Task 1.1a pins `10.0.302` in `global.json` so CI and the developer machine resolve identically. `rollForward` is `latestPatch`: the build stays inside the `10.0.3xx` feature band — a servicing patch is picked up without a file change — while a feature-band change never silently alters compiler or SDK behaviour. `disable` was rejected as needlessly brittle.
2. **.NET 8 runtime also present.** Harmless; `TargetFramework` governs.
3. **Node outside the default location.** A `PATH` matter, not an install matter.
4. **MySQL/SQL Server present.** A convenience trap, not an option. ENV-2 is absolute.
5. **No Docker.** Affects only the integration-test host strategy, decided in Phase 2.

## 9. Security Implications

- This document records versions only. It contains **no** connection strings, credentials, or secrets, and must never be extended to hold any (`docs/20 §2.1`).
- Multiple runtime versions on one machine are a supply-chain surface. `dotnet list package --vulnerable` and `npm audit` run in Phase S1.

## 10. Dependencies

`docs/19-local-development-startup.md`, `docs/20-deployment-infrastructure.md`, `docs/09-implementation-plan.md` §8, ADR-001, ADR-022, ADR-027.

## 11. Acceptance Criteria

- `AC-33-1` §7 contains the literal executed output of every command in §6.
- `AC-33-2` `psql --version` reports 16 or later.
- `AC-33-3` `dotnet --version` reports the version pinned in `global.json`.
- `AC-33-4` `node --version` reports ≥ 20.9.
- `AC-33-5` No specification or project file references a database engine other than PostgreSQL.

## 12. Related Documents

`docs/decision-log.md` (ADR-001, ADR-022, ADR-027), `docs/open-decisions.md` (OD-009, closed), `docs/09-implementation-plan.md`, `docs/19`, `docs/20`, `docs/29`.
