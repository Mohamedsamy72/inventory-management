namespace Inventory.Infrastructure;

/// <summary>
/// Stable type used by the architecture tests to locate this assembly.
/// Contains no behaviour by design.
/// </summary>
public static class InfrastructureAssemblyMarker
{
    /// <summary>The <see cref="System.Reflection.Assembly"/> of the infrastructure layer.</summary>
    public static readonly System.Reflection.Assembly Assembly =
        typeof(InfrastructureAssemblyMarker).Assembly;
}
