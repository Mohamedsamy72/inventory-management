# Open & Configurable Engineering Decisions

> **Purpose:** Explicit record of items that are intentionally configurable, or that genuinely require a decision from the project owner before the affected implementation phase begins.
> **Rule:** Core business invariants (warehouse-only inventory, Admin financial denial, five roles, dispatch does not deduct, confirmation deducts, stock ledger authority, server-generated codes) are **CLOSED** and must never be reopened here.
> **Status:** Authoritative — Living Register

---

## 0. How To Read This Document

| Marker | Meaning |
| :--- | :--- |
| **CONFIGURABLE** | A baseline exists and implementation may proceed. The choice is an environment or formatting matter. |
| **REQUIRES DECISION** | Implementation of the named phase **must not begin** until the owner decides. An interim baseline is recorded only so that planning can continue. |

---

## 1. Environment & Infrastructure Choices

| Item ID | Status | Topic | Current Baseline / Recommendation | Production Options | Decision Trigger |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **OD-001** | CONFIGURABLE | **SMS Gateway Provider** | Mock / Development SMS provider logging OTP to console and structured test logs. | Twilio, Unifonic, Vodafone SMS Gateway, AWS SNS. | Selection of regional telecom provider during production staging. |
| **OD-002** | CONFIGURABLE | **File Storage Backend** | Local physical disk storage abstraction under `/storage/tenants/{companyId}/`. | AWS S3, Azure Blob Storage, MinIO. | Production cloud infrastructure deployment. |
| **OD-003** | CONFIGURABLE | **Hosting Environment** | Self-hosted Kestrel + Node.js, Docker Compose. | Azure App Services + Azure Database for PostgreSQL, or managed VPS. | Target production budget and SLA. |

---

## 2. Configurable Business Formatting

| Item ID | Status | Topic | Current Standard Baseline | Notes / Constraints |
| :--- | :---: | :--- | :--- | :--- |
| **OD-004** | CONFIGURABLE | **Item Code Visual Format** | `ITM-{000000}` (`ITM-000001` … `ITM-999999`). | Zero-padded, unique per company. The format carries no barcode semantics; ADR-007 removed barcodes from the product. |
| **OD-005** | CONFIGURABLE | **Document Number Prefixes** | Receiving `REC-YYYYMM-{0000}`<br>Supply Request `REQ-YYYYMM-{0000}`<br>Supply / Dispatch `SUP-YYYYMM-{0000}`<br>Stock Count `CNT-YYYYMM-{0000}`<br>Discrepancy `DSC-YYYYMM-{0000}` | Gap-free and monotonic per company per period. Period boundaries follow the tenant timezone (ADR-024). Full specification: `docs/28`. |
| **OD-006** | CONFIGURABLE | **Default Currency** | Egyptian Pound (`EGP` / `ج.م`). | Tenant setting. Internal math `NUMERIC(18,4)`; display `18,2`. |

---

## 3. Decision Register

### 3.1 Closed Decisions

| ID | Topic | Resolution | Closed By | Date |
| :--- | :--- | :--- | :--- | :--- |
| **OD-008** | Serving warehouse selection | **CLOSED.** One default Serving Warehouse per restaurant, derived server-side. The supervisor never selects it. | ADR-028 | 2026-09-10 |
| **OD-009** | Framework version targets | **CLOSED.** Stack confirmed and not negotiable downward; installed toolchain verified. **PostgreSQL 16+ is not installed and must be installed before Phase 2.** | ADR-027, `docs/33` | 2026-09-10 |
| **OD-010** | Unretrievable ChatGPT transcript | **CLOSED.** Treated as an unavailable historical source; no requirement inferred from it. | ADR-030 | 2026-09-10 |
| **OD-011** | Files and Reports scope | **CLOSED.** Both deferred out of MVP. Files only if an approved workflow requires an attachment; Reports deferred as implementation while reportability of core data is preserved. No fake reports, no placeholder KPIs. | ADR-029 | 2026-09-10 |

#### OD-008 — Serving Warehouse *(closed)*
Each `Restaurant` has exactly one `default_serving_warehouse_id` (`NOT NULL`). The Restaurant Supervisor does **not** choose a warehouse; the backend derives it. The creation DTO does not declare `warehouseId`, so no client value exists to trust. The derived warehouse must be in the same company (structural, ADR-016) and `Active`; otherwise `409 SERVING_WAREHOUSE_UNAVAILABLE` with no fallback. Warehouse Staff scope enforcement is unchanged. A many-to-many serving mapping is a future version increment, not a patch. Full record: **ADR-028**.

#### OD-009 — Technology Baseline *(closed)*
Target stack confirmed: .NET 10, ASP.NET Core 10, EF Core 10, EF Core 10-compatible Npgsql provider, Next.js 16, React 19, TypeScript, Tailwind CSS, PostgreSQL 16+.

