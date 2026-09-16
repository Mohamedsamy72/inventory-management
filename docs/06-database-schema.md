# 06 — Database Schema Specification (PostgreSQL 16+ / EF Core)

> ⚠️ **AMENDED BY `docs/29-database-integrity-indexes-constraints.md`.** This document is the canonical **table inventory**. `docs/29` corrects and extends it with composite tenant foreign keys, the corrected concurrency token, uniqueness and check constraints, append-only enforcement, audit partitioning, four missing tables, and the complete index plan. **Where the two differ, `docs/29` governs.**
>
> **Known defects corrected in `docs/29` — do not implement this file as written:**
> - `stock_balances.version BYTEA` is not a working PostgreSQL concurrency token → use `xmin` (ADR-022, CR-035).
> - Every foreign key is single-column → must be composite `(company_id, fk)` (ADR-016, CR-065).
> - `user_warehouse_scopes` / `user_restaurant_scopes` have FK comments but no actual FKs (CR-016).
> - **No index is defined anywhere in this file** (CR-068).
> - `document_sequences`, `password_reset_otps`, `file_attachments`, `company_settings` are missing (CR-050, CR-061, CR-066).
> - `discrepancies` lacks `document_number` and `reference_line_id` (CR-051, CR-025).
> - `supply_items` lacks `supply_request_item_id` (CR-024).
> - The `supplies.status` comment omits `Prepared`, `ConfirmedWithDiscrepancy`, `RejectedAtDelivery`, `Cancelled`, and wrongly lists `Discrepancy` (ADR-017, CR-021).
> - `user_roles` permits multiple roles per user (ADR-014, CR-013).
> - `restaurants` lacks `default_serving_warehouse_id` (ADR-028, CR-090).
> - `audit_logs` is unpartitioned despite a 7-year retention requirement (CR-070).
> - Append-only is stated but unenforced (CR-069).

> **Document ID:** SPEC-06  
> **Topic:** Comprehensive Relational Schema, Tables, Constraints, Types, Indexes, and EF Core Mapping Rules

---

## 1. Global Schema Design Principles

1. **PostgreSQL Compatibility:** Optimized for PostgreSQL 16+ using native `uuid`, `numeric(18,4)`, `timestamptz`, and `jsonb`.
2. **Tenant Isolation:** Every operational table contains `company_id` (UUID) with a foreign key to `companies(id)`.
3. **Immutability of Ledgers:** Append-only tables (`stock_ledger`, `audit_logs`) do not support update/delete operations.
4. **Naming Conventions:** Table and column names use snake_case in PostgreSQL, mapped to PascalCase in C# entities.

---

## 2. Table Specifications

### 2.1 Organizational & Identity Tables

```sql
CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    status VARCHAR(30) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    full_name VARCHAR(200) NOT NULL,
    mobile_number VARCHAR(30) NOT NULL,
    password_hash VARCHAR(500) NOT NULL,
    security_stamp VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_users_company_mobile UNIQUE (company_id, mobile_number)
);

CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE, -- 'Owner', 'Admin', 'WarehouseStaff', 'RestaurantSupervisor', 'User'
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE user_roles (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(100) NOT NULL UNIQUE, -- e.g. 'items:create'
    description VARCHAR(250) NOT NULL,
    module VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE role_permissions (
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_permissions (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    is_granted BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (user_id, permission_id)
);

CREATE TABLE user_warehouse_scopes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    warehouse_id UUID NOT NULL, -- references warehouses(id)
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_warehouse UNIQUE (user_id, warehouse_id)
);

CREATE TABLE user_restaurant_scopes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    restaurant_id UUID NOT NULL, -- references restaurants(id)
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_restaurant UNIQUE (user_id, restaurant_id)
);
```

---

### 2.2 Master Data Tables

