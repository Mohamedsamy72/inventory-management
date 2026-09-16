# Restaurant Inventory Management System
## Master Engineering & Product Specification System

> **Document Status:** Authoritative Engineering Baseline — Clean Restart (v1.1, Reconciled)
> **Target Audience:** Engineering, Implementation Agents (Claude), System Architects, QA
> **Product Nature:** Arabic-First Enterprise Multi-Tenant Inventory & Supply Management Platform
> **Phase:** Specification reconciliation complete. **Implementation has not begun and must not begin without explicit approval.**

---

## 1. Documentation Structure & Map

All specifications are modularized under `docs/`. Each document is the single source of truth for its domain.

```
docs/
├── README.md                              # This file — system map, authority hierarchy, tenets
├── glossary.md                            # Domain terminology (Arabic/English mapping & invariants)
├── decision-log.md                        # Architectural Decision Records (ADR-001 … ADR-026)
├── open-decisions.md                      # Configurable items + decisions REQUIRED before named phases
├── system-master-flow.md                  # Non-normative topology & lifecycle overview
│
├── 01-product-overview.md                 # Product vision, multi-tenancy, core topology
├── 02-business-requirements.md            # Inviolable business invariants
├── 03-roles-permissions-data-scope.md     # 5 roles, permission matrix, role denial, scopes
├── 04-end-to-end-business-flow.md         # ★ MASTER business-flow authority (SPEC-FLOW-04)
├── 04-inventory-stock-model.md            # ★ Stock ledger, balances, WAC, concurrency (SPEC-04)
├── 05-business-workflows.md               # Subordinate state-machine summary
├── 06-database-schema.md                  # PostgreSQL table inventory
├── 07-api-specification.md                # REST contracts, DTOs, RFC 7807 errors
├── 08-authentication-session-security.md  # Mobile+password, OTP, cookie sessions, CSRF
├── 09-authorization-security.md           # Policy engine, tenant filters, threat mitigation
├── 10-frontend-architecture.md            # Next.js App Router, TypeScript, RTL, state
├── 11-ui-ux-design-system.md              # Swiss-minimal RTL design system
├── frontend-ui-ux-implementation-guide.md # Mandatory frontend implementation guide
├── 12-navigation-role-based-screens.md    # Role navigation, dashboards, zero fake data
├── 13-validation-error-handling.md        # Server validation & Arabic error catalog
├── 14-audit-activity.md                   # Transactional audit logging, Owner activity monitor
├── 15-costing-financial-data.md           # Weighted average costing & financial lockdown
├── 16-files-evidence.md                   # 🚫 DEFERRED (ADR-029) — file storage abstraction
├── 17-reporting.md                        # 🚫 DEFERRED (ADR-029) — reporting + binding extension points
├── 18-testing-strategy.md                 # Unit, integration, security, concurrency, E2E
├── 19-local-development-startup.md        # Local Postgres setup and scripts
├── 20-deployment-infrastructure.md        # Production architecture & hardening
├── 21-non-functional-requirements.md      # Performance, concurrency, SLA, observability
├── 22-implementation-plan.md              # ⇢ superseded pointer → 09-implementation-plan.md
├── 23-acceptance-criteria.md              # Formal acceptance tests & numerical scenarios
├── 24-data-migration-clean-start-rules.md # Clean restart rules & obsolete model removal
├── 25-developer-rules-anti-patterns.md    # Strict anti-patterns & prohibited architectures
├── 26-traceability-matrix.md              # ★ End-to-end requirement-to-code matrix
├── 27-implementation-status.md            # ★ Live progress register & phase gates
│
│   ── Added by the reconciliation pass ──
├── 28-document-numbering-and-sequences.md # Server-generated identifiers & gap-free sequences
├── 29-database-integrity-indexes-constraints.md # Schema corrections, composite FKs, full index plan
├── 30-transactions-concurrency-idempotency.md   # Transaction boundaries, locking, idempotency contract
├── 31-arabic-rtl-localization-spec.md     # Arabic-only content & RTL specification
├── 32-specification-conflict-register.md  # ★ Classified register of every conflict and gap
├── 33-verified-environment-matrix.md      # ★ Actually-installed toolchain, verified not assumed
├── 09-implementation-plan.md              # ★ AUTHORITATIVE dependency-aware implementation plan
│
│   ── Alias stubs (no specification content — see OD-007) ──
├── 08-frontend-ui-ux-implementation-guide.md
├── traceability-matrix.md
└── implementation-status.md
```

> **Numbering note.** The re-initialisation brief names `docs/08-frontend-ui-ux-implementation-guide.md` and `docs/09-implementation-plan.md`. Number `08` is already the authentication spec, so the frontend guide is reachable through an alias stub while its content stays at `frontend-ui-ux-implementation-guide.md`. Number `09` is the authorization spec, but the implementation plan is a genuinely new document and takes `09-implementation-plan.md` as its **real** path; `22-implementation-plan.md` is demoted to a pointer. Two documents share the number `04` and are disambiguated by their distinct Document IDs (`SPEC-04`, `SPEC-FLOW-04`). See **OD-007**.

---

## 2. Documentation Authority Hierarchy

When any ambiguity, discrepancy, or legacy artifact is encountered, apply this order strictly. A lower tier **never** overrides a higher one.

