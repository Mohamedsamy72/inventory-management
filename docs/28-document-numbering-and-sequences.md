# 28 — Automatic Business Identifier & Sequence Specification

> **Document ID:** SPEC-28
> **Topic:** Server-generated item codes and document numbers — mechanism, concurrency, immutability, API contract, and UI behaviour
> **Status:** Authoritative
> **Authority:** Subordinate to `docs/decision-log.md` (ADR-008, ADR-024, ADR-025). Governs `docs/04 §5`, which is now a summary of this document.

---

## 1. Purpose

To specify, for every entity carrying a business identifier, exactly how that identifier is produced, why no client may supply it, and how concurrent creation is made safe. `docs/04 §5` mandated a `tenant_sequences` table that was never added to the schema (CR-050); this document closes that gap.

## 2. Scope

Every generated business identifier in the product: item codes and the document numbers of receiving orders, supply requests, supplies, stock counts, and discrepancies. Out of scope: surrogate `UUID` primary keys, which are not business identifiers and are never shown to users.

## 3. Terminology

| Term | Meaning |
| :--- | :--- |
| **Business identifier** | A human-facing code printed on documents and spoken aloud in the warehouse. |
| **Sequence period** | The window over which a counter is monotonic before resetting: `LIFETIME` or `MONTHLY`. |
| **Allocation** | Reserving the next value, inside the business transaction. |

## 4. Identifier Catalog

| Entity | Format | Prefix | Period | Allocation Trigger | Sequence Type Key |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **Item** | `ITM-{000000}` | `ITM-` | `LIFETIME` per company | `POST /api/v1/items` | `Item` |
| **Receiving Order** | `REC-{YYYYMM}-{0000}` | `REC-` | `MONTHLY` per company | `POST /api/v1/receiving-orders` (draft creation) | `ReceivingOrder` |
| **Supply Request** | `REQ-{YYYYMM}-{0000}` | `REQ-` | `MONTHLY` per company | `POST /api/v1/supply-requests` (draft creation) | `SupplyRequest` |
| **Supply / Dispatch** | `SUP-{YYYYMM}-{0000}` | `SUP-` | `MONTHLY` per company | `POST /api/v1/supply-requests/{id}/fulfill` | `Supply` |
| **Stock Count** | `CNT-{YYYYMM}-{0000}` | `CNT-` | `MONTHLY` per company | `POST /api/v1/stock-counts` | `StockCount` |
| **Discrepancy** | `DSC-{YYYYMM}-{0000}` | `DSC-` | `MONTHLY` per company | Variance trigger, inside the parent transaction | `Discrepancy` |

**Overflow.** `ITM-999999` and `-9999` within a month are hard limits. Reaching either returns `409 SEQUENCE_EXHAUSTED` with an Arabic message; it is an operational escalation, never a silent wrap.

## 5. Requirements

### 5.1 Server Authority
1. No create or update endpoint accepts `generatedCode` or `documentNumber` in a request body. The binding DTO does not declare the property, so an over-posted value is discarded by the model binder, not merely ignored by the handler.
2. A client that submits one receives `400 GENERATED_FIELD_NOT_ACCEPTED` (`"لا يمكن إدخال أرقام المستندات يدوياً — يتم توليدها تلقائياً بواسطة النظام"`) — an explicit rejection rather than a silent drop, so integration errors surface immediately.
3. Identifiers are generated once, at the trigger listed above, and never regenerated.

### 5.2 Storage
`document_sequences` (full DDL in `docs/29 §4.2`):

| Column | Type | Notes |
| :--- | :--- | :--- |
| `company_id` | `UUID NOT NULL` | Tenant. |
| `document_type` | `VARCHAR(40) NOT NULL` | The Sequence Type Key above. |
| `period_key` | `VARCHAR(6) NOT NULL` | `YYYYMM`, or `'LIFETIME'` for lifetime sequences. |
| `last_value` | `BIGINT NOT NULL DEFAULT 0` | Last allocated value. |
| `updated_at` | `TIMESTAMPTZ NOT NULL` | |

Primary key `(company_id, document_type, period_key)`.

### 5.3 Allocation Mechanism (ADR-025)

```sql
-- Executed inside the business transaction, immediately before INSERT.
INSERT INTO document_sequences (company_id, document_type, period_key, last_value, updated_at)
VALUES (@companyId, @documentType, @periodKey, 1, NOW())
ON CONFLICT (company_id, document_type, period_key)
DO UPDATE SET last_value = document_sequences.last_value + 1,
              updated_at = NOW()
RETURNING last_value;
```

