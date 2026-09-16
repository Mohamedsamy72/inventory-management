namespace Inventory.Application;

/// <summary>
/// Stable type used by the architecture tests to locate this assembly.
/// Contains no behaviour by design.
/// </summary>
public static class ApplicationAssemblyMarker
{
    /// <summary>The <see cref="System.Reflection.Assembly"/> of the application layer.</summary>
    public static readonly System.Reflection.Assembly Assembly =
        typeof(ApplicationAssemblyMarker).Assembly;
}