| Tier | Authority | Documents |
| :---: | :--- | :--- |
| **1** | Final business decisions | `decision-log.md` (ADR-001 … ADR-026) |
| **2** | Product requirements & invariants | `01-product-overview.md`, `02-business-requirements.md` |
| **3** | End-to-end business flow | `04-end-to-end-business-flow.md` ★ |
| **4** | Architecture: inventory, roles, scope | `04-inventory-stock-model.md`, `03-roles-permissions-data-scope.md` |
| **5** | Database specification | `06-database-schema.md`, amended and governed by `29-database-integrity-indexes-constraints.md` |
| **6** | API specification | `07-api-specification.md`, `28`, `30` |
| **7** | Security & authorization | `08-authentication-session-security.md`, `09-authorization-security.md`, `14`, `15`, `16` |
| **8** | Frontend UI/UX | `frontend-ui-ux-implementation-guide.md`, `10`, `11`, `12`, `31` |
| **9** | Testing specification | `18-testing-strategy.md`, `23-acceptance-criteria.md` |
| **10** | Implementation plan | `09-implementation-plan.md` ★ |
| **11** | Implementation status | `27-implementation-status.md`, `33-verified-environment-matrix.md` |
| **12** | Pre-existing code | Untrusted. **Never** overrides a written specification. |

### 2.1 Resolution Rules

1. **Conflicts are never silently merged.** Every discovered conflict is entered in `32-specification-conflict-register.md`, classified, and resolved by an ADR or escalated to `open-decisions.md`.
2. **`29` governs `06`.** Where the two differ on a constraint, index, or column, `29` wins — it exists to correct defects in `06`.
3. **`04-end-to-end-business-flow.md` governs `05` and `system-master-flow.md`.** `05` is a subordinate state-machine summary; `system-master-flow.md` is a non-normative overview. Neither may introduce a rule the master flow does not state.
4. **Business rules are never invented.** If an answer cannot be established from the documents, it is recorded in `open-decisions.md`, not guessed.
5. **No implementation file may override a business rule.** A code change that requires a rule change requires a documentation change first.

---

## 3. Core Architectural Tenets

- **Warehouse-Only Inventory.** Warehouses own 100% of physical inventory. Restaurants have **no** stock balances, on-hand balances, in-transit balances, or valuations — ever.
- **Controlled Stock Ledger.** Direct stock mutation is forbidden. Every balance change originates from an append-only `StockLedger` row produced by a formal business event.
- **Dispatch Does Not Deduct.** Dispatching changes workflow state only. Stock is deducted **only** when the Restaurant Supervisor confirms actual received quantities.
- **In-Transit Is Derived, Never Stored (ADR-018).** Goods on the road are shown as a computed warehouse-side figure so physical counts stay correct — never as a persisted balance and never attributed to a restaurant.
- **Server Authority.** Client input — calculations, conversion factors, actor IDs, company IDs, item codes, document numbers — is never trusted. All identifiers and all business math are server-produced.
- **Financial & Audit Lockdown (ADR-012, ADR-013).** Costs, valuations, and audit logs are visible **exclusively** to the `Owner`. `costs:view`, `valuation:view`, `audit:view`, `audit:export` are **non-grantable**. Denial is enforced in SQL projections, not in the UI.
- **One Role Per User (ADR-014).** Exactly one of the five product roles per user, so role denial always has a single unambiguous subject.
- **Tenant Isolation Is Structural (ADR-016).** Composite `(company_id, id)` foreign keys make cross-tenant references physically impossible, not merely filtered.
- **Correctness Over Convenience.** Negative stock is never permitted (ADR-021); the system never invents a number to make an operation succeed.
- **Serving Warehouse Is Derived, Never Chosen (ADR-028).** Each restaurant has exactly one default serving warehouse. The supply-request DTO declares no `warehouseId`, so there is no client value to trust and nothing to validate.
- **Deferred Means Absent, Not Faked (ADR-029).** Files and Reporting are out of MVP scope. A deferred feature is shown as nothing at all — never a placeholder chart, mock KPI, or "coming soon" screen. Core data nonetheless stays fully reportable.
- **No Silent Downgrade (ADR-027).** PostgreSQL 16+ is mandatory. Other engines installed on the development machine are explicitly rejected.
- **Arabic-First Enterprise UX.** RTL-native, Swiss-minimalist, `Noto Sans Arabic`, zero placeholder data, role-tailored screens. Full specification in `31`.

---

## 4. Current Project State

| Item | State |
| :--- | :--- |
| Source tree | **Does not exist.** `docs/` and the `ui-ux-pro-max` skill are the entire repository. |
| Specification reconciliation | **Complete.** See `32-specification-conflict-register.md`. |
| Implementation plan | **Complete.** See `09-implementation-plan.md`. |
| Product decisions | **All closed.** OD-008, OD-009, OD-010, OD-011 resolved by ADR-027 … ADR-030. OD-007 open and non-blocking. |
| Environment | **Verified** — see `33-verified-environment-matrix.md`. Backend toolchain complete; **PostgreSQL 16+ is NOT installed**. |
| Phase 1 | **Ready to start** on approval (creates no schema). |
| Phase 2 onward | **Blocked** until PostgreSQL 16+ is installed. No substitute engine is permitted. |
| Deferred scope | File storage and Reporting, by ADR-029. Reportability of core data is preserved. |
| Implementation | **Not started. Awaiting explicit approval.** |
