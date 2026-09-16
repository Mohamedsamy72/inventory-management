using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_arabic = table.Column<string>(type: "text", nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => new { x.id, x.created_at });
                    table.UniqueConstraint("AK_audit_logs_company_id_id_created_at", x => new { x.company_id, x.id, x.created_at });
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("AK_categories_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_categories_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timezone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => x.company_id);
                    table.ForeignKey(
                        name: "FK_company_settings_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period_key = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => new { x.company_id, x.document_type, x.period_key });
                    table.CheckConstraint("ck_document_sequences_last_value", "last_value >= 0");
                    table.ForeignKey(
                        name: "FK_document_sequences_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_person = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                    table.UniqueConstraint("AK_suppliers_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_suppliers_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    abbreviation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                    table.UniqueConstraint("AK_units_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_units_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mobile_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.UniqueConstraint("AK_users_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_users_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.UniqueConstraint("AK_warehouses_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_warehouses_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_normalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                    table.UniqueConstraint("AK_items_company_id_id", x => new { x.company_id, x.id });
                    table.UniqueConstraint("uq_items_base_unit", x => new { x.company_id, x.id, x.base_unit_id });
                    table.ForeignKey(
                        name: "FK_items_categories_company_id_category_id",
                        columns: x => new { x.company_id, x.category_id },
                        principalTable: "categories",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_items_base_unit",
                        columns: x => new { x.company_id, x.base_unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_items_default_supplier",
                        columns: x => new { x.company_id, x.default_supplier_id },
                        principalTable: "suppliers",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_items_purchase_unit",
                        columns: x => new { x.company_id, x.purchase_unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                    table.UniqueConstraint("AK_idempotency_records_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_idempotency_records_users_company_id_user_id",
                        columns: x => new { x.company_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_otps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    otp_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_otps", x => x.id);
                    table.UniqueConstraint("AK_password_reset_otps_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_password_reset_otps_users_company_id_user_id",
                        columns: x => new { x.company_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => new { x.user_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_user_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receiving_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversal_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_orders", x => x.id);
                    table.UniqueConstraint("AK_receiving_orders_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_receiving_orders_suppliers_company_id_supplier_id",
                        columns: x => new { x.company_id, x.supplier_id },
                        principalTable: "suppliers",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_orders_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rec_created_by",
                        columns: x => new { x.company_id, x.created_by },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rec_reversed_by",
                        columns: x => new { x.company_id, x.reversed_by },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rec_verified_by",
                        columns: x => new { x.company_id, x.verified_by },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restaurants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_arabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    default_serving_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurants", x => x.id);
                    table.UniqueConstraint("AK_restaurants_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_restaurants_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_restaurants_serving_warehouse",
                        columns: x => new { x.company_id, x.default_serving_warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_counts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_blind_count = table.Column<bool>(type: "boolean", nullable: false),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_counts", x => x.id);
                    table.UniqueConstraint("AK_stock_counts_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_stock_counts_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_warehouse_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_warehouse_scopes", x => x.id);
                    table.UniqueConstraint("AK_user_warehouse_scopes_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_warehouse_scopes_users_company_id_user_id",
                        columns: x => new { x.company_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_warehouse_scopes_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_unit_conversions", x => x.id);
                    table.UniqueConstraint("AK_item_unit_conversions_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "fk_conversion_from_unit",
                        columns: x => new { x.company_id, x.from_unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversion_item_base_unit",
                        columns: x => new { x.company_id, x.item_id, x.to_base_unit_id },
                        principalTable: "items",
                        principalColumns: new[] { "company_id", "id", "base_unit_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    average_unit_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => x.id);
                    table.UniqueConstraint("AK_stock_balances_company_id_id", x => new { x.company_id, x.id });
                    table.CheckConstraint("ck_stock_balances_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_stock_balances_items_company_id_item_id",
                        columns: x => new { x.company_id, x.item_id },
                        principalTable: "items",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balances_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balances_base_unit",
                        columns: x => new { x.company_id, x.base_unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_ledger", x => x.id);
                    table.UniqueConstraint("AK_stock_ledger_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_stock_ledger_items_company_id_item_id",
                        columns: x => new { x.company_id, x.item_id },
                        principalTable: "items",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_ledger_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ledger_actor",
                        columns: x => new { x.company_id, x.actor_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ledger_unit",
                        columns: x => new { x.company_id, x.unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receiving_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    actual_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    actual_base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_order_items", x => x.id);
                    table.CheckConstraint("ck_roi_base", "base_quantity > 0");
                    table.ForeignKey(
                        name: "FK_receiving_order_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_order_items_receiving_orders_receiving_order_id",
                        column: x => x.receiving_order_id,
                        principalTable: "receiving_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roi_unit",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consumption_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    consumption_date = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumption_records", x => x.id);
                    table.UniqueConstraint("AK_consumption_records_company_id_id", x => new { x.company_id, x.id });
                    table.CheckConstraint("ck_cons_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_consumption_records_restaurants_company_id_restaurant_id",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cons_item",
                        columns: x => new { x.company_id, x.item_id },
                        principalTable: "items",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discrepancies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    actual_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    variance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discrepancies", x => x.id);
                    table.UniqueConstraint("AK_discrepancies_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "fk_disc_item",
                        columns: x => new { x.company_id, x.item_id },
                        principalTable: "items",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disc_restaurant",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disc_warehouse",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supply_requests", x => x.id);
                    table.UniqueConstraint("AK_supply_requests_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_supply_requests_restaurants_company_id_restaurant_id",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_req_requested_by",
                        columns: x => new { x.company_id, x.requested_by },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_req_warehouse",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_restaurant_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_restaurant_scopes", x => x.id);
                    table.UniqueConstraint("AK_user_restaurant_scopes_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_restaurant_scopes_restaurants_company_id_restaurant_id",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_restaurant_scopes_users_company_id_user_id",
                        columns: x => new { x.company_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_count_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    physical_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    variance = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_count_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_count_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_count_items_stock_counts_stock_count_id",
                        column: x => x.stock_count_id,
                        principalTable: "stock_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sci_base_unit",
                        column: x => x.base_unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    prepared_by = table.Column<Guid>(type: "uuid", nullable: true),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatched_by = table.Column<Guid>(type: "uuid", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplies", x => x.id);
                    table.UniqueConstraint("AK_supplies_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplies_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplies_request",
                        columns: x => new { x.company_id, x.supply_request_id },
                        principalTable: "supply_requests",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplies_restaurant",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supply_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    fulfilled_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supply_request_items", x => x.id);
                    table.CheckConstraint("ck_sri_fulfilled", "fulfilled_quantity >= 0 AND fulfilled_quantity <= requested_quantity");
                    table.ForeignKey(
                        name: "FK_supply_request_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supply_request_items_supply_requests_supply_request_id",
                        column: x => x.supply_request_id,
                        principalTable: "supply_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sri_unit",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supply_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_request_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatched_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    received_base_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    variance = table.Column<decimal>(type: "numeric(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supply_items", x => x.id);
                    table.CheckConstraint("ck_si_received", "received_quantity IS NULL OR (received_quantity >= 0 AND received_quantity <= dispatched_quantity)");
                    table.ForeignKey(
                        name: "FK_supply_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supply_items_supplies_supply_id",
                        column: x => x.supply_id,
                        principalTable: "supplies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supply_items_supply_request_items_supply_request_item_id",
                        column: x => x.supply_request_item_id,
                        principalTable: "supply_request_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_si_unit",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "module" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0a02-000000000001"), "items:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض الأصناف", "items" },
                    { new Guid("00000000-0000-0000-0a02-000000000002"), "items:create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إضافة صنف", "items" },
                    { new Guid("00000000-0000-0000-0a02-000000000003"), "items:update", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تعديل صنف", "items" },
                    { new Guid("00000000-0000-0000-0a02-000000000004"), "items:delete", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تعطيل صنف", "items" },
                    { new Guid("00000000-0000-0000-0a02-000000000005"), "categories:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة الأقسام", "master_data" },
                    { new Guid("00000000-0000-0000-0a02-000000000006"), "units:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة الوحدات", "master_data" },
                    { new Guid("00000000-0000-0000-0a02-000000000007"), "suppliers:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة الموردين", "master_data" },
                    { new Guid("00000000-0000-0000-0a02-000000000008"), "conversions:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة التحويلات", "master_data" },
                    { new Guid("00000000-0000-0000-0a02-000000000009"), "warehouses:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة المستودعات", "warehouses_branches" },
                    { new Guid("00000000-0000-0000-0a02-000000000010"), "restaurants:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة الفروع", "warehouses_branches" },
                    { new Guid("00000000-0000-0000-0a02-000000000011"), "supply_requests:create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إنشاء طلب توريد", "supply_requests" },
                    { new Guid("00000000-0000-0000-0a02-000000000012"), "supply_requests:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض طلبات التوريد", "supply_requests" },
                    { new Guid("00000000-0000-0000-0a02-000000000013"), "supply_requests:fulfill", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تلبية وشحن الطلب", "supply_requests" },
                    { new Guid("00000000-0000-0000-0a02-000000000014"), "supplies:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض التوريدات", "supplies" },
                    { new Guid("00000000-0000-0000-0a02-000000000015"), "supplies:dispatch", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إرسال الشحنة", "supplies" },
                    { new Guid("00000000-0000-0000-0a02-000000000016"), "supplies:confirm", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تأكيد استلام الفرع", "supplies" },
                    { new Guid("00000000-0000-0000-0a02-000000000017"), "receiving:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض الاستلام الوارد", "receiving" },
                    { new Guid("00000000-0000-0000-0a02-000000000018"), "receiving:create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إنشاء استلام", "receiving" },
                    { new Guid("00000000-0000-0000-0a02-000000000019"), "receiving:submit", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ترحيل استلام", "receiving" },
                    { new Guid("00000000-0000-0000-0a02-000000000020"), "receiving:verify", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "مطابقة استلام", "receiving" },
                    { new Guid("00000000-0000-0000-0a02-000000000021"), "receiving:reverse", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عكس استلام", "receiving" },
                    { new Guid("00000000-0000-0000-0a02-000000000022"), "stock_counts:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض الجرد", "stock_counts" },
                    { new Guid("00000000-0000-0000-0a02-000000000023"), "stock_counts:create", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "بدء جرد جديد", "stock_counts" },
                    { new Guid("00000000-0000-0000-0a02-000000000024"), "stock_counts:count", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تسجيل الكميات", "stock_counts" },
                    { new Guid("00000000-0000-0000-0a02-000000000025"), "stock_counts:approve", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "اعتماد الجرد والتسوية", "stock_counts" },
                    { new Guid("00000000-0000-0000-0a02-000000000026"), "costs:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض التكاليف والأسعار", "financials" },
                    { new Guid("00000000-0000-0000-0a02-000000000027"), "valuation:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض تقييم المخزون", "financials" },
                    { new Guid("00000000-0000-0000-0a02-000000000028"), "audit:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض سجل التدقيق", "audit" },
                    { new Guid("00000000-0000-0000-0a02-000000000029"), "audit:export", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تصدير سجل التدقيق", "audit" },
                    { new Guid("00000000-0000-0000-0a02-000000000030"), "users:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض المستخدمين", "users" },
                    { new Guid("00000000-0000-0000-0a02-000000000031"), "users:manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "إدارة المستخدمين", "users" },
                    { new Guid("00000000-0000-0000-0a02-000000000032"), "users:scope", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تعيين النطاقات", "users" }
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "created_at", "name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0a01-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Owner" },
                    { new Guid("00000000-0000-0000-0a01-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Admin" },
                    { new Guid("00000000-0000-0000-0a01-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "WarehouseStaff" },
                    { new Guid("00000000-0000-0000-0a01-000000000004"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "RestaurantSupervisor" },
                    { new Guid("00000000-0000-0000-0a01-000000000005"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "User" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0a02-000000000001"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000002"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000003"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000004"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000005"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000006"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000007"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000008"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000009"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000010"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000011"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000012"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000013"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000014"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000015"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000016"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000017"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000018"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000019"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000020"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000021"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000022"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000023"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000024"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000025"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000026"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000027"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000028"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000029"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000030"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000031"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000032"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000001"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000002"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000003"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000004"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000005"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000006"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000007"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000008"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000009"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000010"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000011"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000012"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000013"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000014"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000015"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000016"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000017"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000018"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000019"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000020"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000021"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000022"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000023"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000024"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000025"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000030"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000031"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000032"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000001"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000012"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000013"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000014"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000015"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000017"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000018"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000019"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000020"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000022"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000024"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000001"), new Guid("00000000-0000-0000-0a01-000000000004") },
                    { new Guid("00000000-0000-0000-0a02-000000000011"), new Guid("00000000-0000-0000-0a01-000000000004") },
                    { new Guid("00000000-0000-0000-0a02-000000000012"), new Guid("00000000-0000-0000-0a01-000000000004") },
                    { new Guid("00000000-0000-0000-0a02-000000000014"), new Guid("00000000-0000-0000-0a01-000000000004") },
                    { new Guid("00000000-0000-0000-0a02-000000000016"), new Guid("00000000-0000-0000-0a01-000000000004") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_actor",
                table: "audit_logs",
                columns: new[] { "company_id", "actor_user_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "audit_logs",
                columns: new[] { "company_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_tenant_time",
                table: "audit_logs",
                columns: new[] { "company_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_categories_tenant_keyset",
                table: "categories",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_categories_company_name",
                table: "categories",
                columns: new[] { "company_id", "name_arabic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_code",
                table: "companies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cons_item_date",
                table: "consumption_records",
                columns: new[] { "company_id", "item_id", "consumption_date" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cons_rest_date",
                table: "consumption_records",
                columns: new[] { "company_id", "restaurant_id", "consumption_date" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_consumption_records_tenant_keyset",
                table: "consumption_records",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_disc_reference",
                table: "discrepancies",
                columns: new[] { "company_id", "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_disc_status",
                table: "discrepancies",
                columns: new[] { "company_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_discrepancies_company_id_item_id",
                table: "discrepancies",
                columns: new[] { "company_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_discrepancies_company_id_restaurant_id",
                table: "discrepancies",
                columns: new[] { "company_id", "restaurant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_discrepancies_company_id_warehouse_id",
                table: "discrepancies",
                columns: new[] { "company_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_discrepancies_tenant_keyset",
                table: "discrepancies",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_dsc_company_doc",
                table: "discrepancies",
                columns: new[] { "company_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idem_expiry",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "uq_idempotency_key",
                table: "idempotency_records",
                columns: new[] { "company_id", "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conv_item",
                table: "item_unit_conversions",
                columns: new[] { "company_id", "item_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_item_unit_conversions_company_id_from_unit_id",
                table: "item_unit_conversions",
                columns: new[] { "company_id", "from_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_unit_conversions_company_id_item_id_to_base_unit_id",
                table: "item_unit_conversions",
                columns: new[] { "company_id", "item_id", "to_base_unit_id" });

            migrationBuilder.CreateIndex(
                name: "uq_item_conversion",
                table: "item_unit_conversions",
                columns: new[] { "item_id", "from_unit_id", "to_base_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_active_cat",
                table: "items",
                columns: new[] { "company_id", "is_active", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id_base_unit_id",
                table: "items",
                columns: new[] { "company_id", "base_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id_category_id",
                table: "items",
                columns: new[] { "company_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id_default_supplier_id",
                table: "items",
                columns: new[] { "company_id", "default_supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id_purchase_unit_id",
                table: "items",
                columns: new[] { "company_id", "purchase_unit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_items_name_trgm",
                table: "items",
                column: "name_normalized")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_items_tenant_keyset",
                table: "items",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_items_company_code",
                table: "items",
                columns: new[] { "company_id", "generated_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_items_company_name",
                table: "items",
                columns: new[] { "company_id", "name_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_user_active",
                table: "password_reset_otps",
                columns: new[] { "user_id", "expires_at" },
                descending: new[] { false, true },
                filter: "consumed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_otps_company_id_user_id",
                table: "password_reset_otps",
                columns: new[] { "company_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_order_items_item_id",
                table: "receiving_order_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_order_items_unit_id",
                table: "receiving_order_items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_roi_item",
                table: "receiving_order_items",
                columns: new[] { "receiving_order_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rec_wh_status",
                table: "receiving_orders",
                columns: new[] { "company_id", "warehouse_id", "status", "business_date" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_orders_company_id_created_by",
                table: "receiving_orders",
                columns: new[] { "company_id", "created_by" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_orders_company_id_reversed_by",
                table: "receiving_orders",
                columns: new[] { "company_id", "reversed_by" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_orders_company_id_supplier_id",
                table: "receiving_orders",
                columns: new[] { "company_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_orders_company_id_verified_by",
                table: "receiving_orders",
                columns: new[] { "company_id", "verified_by" });

            migrationBuilder.CreateIndex(
                name: "ix_receiving_orders_tenant_keyset",
                table: "receiving_orders",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_rec_company_doc",
                table: "receiving_orders",
                columns: new[] { "company_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_serving_wh",
                table: "restaurants",
                columns: new[] { "company_id", "default_serving_warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_tenant_keyset",
                table: "restaurants",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_restaurants_company_code",
                table: "restaurants",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_company_id_base_unit_id",
                table: "stock_balances",
                columns: new[] { "company_id", "base_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_company_id_item_id",
                table: "stock_balances",
                columns: new[] { "company_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_balances",
                table: "stock_balances",
                columns: new[] { "company_id", "warehouse_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_items_base_unit_id",
                table: "stock_count_items",
                column: "base_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_items_item_id",
                table: "stock_count_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_sci_item",
                table: "stock_count_items",
                columns: new[] { "stock_count_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cnt_wh_status",
                table: "stock_counts",
                columns: new[] { "company_id", "warehouse_id", "status", "opened_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_stock_counts_tenant_keyset",
                table: "stock_counts",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_cnt_company_doc",
                table: "stock_counts",
                columns: new[] { "company_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reference",
                table: "stock_ledger",
                columns: new[] { "company_id", "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_type_time",
                table: "stock_ledger",
                columns: new[] { "company_id", "movement_type", "occurred_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_wh_item_time",
                table: "stock_ledger",
                columns: new[] { "company_id", "warehouse_id", "item_id", "occurred_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_company_id_actor_user_id",
                table: "stock_ledger",
                columns: new[] { "company_id", "actor_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_company_id_item_id",
                table: "stock_ledger",
                columns: new[] { "company_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_company_id_unit_id",
                table: "stock_ledger",
                columns: new[] { "company_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "uq_ledger_posting",
                table: "stock_ledger",
                columns: new[] { "company_id", "reference_type", "reference_id", "item_id", "movement_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_tenant_keyset",
                table: "suppliers",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_sup_rest_status",
                table: "supplies",
                columns: new[] { "company_id", "restaurant_id", "status", "dispatched_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_supplies_company_id_supply_request_id",
                table: "supplies",
                columns: new[] { "company_id", "supply_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplies_tenant_keyset",
                table: "supplies",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_supplies_wh_status",
                table: "supplies",
                columns: new[] { "company_id", "warehouse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sup_company_doc",
                table: "supplies",
                columns: new[] { "company_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supply_items_item_id",
                table: "supply_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_supply_items_supply_request_item_id",
                table: "supply_items",
                column: "supply_request_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_supply_items_unit_id",
                table: "supply_items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_si_item",
                table: "supply_items",
                columns: new[] { "supply_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supply_request_items_item_id",
                table: "supply_request_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_supply_request_items_unit_id",
                table: "supply_request_items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_sri_item",
                table: "supply_request_items",
                columns: new[] { "supply_request_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reqs_rest_status",
                table: "supply_requests",
                columns: new[] { "company_id", "restaurant_id", "status", "requested_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_reqs_wh_status",
                table: "supply_requests",
                columns: new[] { "company_id", "warehouse_id", "status", "requested_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_supply_requests_company_id_requested_by",
                table: "supply_requests",
                columns: new[] { "company_id", "requested_by" });

            migrationBuilder.CreateIndex(
                name: "ix_supply_requests_tenant_keyset",
                table: "supply_requests",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_req_company_doc",
                table: "supply_requests",
                columns: new[] { "company_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_units_tenant_keyset",
                table: "units",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_units_company_name",
                table: "units",
                columns: new[] { "company_id", "name_arabic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_permission_id",
                table: "user_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_urs_user",
                table: "user_restaurant_scopes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_restaurant_scopes_company_id_restaurant_id",
                table: "user_restaurant_scopes",
                columns: new[] { "company_id", "restaurant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_restaurant_scopes_company_id_user_id",
                table: "user_restaurant_scopes",
                columns: new[] { "company_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "uq_user_restaurant",
                table: "user_restaurant_scopes",
                columns: new[] { "user_id", "restaurant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_single_role",
                table: "user_roles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_warehouse_scopes_company_id_user_id",
                table: "user_warehouse_scopes",
                columns: new[] { "company_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_warehouse_scopes_company_id_warehouse_id",
                table: "user_warehouse_scopes",
                columns: new[] { "company_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_uws_user",
                table: "user_warehouse_scopes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_warehouse",
                table: "user_warehouse_scopes",
                columns: new[] { "user_id", "warehouse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_keyset",
                table: "users",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_users_company_mobile",
                table: "users",
                columns: new[] { "company_id", "mobile_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_tenant_keyset",
                table: "warehouses",
                columns: new[] { "company_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_warehouses_company_code",
                table: "warehouses",
                columns: new[] { "company_id", "code" },
                unique: true);

            // ---------------------------------------------------------------------------
            // Hand-added below this line (docs/29 section 4.4-4.5) - EF's fluent API has no
            // representation for triggers or partitioning. A raising trigger is used, not
            // `DO INSTEAD NOTHING`, per docs/29's own recommendation: a silent no-op would hide
            // the defect instead of failing a test. NOTE: the companion `REVOKE UPDATE, DELETE,
            // TRUNCATE ... FROM app_user` from docs/29 section 4.4 is deliberately NOT included
            // here - no least-privilege "app_user" database role exists yet (local dev connects
            // as the `postgres` superuser, which no REVOKE can restrict anyway), and creating
            // one is a deployment/ops concern (docs/20) out of Phase 2's scope. Flagged in
            // docs/27-implementation-status.md rather than silently invented or silently
            // skipped. The trigger alone still satisfies AC-29-3 ("fails loudly") for every
            // caller, including a superuser.
            // ---------------------------------------------------------------------------
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION reject_append_only_mutation() RETURNS trigger AS $BODY$
                BEGIN
                    RAISE EXCEPTION 'Table % is append-only; % is not permitted (docs/29 section 4.4).', TG_TABLE_NAME, TG_OP
                        USING ERRCODE = '55006';
                    RETURN NULL;
                END;
                $BODY$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_stock_ledger_append_only
                    BEFORE UPDATE OR DELETE ON stock_ledger
                    FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                """);

            // docs/29 section 4.5: audit_logs is declared PARTITION BY RANGE (created_at) with
            // the current and next month's partitions pre-created; a missing partition must be a
            // hard failure, never a silent drop, so no catch-all default partition is created.
            // EF's CreateTable above has already created audit_logs as an ordinary table - it
            // cannot express partitioning, so it is converted here: renamed aside, recreated as
            // the partitioned parent (its PK/alternate key already include created_at, the
            // partition key, satisfying PostgreSQL's requirement that every unique constraint on
            // a partitioned table include the partition column), and the (empty, since this is
            // the initial migration) data moved back, in one transaction with everything above.
            migrationBuilder.Sql(
                """
                ALTER TABLE audit_logs RENAME TO audit_logs_unpartitioned;

                CREATE TABLE audit_logs (
                    LIKE audit_logs_unpartitioned INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES
                ) PARTITION BY RANGE (created_at);

                INSERT INTO audit_logs SELECT * FROM audit_logs_unpartitioned;

                DROP TABLE audit_logs_unpartitioned;

                CREATE TRIGGER trg_audit_logs_append_only
                    BEFORE UPDATE OR DELETE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                """);

            // `LIKE ... INCLUDING INDEXES` recreates the indexes but does NOT preserve their
            // original names - PostgreSQL assigns its own default-pattern names to the copies.
            // Renamed back to the exact names docs/29 section 5.5 specifies, matching every
            // other table's indexes.
            migrationBuilder.Sql(
                """
                ALTER INDEX audit_logs_company_id_created_at_idx RENAME TO ix_audit_tenant_time;
                ALTER INDEX audit_logs_company_id_actor_user_id_created_at_idx RENAME TO ix_audit_actor;
                ALTER INDEX audit_logs_company_id_entity_type_entity_id_idx RENAME TO ix_audit_entity;
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE audit_logs_2026_09 PARTITION OF audit_logs
                    FOR VALUES FROM ('2026-09-01T00:00:00Z') TO ('2026-10-01T00:00:00Z');
                CREATE TABLE audit_logs_2026_10 PARTITION OF audit_logs
                    FOR VALUES FROM ('2026-10-01T00:00:00Z') TO ('2026-11-01T00:00:00Z');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_stock_ledger_append_only ON stock_ledger;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON audit_logs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_append_only_mutation();");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "company_settings");

            migrationBuilder.DropTable(
                name: "consumption_records");

            migrationBuilder.DropTable(
                name: "discrepancies");

            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "item_unit_conversions");

            migrationBuilder.DropTable(
                name: "password_reset_otps");

            migrationBuilder.DropTable(
                name: "receiving_order_items");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropTable(
                name: "stock_count_items");

            migrationBuilder.DropTable(
                name: "stock_ledger");

            migrationBuilder.DropTable(
                name: "supply_items");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "user_restaurant_scopes");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_warehouse_scopes");

            migrationBuilder.DropTable(
                name: "receiving_orders");

            migrationBuilder.DropTable(
                name: "stock_counts");

            migrationBuilder.DropTable(
                name: "supplies");

            migrationBuilder.DropTable(
                name: "supply_request_items");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "supply_requests");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "restaurants");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
