using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialUniqueIndexOnActiveItemUnitConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_item_conversion",
                table: "item_unit_conversions");

            migrationBuilder.CreateIndex(
                name: "uq_item_conversion",
                table: "item_unit_conversions",
                columns: new[] { "item_id", "from_unit_id", "to_base_unit_id" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_item_conversion",
                table: "item_unit_conversions");

            migrationBuilder.CreateIndex(
                name: "uq_item_conversion",
                table: "item_unit_conversions",
                columns: new[] { "item_id", "from_unit_id", "to_base_unit_id" },
                unique: true);
        }
    }
}
