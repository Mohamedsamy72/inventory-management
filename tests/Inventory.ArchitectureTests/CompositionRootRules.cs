using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Task 1.8 — the Api reaches the infrastructure layer only through its composition root.
/// </summary>
/// <remarks>
/// The project reference from Api to Infrastructure exists so that <c>Program.cs</c> can call
/// <c>AddInfrastructure</c>. That is the single intended seam. If a controller or middleware
/// starts using an infrastructure type directly, the host stops being thin and the layering
/// becomes decorative — a reference that is architecturally legal in one file and not in
/// another, which no compiler will ever tell you about.
/// </remarks>
public sealed class CompositionRootRules
{
    private const string InfrastructureNamespace = "Inventory.Infrastructure";

    /// <summary>Files permitted to mention the infrastructure namespace.</summary>
    private static readonly string[] CompositionRootFiles = ["Program.cs"];

    /// <summary>
    /// Only the composition root may reference infrastructure types.
    /// </summary>
    [Fact]
    public void Only_The_Composition_Root_May_Reference_Infrastructure()
    {
        var offenders = new List<string>();

        foreach (string file in RepositoryRoot.CSharpFilesIn(Path.Combine("src", "Inventory.Api")))
        {
            string fileName = Path.GetFileName(file);

            if (CompositionRootFiles.Contains(fileName, StringComparer.Ordinal))
            {
                continue;
            }

            string contents = File.ReadAllText(file);

            if (contents.Contains(InfrastructureNamespace, StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(RepositoryRoot.Path, file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Only {string.Join(", ", CompositionRootFiles)} may reference '{InfrastructureNamespace}'. " +
            $"Offending files: {string.Join(", ", offenders)}. See docs/decision-log.md ADR-002.");
    }
}