**Why this and not a PostgreSQL `SEQUENCE`:** a sequence is non-transactional. A rolled-back document would burn its number and leave a gap, and auditors read a gap in a numbered series as a deleted record. The counter row rolls back with the transaction, so numbering is gap-free. The cost is that concurrent creators of the *same* document type in the *same* company and month serialize on one row — bounded, predictable, and correct.

**Prohibited:** `SELECT MAX(...) + 1`, client-side generation, `Guid`-derived codes, application-level in-memory counters, and PostgreSQL `SEQUENCE` objects for business identifiers.

### 5.4 Period Derivation (ADR-024)
`period_key` is derived from the document's **business date rendered in the tenant's configured timezone** (default `Africa/Cairo`), not from `NOW()` in UTC. Storage and transport remain UTC throughout.

### 5.5 Immutability
Once allocated, a business identifier is never modified. `items.generated_code` and every `document_number` column are excluded from all update DTOs, and an audit entry is written at allocation recording the value and the actor.

## 6. Business Rules

| ID | Rule |
| :--- | :--- |
| **BR-28-1** | Every business identifier is generated by the server, inside the transaction that creates its document. |
| **BR-28-2** | Numbering is gap-free and strictly monotonic within `(company_id, document_type, period_key)`. |
| **BR-28-3** | Identifiers are unique per company, enforced by a database unique constraint in addition to sequence allocation. |
| **BR-28-4** | Identifiers are immutable for the life of the record. |
| **BR-28-5** | The create form shows a disabled, read-only field reading `"توليد تلقائي بواسطة النظام"`. |
| **BR-28-6** | An identifier is displayed with `dir="ltr"` and bidirectional isolation so Arabic text does not invert its hyphens. |

## 7. Edge Cases

1. **Concurrent creation** — two users create an item simultaneously: the second `UPDATE` blocks on the row lock, then proceeds. Both succeed with distinct codes.
2. **Rollback** — the document fails validation after allocation: the counter rolls back and the number is reused by the next caller. No gap.
3. **Month boundary** — a document created at `23:30` Cairo time on the last day of the month uses the closing month's `period_key`, matching the business day the user would name (ADR-024).
4. **Idempotent retry** — a replayed `X-Idempotency-Key` returns the cached response containing the **original** number. No second allocation occurs (`docs/30`).
5. **Overflow** — `409 SEQUENCE_EXHAUSTED`.
6. **Legacy import** — a unique legacy code may be preserved in `generated_code`; the counter is then seeded above the highest imported numeric value (`docs/24 §2`).

## 8. Security Implications

- Accepting a client-supplied identifier would permit **document forgery** — overwriting or shadowing an existing document number within a tenant. The DTO-level exclusion is therefore a security control, not a convenience.
- Sequential per-company codes disclose approximate business volume to anyone inside that tenant. This is accepted: the audience is already inside the tenant, and gap-free auditability outweighs the disclosure.
- `document_sequences` is tenant-scoped and never exposed through any API.

## 9. Dependencies

- `docs/29` — DDL, constraints, indexes.
- `docs/30` — interaction with idempotency.
- `docs/07` — API contract for rejection of client-supplied identifiers.
- ADR-008, ADR-024, ADR-025.

## 10. Acceptance Criteria

- `AC-28-1` 1,000 concurrent item creations in one company yield 1,000 distinct codes with **no gaps** and **no duplicates**.
- `AC-28-2` A create request containing `generatedCode` or `documentNumber` returns `400 GENERATED_FIELD_NOT_ACCEPTED`.
- `AC-28-3` A transaction that rolls back after allocation leaves `last_value` unchanged.
- `AC-28-4` A document created at `23:30 Africa/Cairo` on the last day of a month carries that month's `period_key`.
- `AC-28-5` No update endpoint can alter an existing `generated_code` or `document_number`.
- `AC-28-6` A `discrepancies` row created by a variance carries a `DSC-` number.

## 11. Related Documents

`docs/04-end-to-end-business-flow.md` §5, `docs/06-database-schema.md`, `docs/29`, `docs/30`, `docs/07-api-specification.md`, `docs/24-data-migration-clean-start-rules.md`, `docs/32`.
