using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Task 7.13 - <c>IStockPostingService</c> is the ONLY path that writes <c>stock_ledger</c> or
/// <c>stock_balances</c>. Text-scanned (like <see cref="CompositionRootRules"/>), not reflected
/// over compiled metadata: the thing being forbidden is a specific method CALL
/// (<c>StockLedgerEntries.Add</c>, <c>StockBalances.Add</c>, <c>SetQuantityAndCost</c>), which
/// has no distinct compiled shape to detect - only its source text does.
/// </summary>
public sealed class StockPostingRules
{
    /// <summary>The one file allowed to contain these calls.</summary>
    private const string StockPostingServiceFileName = "StockPostingService.cs";

    private static readonly string[] ForbiddenPatterns =
    [
        "StockLedgerEntries.Add(",
        "StockBalances.Add(",
        ".SetQuantityAndCost(",
    ];

    [Fact]
    public void Only_StockPostingService_May_Write_StockLedger_Or_StockBalances()
    {
        var offenders = new List<string>();

        foreach (string file in RepositoryRoot.CSharpFilesIn(Path.Combine("src")))
        {
            if (Path.GetFileName(file) == StockPostingServiceFileName)
            {
                continue;
            }

            string contents = File.ReadAllText(file);

            foreach (string pattern in ForbiddenPatterns)
            {
                if (contents.Contains(pattern, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryRoot.Path, file)} ({pattern})");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Only StockPostingService.cs may write stock_ledger/stock_balances (task 7.13). Offenders: {string.Join(", ", offenders)}.");
    }
}
