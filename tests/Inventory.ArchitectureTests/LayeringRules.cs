using System.Reflection;
using Inventory.Application;
using Inventory.Domain;
using Inventory.Infrastructure;
using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency direction of ADR-002:
/// <c>Api -&gt; Application -&gt; Domain &lt;- Infrastructure</c>.
/// </summary>
/// <remarks>
/// These rules are checked against compiled assembly references rather than source text,
/// so they cannot be satisfied by a comment or defeated by a clever alias. A reference
/// either exists in the metadata or it does not.
/// </remarks>
public sealed class LayeringRules
{
    private static readonly Assembly Domain = DomainAssemblyMarker.Assembly;
    private static readonly Assembly Application = ApplicationAssemblyMarker.Assembly;
    private static readonly Assembly Infrastructure = InfrastructureAssemblyMarker.Assembly;

    /// <summary>
    /// Task 1.7 — the domain references no persistence, web or driver technology.
    /// </summary>
    /// <remarks>
    /// The domain is where business invariants live. The moment it can see a DbContext it
    /// starts being shaped by the database instead of by the business, which is precisely
    /// the drift ADR-002 exists to prevent.
    /// </remarks>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("Serilog")]
    public void Domain_Must_Not_Reference_External_Technology(string forbiddenPrefix)
    {
        string[] offenders = ReferencedAssemblyNames(Domain)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Inventory.Domain must not reference '{forbiddenPrefix}'. Found: {string.Join(", ", offenders)}. " +
            "See docs/decision-log.md ADR-002.");
    }

    /// <summary>
    /// The domain is the innermost layer: it references no other project in the solution.
    /// </summary>
    [Fact]
    public void Domain_Must_Not_Reference_Any_Other_Solution_Project()
    {
        string[] offenders = ReferencedAssemblyNames(Domain)
            .Where(name => name.StartsWith("Inventory.", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Inventory.Domain must depend on nothing. Found: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// The application layer may see the domain, and must not see infrastructure or the host.
    /// </summary>
    [Theory]
    [InlineData("Inventory.Infrastructure")]
    [InlineData("Inventory.Api")]
    public void Application_Must_Not_Reference_Outer_Layers(string forbiddenAssembly)
    {
        bool referenced = ReferencedAssemblyNames(Application)
            .Any(name => string.Equals(name, forbiddenAssembly, StringComparison.Ordinal));

        Assert.False(
            referenced,
            $"Inventory.Application must not reference {forbiddenAssembly}. Dependencies point inward (ADR-002).");
    }

    /// <summary>
    /// Infrastructure implements application abstractions; it never depends on the host.
    /// </summary>
    [Fact]
    public void Infrastructure_Must_Not_Reference_The_Api()
    {
        bool referenced = ReferencedAssemblyNames(Infrastructure)
            .Any(name => string.Equals(name, "Inventory.Api", StringComparison.Ordinal));

        Assert.False(
            referenced,
            "Inventory.Infrastructure must not reference Inventory.Api. Dependencies point inward (ADR-002).");
    }

    private static IEnumerable<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Select(name => name!);
}
