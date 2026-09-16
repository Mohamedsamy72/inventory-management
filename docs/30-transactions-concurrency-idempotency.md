# 30 — Transactions, Concurrency & Idempotency Specification

> **Document ID:** SPEC-30
> **Topic:** Transaction boundaries, locking strategy, idempotency contract, failure and retry behaviour for every critical mutation
> **Status:** Authoritative
> **Authority:** Expands `docs/04 §16`, which remains the business-level summary.

---

## 1. Purpose

`docs/04 §16` names three transactional operations and states that mutating requests "accept" an idempotency key. It does not say which endpoints **require** one, what happens when a key is replayed with a different payload, or how a legitimately impossible confirmation is resolved. Those omissions are exactly where inventory systems corrupt themselves, so they are specified here.

## 2. Scope

Every server operation that writes to `stock_ledger`, `stock_balances`, or any document status field. Excludes read queries and pure master-data creation, which need no special protocol beyond ordinary validation.

## 3. Terminology

| Term | Meaning |
| :--- | :--- |
| **Critical mutation** | An operation that changes physical stock or advances a document lifecycle state. |
| **Idempotency key** | A client-generated `UUID` in `X-Idempotency-Key` identifying one logical business intent. |
| **Replay** | A repeat of the same key with the same payload. |
| **Key reuse** | The same key with a *different* payload — always an error. |

---

## 4. Transaction Boundaries

Each of the following executes as **one** ACID transaction. Partial completion is impossible.

