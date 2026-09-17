using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscrepancyPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "module" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0a02-000000000033"), "discrepancies:view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "عرض الفروقات", "discrepancies" },
                    { new Guid("00000000-0000-0000-0a02-000000000034"), "discrepancies:resolve", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "حل الفروقات", "discrepancies" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000034"), new Guid("00000000-0000-0000-0a01-000000000001") },
                    { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000034"), new Guid("00000000-0000-0000-0a01-000000000002") },
                    { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000003") },
                    { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000004") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000034"), new Guid("00000000-0000-0000-0a01-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000034"), new Guid("00000000-0000-0000-0a01-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0a02-000000000033"), new Guid("00000000-0000-0000-0a01-000000000004") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0a02-000000000033"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0a02-000000000034"));
        }
    }
}
