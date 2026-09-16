using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Task 2.28 - scans every migration source file for identifiers the product must never
/// contain: any restaurant stock/inventory/on-hand/in-transit table or column, a barcode field,
/// an English-name column, or the removed reorder-point / max-stock / waste-record features
/// (docs/24 section 1, ADR-003, ADR-007, docs/02 section 1).
/// </summary>
/// <remarks>
/// This checks generated migration text rather than the compiled model, because a prohibited
/// column added directly in a hand-edited migration (bypassing the entity configurations
/// entirely) would still reach the database - exactly the failure mode this guard exists to
/// catch (AC-29-1).
/// </remarks>
public sealed class MigrationIntegrityRules
{
    private static readonly string[] ProhibitedFragments =
    [
        "restaurant_stock",
        "restaurant_stocks",
        "restaurant_inventory",
        "restaurant_inventories",
        "restaurant_on_hand",
        "restaurant_in_transit",
        "restaurant_inventory_balance",
        "restaurant_balances",
        "branch_inventories",
        "restaurant_serving_warehouses",
        "in_transit_balance",
        "barcode",
        "name_english",
        "name_en",
        "reorder_point",
        "max_stock",
        "waste_record",
    ];

    [Fact]
    public void Migrations_Must_Not_Contain_Prohibited_Identifiers()
    {
        string[] migrationFiles = RepositoryRoot
            .CSharpFilesIn(Path.Combine("src", "Inventory.Infrastructure", "Persistence", "Migrations"))
            .Where(static file => !file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            migrationFiles.Length > 0,
            "Expected at least one migration under src/Inventory.Infrastructure/Persistence/Migrations - Phase 2 has not generated InitialCreate yet.");

        var offenders = new List<string>();

        foreach (string file in migrationFiles)
        {
            string contents = File.ReadAllText(file).ToLowerInvariant();

            foreach (string fragment in ProhibitedFragments)
            {
                if (contents.Contains(fragment, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)} contains '{fragment}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Prohibited identifiers found in migrations (docs/24 section 1, ADR-003, ADR-007): {string.Join("; ", offenders)}.");
    }
}
