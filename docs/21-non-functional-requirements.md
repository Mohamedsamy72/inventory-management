# 21 — Non-Functional Requirements & Observability

> **Document ID:** SPEC-21  
> **Topic:** Performance, Concurrency, Availability, Observability, and Data Retention

---

## 1. Performance & Latency Budgets

- **Operational Read Endpoints:** P95 latency $< 150\text{ ms}$ for list and detail queries.
- **Stock Mutation Endpoints:** P95 latency $< 300\text{ ms}$ for atomic posting and receipt confirmation transactions.
- **Dashboard Loaders:** P95 latency $< 200\text{ ms}$ with real database-backed count queries (no table scans).
- **Database Query Guardrails:** All queries must use explicit `.Take()` or Keyset Cursor limits (default 25 items, max 100 items). Unbounded `SELECT *` queries are strictly forbidden.

---

## 2. Concurrency & High Availability

- **Optimistic Concurrency:** `StockBalance` updates use byte-array row version checks to prevent lost updates.
- **Pessimistic Row Locks:** Critical stock allocations use PostgreSQL `SELECT ... FOR UPDATE` when evaluating stock sufficiency before deduction.
- **Idempotency Window:** Idempotency keys are cached for 24 hours to prevent duplicate submissions from unstable mobile network retries.

---

## 3. Observability, Logging & Retention

- **Structured Logging (Serilog):** JSON-formatted logs containing `CorrelationId`, `CompanyId`, `UserId`, `HttpMethod`, and `ElapsedMs`.
- **Sensitive Data Exclusion:** Logging middleware strictly redacts passwords, OTP tokens, Authorization headers, and credit/bank information.
- **Audit Retention Policy:** `audit_logs` are preserved for a minimum of 7 years in immutable PostgreSQL partitions.