| # | Operation | Writes Inside the Transaction |
| :--- | :--- | :--- |
| **T1** | Receiving submit (post) | Allocate number (if not already) → append `INCOMING_POSTED` per line → upsert `stock_balances` + recompute WAC → set status `Submitted` → `audit_logs` → `idempotency_records`. |
| **T2** | Receiving verify (reconcile) | Append `INCOMING_RECONCILIATION` for non-zero deltas → update `stock_balances` (reverse at the line's own `unit_cost`, ADR-020) → insert `discrepancies` (+ `DSC-` number) → mark lines `reconciled` → set status `Verified` → `audit_logs` → `idempotency_records`. |
| **T3** | Receiving reverse | Append reversing movements → update `stock_balances` → set status `Reversed` with actor, time, reason → `audit_logs` → `idempotency_records`. |
| **T4** | Supply request submit | Set status `Submitted` → freeze lines → `audit_logs`. **No stock effect.** |
| **T5** | Fulfilment (create Supply, ADR-017) | Allocate `SUP-` number → create `Supply` in `Prepared` → create `supply_items` linked to `supply_request_items` → update `supply_request_items.fulfilled_quantity` → set request `PartiallyFulfilled`/`Fulfilled` → `audit_logs`. **No stock effect.** |
| **T6** | Dispatch | `Prepared → Dispatched`, record `dispatched_by`/`dispatched_at` → `audit_logs`. **No stock effect.** |
| **T7** | Receipt confirmation | Verify idempotency → lock balances → append `RESTAURANT_RECEIPT_CONFIRMED` per line → conditional `UPDATE` on `stock_balances` → insert `discrepancies` for variances → set supply status → `audit_logs` → `idempotency_records`. |
| **T8** | Stock count approve | Append `PHYSICAL_ADJUSTMENT` for non-zero variances → update `stock_balances` → insert `discrepancies` → set status `Approved` → `audit_logs` → `idempotency_records`. |
| **T9** | Consumption record | Insert `consumption_records` → `audit_logs`. **No stock effect.** |

**Isolation level:** PostgreSQL `READ COMMITTED` throughout. `SERIALIZABLE` is deliberately avoided: the conditional `UPDATE` in §5.1 and the row-level counter lock in `docs/28` already provide the needed guarantees without serialization failures forcing application-level retry loops.

**Rule:** the audit entry is written inside the same transaction as the business change (`docs/14 §1`). A rolled-back operation leaves no audit trace of success.

---

## 5. Concurrency Strategy

### 5.1 Stock Deduction — Conditional Atomic Update (Primary Guard)

```sql
UPDATE stock_balances
   SET quantity   = quantity - @deduction,
       updated_at = NOW()
 WHERE company_id = @companyId
   AND warehouse_id = @warehouseId
   AND item_id    = @itemId
   AND quantity  >= @deduction;
```

Zero rows affected → `400 INSUFFICIENT_STOCK`, whole transaction rolled back. The predicate is evaluated by PostgreSQL under a row lock, so two concurrent deductions cannot both pass. The `CHECK (quantity >= 0)` constraint remains as an independent last line of defence.

### 5.2 Stock Addition & WAC Recomputation — Optimistic Concurrency

Recomputing the Weighted Average Cost is a read-modify-write and cannot be expressed as a single conditional `UPDATE`. It is protected by the `xmin` concurrency token (ADR-022). A conflict raises `DbUpdateConcurrencyException` → `409 CONCURRENCY_CONFLICT`.

### 5.3 Lock Ordering

A multi-line confirmation touches several balance rows. To eliminate deadlock between two confirmations sharing items, **balance rows are always locked in ascending `item_id` order**. This is a mandatory implementation rule, not an optimization.

### 5.4 Sequence Allocation

Serialized on one `document_sequences` row per `(company, type, period)` — see `docs/28 §5.3`. Allocation happens **early** in the transaction, before balance locks, to keep the two lock domains ordered and non-overlapping.

---

## 6. Idempotency Contract

### 6.1 Requirement Matrix

| Endpoint | `X-Idempotency-Key` | Rationale |
| :--- | :---: | :--- |
| `POST /supplies/{id}/confirm` | **REQUIRED** | Deducts stock. A network retry must never deduct twice. |
| `POST /receiving-orders/{id}/submit` | **REQUIRED** | Adds stock. |
| `POST /receiving-orders/{id}/verify` | **REQUIRED** | Adjusts stock. |
| `POST /receiving-orders/{id}/reverse` | **REQUIRED** | Adjusts stock. |
| `POST /stock-counts/{id}/approve` | **REQUIRED** | Adjusts stock. |
| `POST /supply-requests/{id}/fulfill` | **RECOMMENDED** | Allocates a document number; a retry would otherwise create a second supply. |
| `POST /supply-requests` | **RECOMMENDED** | Allocates a document number. |
| `POST /supplies/{id}/dispatch` | OPTIONAL | State transition only; naturally idempotent via the state machine. |
| All other mutations | OPTIONAL | Accepted and honoured where supplied. |

A **REQUIRED** endpoint called without the header returns `400 IDEMPOTENCY_KEY_REQUIRED` (`"مفتاح منع التكرار مطلوب لهذه العملية"`).

### 6.2 Protocol

1. Compute `request_hash = SHA256(canonical(method, path, body))`.
2. Look up `(company_id, user_id, idempotency_key)`.
3. **Not found** → proceed; insert the `idempotency_records` row **inside** the business transaction alongside the response status and payload.
4. **Found, hash matches** → return the stored status and payload verbatim. Perform **no** business work, **no** ledger write, **no** sequence allocation.
5. **Found, hash differs** → `409 IDEMPOTENCY_KEY_REUSE` (`"تم استخدام مفتاح منع التكرار هذا لعملية مختلفة"`). Never execute; this is a client defect or an attack.
6. **Concurrent duplicate** — two identical requests race, both miss the lookup: the unique constraint `uq_idempotency_key` makes the second `INSERT` fail, its transaction rolls back, and it re-reads and returns the first result. **The unique constraint, not the lookup, is the actual guarantee.**

### 6.3 Scope and Retention
- Keys are scoped to `(company_id, user_id)`. A key from one user can never replay another user's operation, and never crosses a tenant.
- Retention: **24 hours** (`docs/21 §2`), then removed by a scheduled cleanup job.
- A replayed key past expiry is treated as new. This is why idempotency is a **retry** safeguard, not a permanent duplicate-submission guard; the document state machine provides that (a `Confirmed` supply cannot be confirmed again — `409 ALREADY_CONFIRMED`).

---

## 7. Failure & Retry Behaviour

| Failure | HTTP | Code | Server Action | Client Action |
| :--- | :---: | :--- | :--- | :--- |
| Insufficient stock at confirmation | 400 | `INSUFFICIENT_STOCK` | Full rollback; nothing written. | Show the Arabic message naming item and warehouse; escalate to Owner/Admin. |
| WAC concurrency conflict | 409 | `CONCURRENCY_CONFLICT` | Full rollback. | Safe to retry **with the same idempotency key**. |
| Idempotent replay | 200/201 | — | No work; cached response returned. | Treat as success. |
| Key reuse with different body | 409 | `IDEMPOTENCY_KEY_REUSE` | No work. | Do not retry; fix the client. |
| Required key missing | 400 | `IDEMPOTENCY_KEY_REQUIRED` | No work. | Generate a `UUID` and retry. |
| Illegal state transition | 400 | `INVALID_STATE_TRANSITION` | No work. | Refresh the document. |
| Already confirmed | 409 | `ALREADY_CONFIRMED` | No work. | Refresh; the operation already succeeded. |
| Sequence exhausted | 409 | `SEQUENCE_EXHAUSTED` | Full rollback. | Operational escalation. |

### 7.1 The Impossible Confirmation (ADR-019)

Because dispatch never reserves, a warehouse balance can be depleted between dispatch and confirmation, leaving a supervisor holding physical goods they cannot confirm.

**Defined resolution — the only approved path:**
1. Confirmation fails with `INSUFFICIENT_STOCK`. The supply remains `Dispatched`; nothing is written.
2. The supervisor is shown an Arabic explanation and a `[ إبلاغ إدارة المخزن ]` action, which raises a `Discrepancy` of type `StockUnavailableAtConfirmation` in state `Open`.
3. An `Owner` or `Admin` runs a physical stock count on the warehouse, establishing the true balance via `PHYSICAL_ADJUSTMENT`.
4. The supervisor retries the confirmation, which now succeeds.

**Never permitted:** allowing a negative balance, silently reducing the confirmed quantity to fit available stock, or auto-posting an adjustment without approval. Each would substitute a system fiction for a physical fact.

---

## 8. Business Rules

| ID | Rule |
| :--- | :--- |
| **BR-30-1** | Every stock mutation is atomic with its audit entry and its document status change. |
| **BR-30-2** | Balance rows are locked in ascending `item_id` order. |
| **BR-30-3** | Sequence allocation precedes balance locking within a transaction. |
| **BR-30-4** | A replayed idempotency key performs zero business work. |
| **BR-30-5** | A reused key with a different payload is rejected, never executed. |
| **BR-30-6** | Negative stock is never produced, not even transiently. |
| **BR-30-7** | No auto-correcting adjustment is ever posted without human approval. |

## 9. Edge Cases

1. Idempotency key replayed after 24-hour expiry → the state machine catches it (`ALREADY_CONFIRMED`).
2. Client sends the same key to two different endpoints → hash differs → `IDEMPOTENCY_KEY_REUSE`.
3. Server crashes after commit but before responding → client retries with the key → cached response returned. This is the case the whole mechanism exists for.
4. Confirmation of a multi-line supply where line 3 of 5 is short → the **entire** transaction rolls back. Partial confirmation is never written.
5. Two dispatchers fulfil the same request concurrently → the second sees `fulfilled_quantity` already advanced and either fulfils the remainder or fails on `ck_sri_fulfilled`.
6. Stock count approved while a confirmation is in flight → both take balance row locks; whichever commits second sees the other's effect.

## 10. Security Implications

- Idempotency records are scoped to `(company_id, user_id)`; a key can never be used to read back another user's or another tenant's response payload.
- Cached response payloads are stored **after** role-based financial projection, so an Owner's cached response can never be replayed to a non-Owner. Implementation must store the projected DTO, never the domain object.
- Rejecting key reuse prevents an attacker from using a captured key to force a *different* operation to appear idempotent.
- Deadlock-free lock ordering removes a denial-of-service vector.

## 11. Dependencies

`docs/04 §16`, `docs/28`, `docs/29`, `docs/13-validation-error-handling.md` (error catalog), `docs/21` (retention), ADR-019, ADR-020, ADR-021, ADR-022, ADR-025.

## 12. Acceptance Criteria

- `AC-30-1` Two concurrent confirmations of 15 from a balance of 20: exactly one succeeds; the balance ends at 5; never negative.
- `AC-30-2` A repeated confirmation with the same key returns the original response and writes no second ledger row.
- `AC-30-3` The same key with a different body returns `409 IDEMPOTENCY_KEY_REUSE`.
- `AC-30-4` A confirm call without the header returns `400 IDEMPOTENCY_KEY_REQUIRED`.
- `AC-30-5` A five-line confirmation failing on one line writes nothing at all.
- `AC-30-6` A forced concurrency conflict on WAC recomputation returns `409 CONCURRENCY_CONFLICT` and leaves the balance unchanged.
- `AC-30-7` No audit entry exists for a rolled-back operation.
- `AC-30-8` A deadlock-probe test running interleaved multi-item confirmations reports zero deadlocks.

## 13. Related Documents

`docs/04-end-to-end-business-flow.md`, `docs/06-database-schema.md`, `docs/28`, `docs/29`, `docs/13-validation-error-handling.md`, `docs/18-testing-strategy.md`, `docs/21-non-functional-requirements.md`, `docs/32`.
