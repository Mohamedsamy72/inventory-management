# 29 — Database Integrity, Constraints & Index Specification

> **Document ID:** SPEC-29
> **Topic:** Schema corrections, composite tenant foreign keys, missing tables, uniqueness guards, immutability enforcement, partitioning, and the complete index plan
> **Status:** Authoritative
> **Authority:** This document **amends and extends** `docs/06-database-schema.md`. Where the two differ, **this document governs**, because it resolves defects catalogued in `docs/32`. `docs/06` remains the canonical table inventory.

---

## 1. Purpose

`docs/06` defines the tables but contains **no index definitions at all**, no tenant-consistent foreign keys, an unworkable concurrency token, and several tables that other specifications require but never declare. This document closes those gaps so that the initial EF Core migration can be authored once, correctly.

## 2. Scope

All PostgreSQL 16+ schema concerns: constraints, indexes, partitioning, immutability enforcement, and the tables missing from `docs/06`. Excludes business semantics, which live in `docs/02` and `docs/04`.

## 3. Terminology

| Term | Meaning |
| :--- | :--- |
| **Tenant-scoped table** | Any table carrying `company_id`. |
| **Composite tenant FK** | A foreign key on `(company_id, <id>)` rather than `(<id>)` alone. |
| **Posted document** | A document past its draft state whose lines have produced ledger effects. |

---

## 4. Schema Corrections & Additions

### 4.1 Corrections to `docs/06`

| ID | Table | Correction | Rationale |
| :--- | :--- | :--- | :--- |
| **DB-01** | `stock_balances` | **Remove** `version BYTEA NOT NULL`. Use the PostgreSQL `xmin` system column as the concurrency token. | ADR-022 / CR-035. `BYTEA` is a SQL Server idiom with no auto-maintenance in PostgreSQL; it would silently fail to detect lost updates. |
| **DB-02** | all tenant-scoped | Add `UNIQUE (company_id, id)` to every tenant-scoped table. | Target for composite foreign keys (ADR-016). |
| **DB-03** | all tenant-scoped FKs | Convert to composite `(company_id, <fk>)` referencing `(company_id, id)`. | ADR-016 / CR-065. Makes cross-tenant references physically impossible. |
| **DB-04** | `user_warehouse_scopes`, `user_restaurant_scopes` | Add the real foreign keys (currently only SQL comments) as composite FKs. | CR-016. |
| **DB-05** | `user_roles` | Add `UNIQUE (user_id)`. | ADR-014 / CR-013 — exactly one product role per user. |
| **DB-06** | `discrepancies` | Add `document_number VARCHAR(50) NOT NULL` + `UNIQUE (company_id, document_number)`; add `reference_line_id UUID`. | CR-051, CR-025. |
| **DB-07** | `supply_items` | Add `supply_request_item_id UUID NULL` (composite FK). | CR-024 — line-level traceability request → dispatch → receipt. |
| **DB-08** | `supplies` | Widen the status domain to `Prepared`, `Dispatched`, `Confirmed`, `ConfirmedWithDiscrepancy`, `RejectedAtDelivery`, `Cancelled`. Add `prepared_by`, `prepared_at`. | ADR-017 / CR-020, CR-021. |
| **DB-09** | `receiving_orders` | Add `reversed_by UUID`, `reversed_at TIMESTAMPTZ`, `reversal_reason TEXT`. | CR-040. |
| **DB-10** | `receiving_order_items` | Add `reconciled BOOLEAN NOT NULL DEFAULT FALSE`. | CR-041 — prevents a second reconciliation double-posting. |
| **DB-11** | `warehouses`, `restaurants`, `suppliers` | Rename `name` → `name_arabic`. | CR-081 — consistency with `items`, `categories`, `units`; signals the Arabic-only content rule. |
| **DB-12** | line tables | Add per-parent item uniqueness (see §4.3). | CR-023 — duplicate-item handling. |
| **DB-13** | `stock_ledger` | Add the posting-uniqueness guard (see §4.3). | CR-036 — prevents double-posting a document. |
| **DB-14** | `audit_logs` | Declare `PARTITION BY RANGE (created_at)`, monthly partitions. | CR-070 / `docs/21` 7-year retention. |
| **DB-15** | line tables | Narrow `ON DELETE CASCADE` to `RESTRICT` for non-draft parents. | CR-067. |
| **DB-16** | `users` | Add `access_failed_count INT NOT NULL DEFAULT 0`, `lockout_end_at TIMESTAMPTZ NULL`. | CR-062. |
| **DB-17** | `restaurants` | Add `default_serving_warehouse_id UUID NOT NULL` with a **composite tenant FK** `(company_id, default_serving_warehouse_id) → warehouses(company_id, id)`. | ADR-028 / CR-090. The supply request's warehouse is derived from this column, never supplied by the client. |

