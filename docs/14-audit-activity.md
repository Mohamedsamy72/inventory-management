# 14 — Audit Logging & Activity Monitoring

> **Document ID:** SPEC-14  
> **Topic:** Append-Only Audit Logging, Transactional Integrity, Sanitization, and Owner Activity Viewer

---

## 1. Audit Logging Architecture & Principles

The audit logging system provides a legally sound, immutable trail of all security-sensitive and operational events within each company tenant.

```
[ Application Command / Domain Event ]
                  │
                  ▼
   ┌──────────────────────────────┐
   │ EF Core DbContext SaveChanges │ ◄── Includes Business Changes + Audit Entry
   └──────────────┬───────────────┘
                  │ (Single Database Transaction)
                  ▼
       ┌──────────────────────┐
       │ Commit to PostgreSQL │
       └──────────────────────┘
```

### Key Invariants:
1. **Transactional Consistency:** Audit log entries are written inside the **same database transaction** as the underlying business change. If the business operation rolls back, no phantom success audit entry is written.
2. **Strict Sanitization (Zero Secret Leakage):**
   - Passwords, OTP codes, session tokens, and connection strings are **strictly excluded** from `old_values` and `new_values` JSON payloads.
3. **Owner-Only Visibility:** Audit logs (`/api/v1/audit`) are strictly reserved for the `Owner` role. Any `Admin` or non-owner request receives HTTP 403 Forbidden.

---

## 2. Audited System Events

Every significant business mutation triggers an audit record:
- **Authentication:** Successful logins, failed login attempts, logouts, password resets, mobile changes.
- **User Administration:** User creation, role modifications, warehouse/restaurant scope changes, permission overrides.
- **Master Data:** Item creation, unit conversion updates, supplier deactivations.
- **Receiving:** Receiving order posting (`INCOMING_POSTED`), physical reconciliation, order reversals.
- **Supply Chain:** Supply dispatch, receipt confirmation, discrepancy creation and resolution.
- **Inventory Adjustments:** Physical stock count initialization, line recordings, approval and ledger posting.

---

## 3. Owner Activity Monitor vs. Audit Log

| Feature | Audit Log (`سجل التدقيق`) | Activity Monitor (`مراقب النشاط المباشر`) |
| :--- | :--- | :--- |
| **Purpose** | Compliance, security investigation, and legal audit. | Real-time operational pulse and recent activities. |
| **Format** | Detailed JSON diff (`old_values`, `new_values`, IP, Agent). | Human-readable Arabic activity stream. |
| **Audience** | Owner Only. | **Owner Only — no grant path** (corrected: ADR-013 / CR-012). |
| **Storage** | Permanent append-only PostgreSQL table. | Derived read-model view from recent audit entries. |

---

## 4. Reconciliation Amendment

**CR-012 — corrected.** The §3 table previously described the Activity Monitor audience as *"Owner Only (Admin restricted **unless granted**)"*. That grant path directly contradicted the inviolable rule that `Admin` has no audit visibility, and is removed.

**ADR-013.** The Activity Monitor is a derived read-model over `audit_logs` and inherits the audit log's access rule exactly: `Owner` only, enforced by the same authorization policy, `403` for every other role — including a user explicitly granted `audit:view`, because `audit:view` and `audit:export` are **non-grantable** codes (ADR-012).

**Phase placement.** Audit *infrastructure* — the transactional interceptor, sanitization, and correlation propagation described in §1 — is built in **Phase 4**, before the first business mutation, so that every later handler inherits it. Only the Owner-facing viewer and activity monitor are built late, in Phase 14. See `docs/09-implementation-plan.md` §4.1.
