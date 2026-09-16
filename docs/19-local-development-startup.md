# 19 — Local Development & Startup Scripts

> **Document ID:** SPEC-19  
> **Topic:** Local Environment Setup, Portable PostgreSQL 16, and Diagnostic Scripts

---

## 1. Local Technology Stack & Ports

| Component | Technology | Default Port | Base URL / Connection |
| :--- | :--- | :--- | :--- |
| **Database** | PostgreSQL 16.15 (Portable) | `5433` | `Host=localhost;Port=5433;Database=restaurant_inventory;Username=postgres;Password=postgres` |

> ✅ **PREREQUISITE MET (installed 2026-09-16 — `docs/33 §4.3`).** PostgreSQL 16.15 portable binaries are installed and running, dedicated to this repository.
>
> **Two deliberate deviations from the original plan, both forced by facts discovered during installation, not by preference:**
>
> 1. **Port `5433`, not the PostgreSQL default `5432`.** Port 5432 on this machine is already held by an unrelated project (`D:\Invetory management`), left running and explicitly untouched per project-owner instruction. This repository's instance is fully separate: its own binaries, its own data directory, its own port, its own `restaurant_inventory` database.
> 2. **Binaries and data live at `C:\pg-inventory-system\`, outside this repository, not under a `.local_postgres\` folder inside it.** This repository's own root path (`D:\سيستم المخازن`) contains Arabic characters. PostgreSQL's Windows binaries fail to start from any path containing non-ASCII characters — `initdb`'s internal restricted-token re-exec step mangles the path via an ANSI code page before it reaches `CreateProcess`, producing `invalid binary "D:\????? ???????\...": No such file or directory` — verified by direct reproduction on this machine on 2026-09-16, independent of whether the shell was elevated. Every subfolder of this repo inherits the same non-ASCII root, so no location *inside* the repository is usable. `C:\pg-inventory-system\` was chosen as a plain-ASCII, dedicated, persistent location outside any temp/scratch directory. The `.gitignore` entry for `.local_postgres/` (docs/19 §2.1's anticipated name) is retained for any contributor whose own checkout path happens to be ASCII-only.
>
> SQL Server, MySQL, and Oracle remain present on this machine and remain **explicitly rejected as substitutes** (ADR-027): the specification depends on `xmin` concurrency, `ON CONFLICT … RETURNING`, `jsonb`, range partitioning, `pg_trgm`, and `ar-x-icu` collation, none of which transfers.
>
> **Starting/stopping this instance:**
> ```
> C:\pg-inventory-system\pgsql\bin\pg_ctl.exe -D C:\pg-inventory-system\data -l C:\pg-inventory-system\logfile.log start
> C:\pg-inventory-system\pgsql\bin\pg_ctl.exe -D C:\pg-inventory-system\data stop
> ```
| **Backend API** | ASP.NET Core (.NET 10) | `5165` | `http://localhost:5165` |
| **Frontend Web**| Next.js 16 (Node.js 20+) | `3000` | `http://localhost:3000` |

---

## 2. Startup Scripts Specification

### 2.1 `run-local.bat` (Master Local Launcher)
The script must perform the following sequenced operations:
1. Detect prerequisites (`dotnet 10`, `node 20+`, `psql` / portable postgres binary).
2. Check if PostgreSQL is active on port `5433` (this repo's dedicated port — see §1); if not, start it in background from `C:\pg-inventory-system\`.
3. Poll PostgreSQL readiness probe until the database accepts connections.
   - **If PostgreSQL is not installed or cannot be started, the script FAILS LOUDLY** with an explicit operator message naming the missing prerequisite. It must never continue silently, never skip migrations, and never fall back to another database engine (ADR-027, CR-093).
4. Execute EF Core database migrations: `dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api`.
5. Launch ASP.NET Core backend in background (`dotnet run --project src/Inventory.Api`).
6. Poll `/health` endpoint until HTTP 200 is returned.
7. Launch Next.js frontend in background (`npm run dev --prefix frontend`).
8. Poll `http://localhost:3000` until available.
9. Open default web browser to `http://localhost:3000`.

> **CRITICAL DATABASE SAFETY INVARIANT:**  
> `run-local.bat` must **NEVER** run `DROP DATABASE`, delete PostgreSQL data directories, or execute destructive schema resets.

---

### 2.2 `check-local.bat` (Layered Diagnostic Tool)
Reports discrete status per layer rather than a generic failure:
- `[PostgreSQL]`: Listening on 5433? Database exists?
- `[Backend API]`: Process running? `/health` responding?
- `[Frontend]`: Node process active? Port 3000 responding?
- `[Migrations]`: All pending EF Core migrations applied?

---

### 2.3 `stop-local.bat` (Graceful Shutdown Tool)
Stops only the processes launched by the development launcher without terminating unrelated system processes.

---

## 3. Reconciliation Amendment

**Verified toolchain.** `docs/33-verified-environment-matrix.md` records what is actually installed on the development machine. Summary: the .NET 10 backend toolchain is complete (SDK 10.0.302, runtimes 10.0.10, EF Core 10.0.11, Npgsql provider 10.0.3, `dotnet-ef` 10.0.3); Node is present at the non-standard location `D:\Nodejs` with npm 10.9.3; **PostgreSQL 16.15 is installed and running** (2026-09-16), at the non-standard, ASCII-path location `C:\pg-inventory-system\` on port `5433` — see §1 above for why both deviate from the original plan.

**`global.json`.** Two .NET 10 SDKs are installed (10.0.102 and 10.0.302). Phase 1 pins 10.0.302 in `global.json` so that the developer machine and CI resolve the same SDK.

**Node on `PATH`.** Node lives outside the default install location. Phase 1 Task 1.1 confirms `node` and `npm` resolve as bare commands; if they do not, the fix is a `PATH` entry, not a reinstall.

**Command set.** The canonical verification commands for every phase are `docs/09-implementation-plan.md` §8. No phase invents its own.
