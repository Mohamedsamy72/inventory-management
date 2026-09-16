using System.Reflection;
using Inventory.Application;
using Inventory.Domain;
using Inventory.Infrastructure;
using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Enforces the prohibited patterns of <c>docs/25-developer-rules-anti-patterns.md</c>.
/// </summary>
public sealed class ForbiddenPatternRules
{
    private static readonly Assembly[] SolutionAssemblies =
    [
        DomainAssemblyMarker.Assembly,
        ApplicationAssemblyMarker.Assembly,
        InfrastructureAssemblyMarker.Assembly,
    ];

    /// <summary>
    /// Task 1.9 — no generic repository abstraction.
    /// </summary>
    /// <remarks>
    /// <c>IGenericRepository&lt;T&gt;</c> wraps <c>DbSet&lt;T&gt;</c> without adding meaning:
    /// it hides EF Core's capabilities, adds a layer to maintain, and pushes query logic into
    /// callers. Application handlers use the DbContext directly, or a domain-specific
    /// aggregate repository with real behaviour (docs/25 section 1).
    /// </remarks>
    [Fact]
    public void No_Generic_Repository_Abstraction_May_Exist()
    {
        var offenders = new List<string>();

        foreach (Assembly assembly in SolutionAssemblies)
        {
            offenders.AddRange(
                assembly.GetTypes()
                    .Where(static type =>
                        type.IsGenericTypeDefinition &&
                        type.Name.Contains("Repository", StringComparison.Ordinal))
                    .Select(static type => type.FullName ?? type.Name));
        }

        Assert.True(
            offenders.Count == 0,
            $"Generic repository abstractions are prohibited (docs/25). Found: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// Task 1.10 — no binary floating-point type anywhere in the domain.
    /// </summary>
    /// <remarks>
    /// <c>float</c> and <c>double</c> cannot represent 0.1 exactly. In an inventory ledger that
    /// is not a rounding curiosity: quantities and costs accumulate across thousands of
    /// movements, and a balance that drifts is a balance nobody can reconcile. Every quantity,
    /// conversion factor and cost is <c>decimal</c> (docs/25 section 2.1).
    /// </remarks>
    [Fact]
    public void Domain_Must_Not_Use_Float_Or_Double()
    {
        var offenders = new List<string>();

        foreach (Type type in DomainAssemblyMarker.Assembly.GetTypes())
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            offenders.AddRange(
                type.GetFields(flags)
                    .Where(static field => IsBinaryFloatingPoint(field.FieldType))
                    .Select(field => $"{type.FullName}.{field.Name} ({field.FieldType.Name})"));

            offenders.AddRange(
                type.GetProperties(flags)
                    .Where(static property => IsBinaryFloatingPoint(property.PropertyType))
                    .Select(property => $"{type.FullName}.{property.Name} ({property.PropertyType.Name})"));

            offenders.AddRange(
                type.GetMethods(flags)
                    .Where(static method => IsBinaryFloatingPoint(method.ReturnType))
                    .Select(method => $"{type.FullName}.{method.Name}() returns {method.ReturnType.Name}"));
        }

        Assert.True(
            offenders.Count == 0,
            $"float/double are prohibited in the domain; use decimal (docs/25 section 2.1). Found: {string.Join(", ", offenders)}.");
    }

    private static bool IsBinaryFloatingPoint(Type type)
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual == typeof(float) || actual == typeof(double);
    }
}