### 4.2 Missing Tables

```sql
-- Gap-free business identifier counters (docs/28, ADR-025). CR-050.
CREATE TABLE document_sequences (
    company_id    UUID        NOT NULL REFERENCES companies(id),
    document_type VARCHAR(40) NOT NULL,
    period_key    VARCHAR(6)  NOT NULL,   -- 'YYYYMM' or 'LIFETI' for lifetime
    last_value    BIGINT      NOT NULL DEFAULT 0 CHECK (last_value >= 0),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (company_id, document_type, period_key)
);

-- Hashed, single-use, expiring password-reset OTPs (docs/08). CR-061.
CREATE TABLE password_reset_otps (
    id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id    UUID        NOT NULL REFERENCES companies(id),
    user_id       UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    otp_hash      VARCHAR(128) NOT NULL,          -- never plaintext
    attempt_count INT         NOT NULL DEFAULT 0,
    consumed_at   TIMESTAMPTZ,
    expires_at    TIMESTAMPTZ NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_ip    VARCHAR(45)
);

-- Links stored evidence files to the documents they evidence (docs/16). CR-066.
CREATE TABLE file_attachments (
    id             UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id     UUID        NOT NULL REFERENCES companies(id),
    stored_file_id UUID        NOT NULL REFERENCES stored_files(id),
    entity_type    VARCHAR(50) NOT NULL,  -- 'ReceivingOrder' | 'Supply' | 'Discrepancy' | 'StockCount'
    entity_id      UUID        NOT NULL,
    attached_by    UUID        NOT NULL REFERENCES users(id),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_file_attachment UNIQUE (stored_file_id, entity_type, entity_id)
);

-- Tenant settings: timezone (ADR-024), currency (OD-006).
CREATE TABLE company_settings (
    company_id     UUID        PRIMARY KEY REFERENCES companies(id),
    timezone_id    VARCHAR(64) NOT NULL DEFAULT 'Africa/Cairo',
    currency_code  VARCHAR(3)  NOT NULL DEFAULT 'EGP',
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

```sql
-- ADR-028 (CR-090): each restaurant has exactly ONE serving warehouse in v1.0.
ALTER TABLE restaurants
    ADD COLUMN default_serving_warehouse_id UUID NOT NULL;

-- Composite tenant FK: the serving warehouse can only ever be in the same company.
-- This is what makes SW-3 structural rather than an application convention.
ALTER TABLE restaurants
    ADD CONSTRAINT fk_restaurants_serving_warehouse
    FOREIGN KEY (company_id, default_serving_warehouse_id)
    REFERENCES warehouses (company_id, id);

CREATE INDEX ix_restaurants_serving_wh
    ON restaurants (company_id, default_serving_warehouse_id);