```sql
CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    name_arabic VARCHAR(150) NOT NULL,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_categories_company_name UNIQUE (company_id, name_arabic)
);

CREATE TABLE units (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    name_arabic VARCHAR(100) NOT NULL,
    abbreviation VARCHAR(30),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_units_company_name UNIQUE (company_id, name_arabic)
);

CREATE TABLE suppliers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    name VARCHAR(200) NOT NULL,
    phone VARCHAR(50),
    contact_person VARCHAR(150),
    address TEXT,
    notes TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE warehouses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Active',
    address TEXT,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_warehouses_company_code UNIQUE (company_id, code)
);

-- NOTE (ADR-028): `restaurants` additionally carries
--   default_serving_warehouse_id UUID NOT NULL  (composite tenant FK to warehouses)
-- See docs/29 §4.1 DB-17. The supply request's warehouse is DERIVED from this column;
-- it is never supplied by the client.
CREATE TABLE restaurants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Active',
    address TEXT,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_restaurants_company_code UNIQUE (company_id, code)
);

CREATE TABLE items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    generated_code VARCHAR(50) NOT NULL,
    name_arabic VARCHAR(200) NOT NULL,
    category_id UUID NOT NULL REFERENCES categories(id),
    base_unit_id UUID NOT NULL REFERENCES units(id),
    purchase_unit_id UUID REFERENCES units(id),
    default_supplier_id UUID REFERENCES suppliers(id),
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_items_company_code UNIQUE (company_id, generated_code),
    CONSTRAINT uq_items_company_name UNIQUE (company_id, name_arabic)
);

CREATE TABLE item_unit_conversions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    item_id UUID NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    from_unit_id UUID NOT NULL REFERENCES units(id),
    to_base_unit_id UUID NOT NULL REFERENCES units(id),
    conversion_factor NUMERIC(18,6) NOT NULL CHECK (conversion_factor > 0),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_item_conversion UNIQUE (item_id, from_unit_id, to_base_unit_id)
);
```

---

### 2.3 Inventory & Operational Tables