Verified on `koshary` (see **`docs/33-verified-environment-matrix.md`**):
- ✅ .NET SDK 10.0.302, .NET Runtime 10.0.10, ASP.NET Core Runtime 10.0.10, EF Core 10.0.11, Npgsql provider 10.0.3, `dotnet-ef` 10.0.3, Git.
- ⚠️ Node present at `D:\Nodejs` with npm 10.9.3; Node major **inferred as 22.x**, confirmed by Phase 1 Task 1.1.
- ❌ **PostgreSQL NOT INSTALLED.** SQL Server, MySQL, and Oracle are present and are **explicitly rejected as substitutes** — the specification depends on `xmin`, `ON CONFLICT … RETURNING`, `jsonb`, range partitioning, `pg_trgm`, and `ar-x-icu`. **The architecture is not downgraded.**

**Consequence:** Phase 1 is unblocked (it creates no schema). **Phase 2 is blocked until PostgreSQL 16+ is installed.** Full record: **ADR-027**.

#### OD-010 — Unavailable ChatGPT Transcript *(closed)*
The share link is a client-rendered page returning no server content. Treated as an unavailable historical source: no requirement inferred, none invented. If the transcript is later supplied as text or a file, it becomes new source material and triggers a fresh reconciliation pass. Full record: **ADR-030**.

#### OD-011 — Files and Reports *(closed)*
**Files deferred.** No generic file-management subsystem. Storage enters scope only when an already-approved workflow explicitly requires an attachment; none currently does. `docs/16` marked DEFERRED and preserved.
**Reports deferred as implementation; reportability preserved.** No report endpoints, no exports, no `/reports` route, and **no placeholder charts or fake KPIs**. Core data must remain reportable: full ledger metadata, full audit context, line-level document links, and the `docs/29 §5` indexes. Extension points documented in `docs/17`. Full record: **ADR-029**.

### 3.2 Open Decisions

#### OD-012 — Assertion Library Licensing
**Status:** REQUIRES DECISION — due before **Phase 7** (first domain tests). Does not block Phases 1–6.
**Context:** `docs/18 §1` mandates FluentAssertions. From **version 8 it is commercially licensed** (Xceed) — free for open-source and non-commercial use only. This product is a commercial enterprise system, so v8+ would require a paid licence per developer. Version **8.10.0** is already in the development machine's NuGet cache, so it would be adopted by default if nobody looked.
**Options:**
- *Pin FluentAssertions 7.x* — the last Apache-2.0 release. Familiar API, no licence cost, no code change. Receives no new features and eventually no security fixes.
- *Buy v8+ licences* — current library, supported, costs money per seat and needs procurement.
- *Switch to Shouldly* (BSD) or **xUnit's built-in assertions** — no licence question at all; Shouldly's API is close enough that the migration is mechanical.
**Affected:** `docs/18`, all `.NET` test projects from Phase 7 onward.
**Risk:** Low if decided before Phase 7; rising afterwards, because every domain test written against one API is a test to rewrite.
**Interim handling (no commitment made):** Phase 1 uses **xUnit's built-in assertions only**. The architecture tests need nothing more, so no licence is consumed and no API is pre-committed.
**Recommendation:** FluentAssertions 7.x if the team values the familiar API; xUnit assertions alone if they would rather carry no third-party assertion dependency in an enterprise codebase. Either is defensible; the one thing to avoid is drifting into v8 by default.

#### OD-007 — Documentation Numbering Reconciliation
**Status:** OPEN — non-blocking. Implementation may proceed.
**Context:** The re-initialisation brief names `docs/08-frontend-ui-ux-implementation-guide.md` and `docs/09-implementation-plan.md`. `08` is the authentication spec and `09` the authorization spec; `04` is held by two documents distinguished by their Document IDs.
**Both sides:** renaming satisfies the external convention exactly but breaks every cross-reference in `README`, `26`, and the ADRs; preserving keeps all references valid but leaves two documents at paths the brief did not name.
**Current handling:** existing numbers preserved; alias stubs published at the requested paths carrying no specification content; `09-implementation-plan.md` is a genuinely new document and holds that path for real, with `22` demoted to a pointer.
**Recommendation:** leave as is. Revisit only if a single clean renumber is wanted before implementation starts.

## 4. Explicitly Deferred Capabilities (Future Roadmap)

Excluded from the current core scope; must not be implemented until a formal version increment:

1. **Recipe Management & Bill of Materials (BOM)** — automated ingredient deduction from POS sales.
2. **FIFO / LIFO Costing** — only Weighted Average Costing is supported in v1.0.
3. **SignalR / Real-Time Push Notifications** — current scope uses polling and action-oriented dashboard loaders.
4. **Offline PWA Sync** — the web application requires online connectivity.
5. **Customer Loyalty / POS / HR / Payroll** — out of domain scope.
6. **Stock Reservation at Dispatch** — deliberately excluded by ADR-019; dispatch advises, never reserves.
7. **Multi-Currency** — single tenant currency only (OD-006).
