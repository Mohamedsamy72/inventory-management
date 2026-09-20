using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectStockSetCapability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "module" },
                values: new object[] { new Guid("00000000-0000-0000-0a02-000000000039"), "stock:direct_set", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "تعديل رصيد المخزون مباشرة", "stock" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { new Guid("00000000-0000-0000-0a02-000000000039"), new Guid("00000000-0000-0000-0a01-000000000001") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000039"), new Guid("00000000-0000-0000-0a01-000000000001") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0a02-000000000039"));
        }
    }
}
