using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRestaurantServingWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_restaurants_serving_warehouse",
                table: "restaurants");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_serving_wh",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "default_serving_warehouse_id",
                table: "restaurants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_serving_warehouse_id",
                table: "restaurants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_serving_wh",
                table: "restaurants",
                columns: new[] { "company_id", "default_serving_warehouse_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_restaurants_serving_warehouse",
                table: "restaurants",
                columns: new[] { "company_id", "default_serving_warehouse_id" },
                principalTable: "warehouses",
                principalColumns: new[] { "company_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
