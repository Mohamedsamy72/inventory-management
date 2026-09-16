namespace Inventory.Domain;

/// <summary>
/// Stable type used by the architecture tests to locate this assembly without
/// loading it by string name. Contains no behaviour by design.
/// </summary>
public static class DomainAssemblyMarker
{
    /// <summary>The <see cref="System.Reflection.Assembly"/> of the domain layer.</summary>
    public static readonly System.Reflection.Assembly Assembly =
        typeof(DomainAssemblyMarker).Assembly;
}