```

> **No `restaurant_serving_warehouses` join table exists.** v1.0 is deliberately one-to-one (ADR-028). A many-to-many mapping with a selection rule is a future version increment, not a patch — and it would require a decision about which warehouse serves a given request, which is precisely the client-trust hazard this design removes.

> **Active-status check.** `warehouses.status = 'Active'` is enforced at the **application** layer, not by a constraint: a warehouse may legitimately be deactivated while restaurants still reference it historically, and a database constraint would block that deactivation. The application refuses *new* requests with `409 SERVING_WAREHOUSE_UNAVAILABLE` (SW-5) instead.

### 4.3 Uniqueness & Integrity Guards

```sql
-- One line per item per document (CR-023). Same shape on every line table.
ALTER TABLE supply_request_items   ADD CONSTRAINT uq_sri_item UNIQUE (supply_request_id, item_id);
ALTER TABLE supply_items           ADD CONSTRAINT uq_si_item  UNIQUE (supply_id, item_id);
ALTER TABLE receiving_order_items  ADD CONSTRAINT uq_roi_item UNIQUE (receiving_order_id, item_id);
ALTER TABLE stock_count_items      ADD CONSTRAINT uq_sci_item UNIQUE (stock_count_id, item_id);

-- A business document may post a given movement type for a given item exactly once (CR-036).
ALTER TABLE stock_ledger ADD CONSTRAINT uq_ledger_posting
    UNIQUE (company_id, reference_type, reference_id, item_id, movement_type);

-- Exactly one product role per user (ADR-014).
ALTER TABLE user_roles ADD CONSTRAINT uq_user_single_role UNIQUE (user_id);

-- Received quantity never exceeds dispatched quantity.
ALTER TABLE supply_items ADD CONSTRAINT ck_si_received
    CHECK (received_quantity IS NULL OR (received_quantity >= 0 AND received_quantity <= dispatched_quantity));

-- Fulfilled quantity never exceeds requested quantity.
ALTER TABLE supply_request_items ADD CONSTRAINT ck_sri_fulfilled
    CHECK (fulfilled_quantity >= 0 AND fulfilled_quantity <= requested_quantity);

-- Base quantities are always positive magnitudes on documents.
ALTER TABLE receiving_order_items ADD CONSTRAINT ck_roi_base CHECK (base_quantity > 0);

-- A conversion must target the item's own base unit (ADR-023 / CR-038).
-- Enforced by composite FK (company_id, item_id, to_base_unit_id) -> items(company_id, id, base_unit_id)
-- backed by UNIQUE (company_id, id, base_unit_id) on items, plus application validation.
ALTER TABLE items ADD CONSTRAINT uq_items_base_unit UNIQUE (company_id, id, base_unit_id);
```

### 4.4 Append-Only Enforcement (CR-069)

Application-level discipline is insufficient — an ORM bug or a stray script would silently rewrite history.

```sql
CREATE RULE stock_ledger_no_update AS ON UPDATE TO stock_ledger DO INSTEAD NOTHING;
CREATE RULE stock_ledger_no_delete AS ON DELETE TO stock_ledger DO INSTEAD NOTHING;
CREATE RULE audit_logs_no_update  AS ON UPDATE TO audit_logs  DO INSTEAD NOTHING;
CREATE RULE audit_logs_no_delete  AS ON DELETE TO audit_logs  DO INSTEAD NOTHING;

REVOKE UPDATE, DELETE, TRUNCATE ON stock_ledger, audit_logs FROM app_user;
```

> `DO INSTEAD NOTHING` silently discards the statement. Where a loud failure is preferred, substitute a `BEFORE UPDATE OR DELETE` trigger raising `SQLSTATE '55006'`. **Recommendation: use the trigger**, so a defect surfaces in tests instead of hiding as a no-op. The rule form is documented only because `docs/06` implies it.

### 4.5 Audit Partitioning (CR-070)

```sql
CREATE TABLE audit_logs (...) PARTITION BY RANGE (created_at);
CREATE TABLE audit_logs_2026_09 PARTITION OF audit_logs
    FOR VALUES FROM ('2026-09-01Z') TO ('2026-10-01Z');
```

Partitions are created one month ahead by a scheduled maintenance task. Retention is 7 years (`docs/21`); partitions are detached and archived, never dropped while inside the retention window.

---

## 5. Complete Index Plan (CR-068)

`docs/21` mandates keyset pagination "over indexed columns" and a P95 of 150 ms for reads. No index was ever specified. The following is the minimum viable set.

### 5.1 Tenant & Keyset Foundation
Every list endpoint paginates on `(company_id, created_at DESC, id DESC)`. Each tenant-scoped table therefore carries:

```sql
CREATE INDEX ix_<table>_tenant_keyset ON <table> (company_id, created_at DESC, id DESC);
```

### 5.2 Inventory Hot Paths

```sql
-- Balance lookup during confirmation and receiving (the single hottest query).
-- Covered by uq_stock_balances (company_id, warehouse_id, item_id) — no extra index needed.

