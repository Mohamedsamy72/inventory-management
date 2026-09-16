# 22 — Phased Implementation Roadmap (Superseded)

> **Status:** ⛔ **SUPERSEDED.** This file contains no active specification content.
>
> The authoritative, dependency-aware implementation plan is **`docs/09-implementation-plan.md`**.
>
> **Why it was superseded.** The original 16-phase roadmap here was written before the specification reconciliation pass and placed audit logging at Phase 9 and security hardening at Phase 13. Dependency analysis showed both must move earlier: audit entries are written inside the same transaction as their business change (`docs/14 §1`), so audit infrastructure must exist before the first business mutation; and composite tenant foreign keys must be present in the initial migration, because retrofitting them onto populated tables requires a full table rewrite. The full rationale is in `docs/09-implementation-plan.md` §4.
>
> The Zero-Missing Verification Gate defined here survives, in stricter form, as `docs/27-implementation-status.md` §6.
