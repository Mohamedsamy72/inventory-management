using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Task 1.16 guard — the local startup scripts can never destroy a database.
/// </summary>
/// <remarks>
/// <c>docs/19</c> carries this as a CRITICAL DATABASE SAFETY INVARIANT: a developer runs
/// <c>run-local.bat</c> dozens of times a day, usually without reading it. A destructive
/// reset hidden in that script is one careless afternoon away from erasing real data, and
/// the loss would be silent — the script would simply appear to work.
/// <para>
/// This test is why the invariant survives a future edit by someone who has not read the spec.
/// </para>
/// </remarks>
public sealed class StartupScriptRules
{
    private static readonly string[] ForbiddenFragments =
    [
        "DROP DATABASE",
        "DROP SCHEMA",
        "EnsureDeleted",
        "dropdb",
        "database drop",
    ];

    /// <summary>
    /// No repository batch script may contain a destructive database operation.
    /// </summary>
    [Fact]
    public void Startup_Scripts_Must_Not_Contain_Destructive_Database_Commands()
    {
        string[] scripts = Directory
            .EnumerateFiles(RepositoryRoot.Path, "*.bat", SearchOption.TopDirectoryOnly)
            .ToArray();

        Assert.True(
            scripts.Length > 0,
            $"Expected at least one .bat script at the repository root ({RepositoryRoot.Path}).");

        var offenders = new List<string>();

        foreach (string script in scripts)
        {
            string contents = File.ReadAllText(script);
            string scriptName = Path.GetFileName(script);

            foreach (string fragment in ForbiddenFragments)
            {
                // A script may *warn* about the forbidden operation in a comment; it may not
                // contain it as an executable line. REM lines are excluded before matching.
                bool present = contents
                    .Split('\n')
                    .Where(static line => !line.TrimStart().StartsWith("REM", StringComparison.OrdinalIgnoreCase))
                    .Where(static line => !line.TrimStart().StartsWith("::", StringComparison.Ordinal))
                    .Any(line => line.Contains(fragment, StringComparison.OrdinalIgnoreCase));

                if (present)
                {
                    offenders.Add($"{scriptName} contains '{fragment}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Destructive database commands are prohibited in startup scripts (docs/19, docs/25). Found: {string.Join("; ", offenders)}.");
    }
}
