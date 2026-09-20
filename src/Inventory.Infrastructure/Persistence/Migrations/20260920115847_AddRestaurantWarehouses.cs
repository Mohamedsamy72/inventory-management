using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_warehouses", x => x.id);
                    table.UniqueConstraint("AK_restaurant_warehouses_company_id_id", x => new { x.company_id, x.id });
                    table.ForeignKey(
                        name: "FK_restaurant_warehouses_restaurants_company_id_restaurant_id",
                        columns: x => new { x.company_id, x.restaurant_id },
                        principalTable: "restaurants",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_warehouses_warehouses_company_id_warehouse_id",
                        columns: x => new { x.company_id, x.warehouse_id },
                        principalTable: "warehouses",
                        principalColumns: new[] { "company_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_warehouses_company_id_restaurant_id",
                table: "restaurant_warehouses",
                columns: new[] { "company_id", "restaurant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_warehouses_company_id_warehouse_id",
                table: "restaurant_warehouses",
                columns: new[] { "company_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_warehouses_warehouse",
                table: "restaurant_warehouses",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_restaurant_warehouse",
                table: "restaurant_warehouses",
                columns: new[] { "restaurant_id", "warehouse_id" },
                unique: true);

            // Preserve existing behaviour: every (restaurant, warehouse) pair already used by a
            // supply request stays allowed; Owner/Admin manage the set from here on.
            migrationBuilder.Sql(@"
                INSERT INTO restaurant_warehouses (id, company_id, restaurant_id, warehouse_id, created_at)
                SELECT gen_random_uuid(), company_id, restaurant_id, warehouse_id, now()
                FROM (SELECT DISTINCT company_id, restaurant_id, warehouse_id FROM supply_requests) pairs;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_warehouses");
        }
    }
}
