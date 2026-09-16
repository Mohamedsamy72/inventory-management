using Inventory.Domain;
using Xunit;

namespace Inventory.UnitTests;

/// <summary>
/// Phase 1 placeholder-free smoke coverage for the domain assembly.
/// </summary>
/// <remarks>
/// The domain has no business types yet — entities, value objects and invariants arrive with
/// Phase 5 onward. This project exists now so that Phase 5 has somewhere to write its first
/// test instead of creating the harness under deadline pressure.
/// <para>
/// The assertions below are real, not filler: they verify that the assembly loads and that
/// the marker contract the architecture tests depend on actually holds.
/// </para>
/// </remarks>
public sealed class DomainLayerContractTests
{
    /// <summary>The domain assembly loads and identifies itself correctly.</summary>
    [Fact]
    public void Domain_Assembly_Is_Resolvable()
    {
        Assert.NotNull(DomainAssemblyMarker.Assembly);
        Assert.Equal("Inventory.Domain", DomainAssemblyMarker.Assembly.GetName().Name);
    }

    /// <summary>
    /// The marker is the anchor every architecture rule uses to find this assembly.
    /// If it stopped being part of the domain assembly, those rules would silently
    /// start inspecting the wrong thing.
    /// </summary>
    [Fact]
    public void Domain_Marker_Belongs_To_The_Domain_Assembly()
    {
        Assert.Same(typeof(DomainAssemblyMarker).Assembly, DomainAssemblyMarker.Assembly);
    }
}