```sql
CREATE TABLE stock_balances (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    item_id UUID NOT NULL REFERENCES items(id),
    quantity NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (quantity >= 0),
    base_unit_id UUID NOT NULL REFERENCES units(id),
    average_unit_cost NUMERIC(18,4) NOT NULL DEFAULT 0,
    -- ⚠️ SUPERSEDED (ADR-022, docs/29 DB-01): DO NOT CREATE THIS COLUMN.
    -- PostgreSQL has no auto-maintained rowversion; use the xmin system column instead.
    -- version BYTEA NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_stock_balances UNIQUE (company_id, warehouse_id, item_id)
);

CREATE TABLE stock_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    item_id UUID NOT NULL REFERENCES items(id),
    movement_type VARCHAR(50) NOT NULL,
    quantity NUMERIC(18,4) NOT NULL,
    base_quantity NUMERIC(18,4) NOT NULL,
    unit_id UUID NOT NULL REFERENCES units(id),
    reference_type VARCHAR(50) NOT NULL,
    reference_id UUID NOT NULL,
    unit_cost NUMERIC(18,4),
    total_cost NUMERIC(18,4),
    actor_user_id UUID NOT NULL REFERENCES users(id),
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE receiving_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    supplier_id UUID REFERENCES suppliers(id),
    document_number VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL, -- 'Draft', 'Submitted', 'Verified', 'Reversed'
    business_date DATE NOT NULL,
    created_by UUID NOT NULL REFERENCES users(id),
    submitted_at TIMESTAMPTZ,
    verified_by UUID REFERENCES users(id),
    verified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_rec_company_doc UNIQUE (company_id, document_number)
);

CREATE TABLE receiving_order_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    receiving_order_id UUID NOT NULL REFERENCES receiving_orders(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES items(id),
    expected_quantity NUMERIC(18,4) NOT NULL CHECK (expected_quantity > 0),
    actual_quantity NUMERIC(18,4),
    unit_id UUID NOT NULL REFERENCES units(id),
    base_quantity NUMERIC(18,4) NOT NULL,
    actual_base_quantity NUMERIC(18,4),
    unit_cost NUMERIC(18,4),
    total_cost NUMERIC(18,4),
    notes TEXT
);

CREATE TABLE supply_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    -- DERIVED by the server from restaurants.default_serving_warehouse_id (ADR-028).
    -- NEVER accepted from the client; the creation DTO has no warehouseId property.
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    document_number VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL, -- 'Draft', 'Submitted', 'PartiallyFulfilled', 'Fulfilled', 'Cancelled'
    requested_by UUID NOT NULL REFERENCES users(id),
    requested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_req_company_doc UNIQUE (company_id, document_number)
);

CREATE TABLE supply_request_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    supply_request_id UUID NOT NULL REFERENCES supply_requests(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES items(id),
    requested_quantity NUMERIC(18,4) NOT NULL CHECK (requested_quantity > 0),
    fulfilled_quantity NUMERIC(18,4) NOT NULL DEFAULT 0,
    unit_id UUID NOT NULL REFERENCES units(id),
    base_quantity NUMERIC(18,4) NOT NULL,
    notes TEXT
);

CREATE TABLE supplies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    supply_request_id UUID REFERENCES supply_requests(id),
    document_number VARCHAR(50) NOT NULL,
    -- ⚠️ SUPERSEDED (ADR-017, docs/29 DB-08). Canonical status set:
    -- 'Prepared' | 'Dispatched' | 'Confirmed' | 'ConfirmedWithDiscrepancy'
    -- | 'RejectedAtDelivery' | 'Cancelled'.   'Discrepancy' is NOT a status.
    status VARCHAR(30) NOT NULL,
    dispatched_by UUID NOT NULL REFERENCES users(id),
    dispatched_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_by UUID REFERENCES users(id),
    confirmed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_sup_company_doc UNIQUE (company_id, document_number)
);

CREATE TABLE supply_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    supply_id UUID NOT NULL REFERENCES supplies(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES items(id),
    dispatched_quantity NUMERIC(18,4) NOT NULL CHECK (dispatched_quantity > 0),
    received_quantity NUMERIC(18,4),
    unit_id UUID NOT NULL REFERENCES units(id),
    dispatched_base_quantity NUMERIC(18,4) NOT NULL,
    received_base_quantity NUMERIC(18,4),
    variance NUMERIC(18,4)
);

CREATE TABLE stock_counts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id),
    document_number VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL, -- 'Draft', 'InProgress', 'PendingApproval', 'Approved', 'Rejected'
    is_blind_count BOOLEAN NOT NULL DEFAULT FALSE,
    opened_by UUID NOT NULL REFERENCES users(id),
    approved_by UUID REFERENCES users(id),
    opened_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    approved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_cnt_company_doc UNIQUE (company_id, document_number)
);

CREATE TABLE stock_count_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    stock_count_id UUID NOT NULL REFERENCES stock_counts(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES items(id),
    system_quantity NUMERIC(18,4) NOT NULL,
    physical_quantity NUMERIC(18,4),
    variance NUMERIC(18,4),
    base_unit_id UUID NOT NULL REFERENCES units(id),
    notes TEXT
);

CREATE TABLE discrepancies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    type VARCHAR(50) NOT NULL, -- 'ReceivingVariance', 'SupplyReceiptVariance', 'StockCountVariance'
    reference_type VARCHAR(50) NOT NULL,
    reference_id UUID NOT NULL,
    warehouse_id UUID REFERENCES warehouses(id),
    restaurant_id UUID REFERENCES restaurants(id),
    item_id UUID REFERENCES items(id),
    expected_quantity NUMERIC(18,4) NOT NULL,
    actual_quantity NUMERIC(18,4) NOT NULL,
    variance NUMERIC(18,4) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Open', -- 'Open', 'Resolved', 'Investigating'
    reason TEXT,
    resolved_by UUID REFERENCES users(id),
    resolved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE consumption_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    restaurant_id UUID NOT NULL REFERENCES restaurants(id),
    item_id UUID NOT NULL REFERENCES items(id),
    quantity NUMERIC(18,4) NOT NULL CHECK (quantity > 0),
    unit_id UUID NOT NULL REFERENCES units(id),
    base_quantity NUMERIC(18,4) NOT NULL,
    consumption_date DATE NOT NULL,
    recorded_by UUID NOT NULL REFERENCES users(id),
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    actor_user_id UUID NOT NULL REFERENCES users(id),
    actor_role VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID NOT NULL,
    description_arabic TEXT NOT NULL,
    old_values JSONB,
    new_values JSONB,
    result VARCHAR(30) NOT NULL DEFAULT 'Success',
    warehouse_id UUID,
    restaurant_id UUID,
    correlation_id VARCHAR(100) NOT NULL,
    ip_address VARCHAR(45),
    user_agent TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE stored_files (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    storage_key VARCHAR(500) NOT NULL UNIQUE,
    original_file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    size_bytes BIGINT NOT NULL,
    sha256_hash VARCHAR(64) NOT NULL,
    uploaded_by UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE idempotency_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id),
    user_id UUID NOT NULL REFERENCES users(id),
    idempotency_key VARCHAR(100) NOT NULL,
    endpoint VARCHAR(200) NOT NULL,
    request_hash VARCHAR(64) NOT NULL,
    response_status_code INT NOT NULL,
    response_payload JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_idempotency_key UNIQUE (company_id, user_id, idempotency_key)
);
```