-- Ledger history per item per warehouse, newest first.
CREATE INDEX ix_ledger_wh_item_time  ON stock_ledger (company_id, warehouse_id, item_id, occurred_at DESC);
-- Ledger traceback from a source document.
CREATE INDEX ix_ledger_reference     ON stock_ledger (company_id, reference_type, reference_id);
-- Movement-type reporting (RPT_STOCK_MOVEMENT).
CREATE INDEX ix_ledger_type_time     ON stock_ledger (company_id, movement_type, occurred_at DESC);

-- In-transit derivation (ADR-018): sum over supplies in state 'Dispatched'.
CREATE INDEX ix_supplies_wh_status   ON supplies (company_id, warehouse_id, status);
CREATE INDEX ix_supply_items_supply  ON supply_items (supply_id, item_id);
```

### 5.3 Workflow Queues (Dashboard Widgets)

```sql
CREATE INDEX ix_reqs_wh_status  ON supply_requests  (company_id, warehouse_id, status, requested_at DESC);
CREATE INDEX ix_reqs_rest_status ON supply_requests (company_id, restaurant_id, status, requested_at DESC);
CREATE INDEX ix_sup_rest_status ON supplies         (company_id, restaurant_id, status, dispatched_at DESC);
CREATE INDEX ix_rec_wh_status   ON receiving_orders (company_id, warehouse_id, status, business_date DESC);
CREATE INDEX ix_cnt_wh_status   ON stock_counts     (company_id, warehouse_id, status, opened_at DESC);
CREATE INDEX ix_disc_status     ON discrepancies    (company_id, status, created_at DESC);
CREATE INDEX ix_disc_reference  ON discrepancies    (company_id, reference_type, reference_id);
```

### 5.4 Master Data & Search

```sql
CREATE INDEX ix_items_active_cat ON items (company_id, is_active, category_id);
-- Arabic substring search on item names. pg_trgm handles Arabic byte-wise; adequate for
-- catalogue-scale search and far cheaper than full-text with an Arabic dictionary.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX ix_items_name_trgm  ON items USING GIN (name_arabic gin_trgm_ops);
CREATE INDEX ix_conv_item        ON item_unit_conversions (company_id, item_id, is_active);
```

### 5.5 Security, Scope & Audit

```sql
CREATE INDEX ix_uws_user ON user_warehouse_scopes  (user_id);
CREATE INDEX ix_urs_user ON user_restaurant_scopes (user_id);
CREATE INDEX ix_audit_tenant_time  ON audit_logs (company_id, created_at DESC);
CREATE INDEX ix_audit_actor        ON audit_logs (company_id, actor_user_id, created_at DESC);
CREATE INDEX ix_audit_entity       ON audit_logs (company_id, entity_type, entity_id);
CREATE INDEX ix_idem_lookup        ON idempotency_records (company_id, user_id, idempotency_key);
CREATE INDEX ix_idem_expiry        ON idempotency_records (expires_at);
CREATE INDEX ix_otp_user_active    ON password_reset_otps (user_id, expires_at DESC) WHERE consumed_at IS NULL;
```

### 5.6 Consumption

```sql
CREATE INDEX ix_cons_rest_date ON consumption_records (company_id, restaurant_id, consumption_date DESC);
CREATE INDEX ix_cons_item_date ON consumption_records (company_id, item_id, consumption_date DESC);
```

---

## 6. Business Rules Enforced at the Database Layer

| ID | Rule | Mechanism |
| :--- | :--- | :--- |
| **BR-29-1** | Warehouse stock is never negative. | `CHECK (quantity >= 0)` + conditional `UPDATE`. |
| **BR-29-2** | No restaurant inventory table may exist. | Architecture test scanning the migration for prohibited names (`docs/25`). |
| **BR-29-3** | The stock ledger and audit log are append-only. | §4.4 trigger + revoked grants. |
| **BR-29-4** | Cross-tenant references are impossible. | Composite tenant FKs (§4.1 DB-02/03). |
| **BR-29-5** | A document posts each movement once. | `uq_ledger_posting`. |
| **BR-29-6** | One product role per user. | `uq_user_single_role`. |
| **BR-29-7** | An item appears at most once per document. | Per-parent uniqueness (§4.3). |
| **BR-29-8** | Received never exceeds dispatched; fulfilled never exceeds requested. | `CHECK` constraints. |
| **BR-29-9** | Business identifiers are unique per company. | `UNIQUE (company_id, document_number)` per document table. |
| **BR-29-10** | A restaurant's serving warehouse is always in the same company. | Composite FK `fk_restaurants_serving_warehouse` (ADR-028). |

## 7. Edge Cases

1. **`xmin` and `TOAST`** — `xmin` changes on every row update, including no-op writes. EF must not issue redundant `SaveChanges` on `stock_balances`, or spurious concurrency exceptions appear.
2. **Partition boundary** — an audit write at the instant a new month begins requires the next partition to already exist. Created one month ahead; a missing partition is a hard failure, never a silent drop.
3. **`pg_trgm` and Arabic** — trigram matching operates on bytes, so it does not normalize Arabic diacritics or `أ/ا` variants. Application-level normalization before storage and before query is required (`docs/31`).
4. **Composite FK migration cost** — composite FKs must be present in the **initial** migration. Retrofitting them after data exists requires a full table rewrite.
5. **Idempotency cleanup** — `idempotency_records` grows without bound unless a scheduled job deletes rows past `expires_at`.

## 8. Security Implications

- **DB-03 (composite tenant FKs)** is the strongest single control in the system: it makes cross-tenant data structurally impossible rather than merely filtered. It is security-critical and must land in Phase 2.
- **§4.4 (append-only enforcement)** protects the audit trail from application compromise.
- **DB-05 (one role per user)** eliminates a privilege-escalation path in which a user holds `Admin` and a second role that shadows Role Denial.
- **DB-16 (lockout columns)** enables brute-force resistance beyond IP-based rate limiting.
- `password_reset_otps` stores only hashes; an attacker with read access to the database still cannot reset an account.

## 9. Dependencies

`docs/06-database-schema.md` (table inventory), `docs/28` (sequences), `docs/30` (idempotency, concurrency), `docs/04` (business semantics), ADR-014, ADR-016, ADR-018, ADR-022, ADR-023, ADR-025.

## 10. Acceptance Criteria

- `AC-29-1` A migration-inspection architecture test finds no table or column matching `restaurant_%stock%`, `%in_transit%`, `barcode`, `name_en%`.
- `AC-29-2` Every tenant-scoped foreign key in the generated model is composite on `company_id`.
- `AC-29-3` An attempt to `UPDATE` or `DELETE` a `stock_ledger` or `audit_logs` row fails loudly.
- `AC-29-4` `stock_balances` has no `version` column and its EF configuration uses `xmin`.
- `AC-29-5` A concurrency integration test observes a `DbUpdateConcurrencyException` on a simultaneous WAC recomputation.
- `AC-29-6` Every index in §5 exists after `dotnet ef database update`.
- `AC-29-7` Inserting a second line for the same item on one document fails on the unique constraint.
- `AC-29-8` Assigning a second role to a user fails on `uq_user_single_role`.
- `AC-29-9` `audit_logs` is partitioned and the current plus next month's partitions exist.
- `AC-29-10` `restaurants.default_serving_warehouse_id` is `NOT NULL` and its composite FK rejects a warehouse from another company.
- `AC-29-11` No table named `restaurant_serving_warehouses` exists.

## 11. Related Documents

`docs/06-database-schema.md`, `docs/28`, `docs/30`, `docs/04-end-to-end-business-flow.md`, `docs/21-non-functional-requirements.md`, `docs/25-developer-rules-anti-patterns.md`, `docs/32`, `docs/decision-log.md`.
