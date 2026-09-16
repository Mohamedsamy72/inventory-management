namespace Inventory.ArchitectureTests;

/// <summary>
/// Locates the repository root at test time by walking up from the test binary until the
/// solution file is found.
/// </summary>
/// <remarks>
/// Some rules are about source text and files on disk — which file may mention a namespace,
/// what a startup script is allowed to contain — and cannot be expressed against compiled
/// metadata. Those rules need the repository, not the output directory.
/// </remarks>
internal static class RepositoryRoot
{
    private const string SolutionFileName = "InventorySystem.sln";

    /// <summary>The absolute path of the repository root.</summary>
    /// <exception cref="InvalidOperationException">The solution file could not be located.</exception>
    public static string Path
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, SolutionFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate '{SolutionFileName}' above '{AppContext.BaseDirectory}'.");
        }
    }

    /// <summary>Enumerates every C# source file beneath a repository-relative directory.</summary>
    /// <param name="relativeDirectory">Directory relative to the repository root.</param>
    public static IEnumerable<string> CSharpFilesIn(string relativeDirectory)
    {
        string absolute = System.IO.Path.Combine(Path, relativeDirectory);

        if (!Directory.Exists(absolute))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(absolute, "*.cs", SearchOption.AllDirectories)
            .Where(static file =>
                !file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
