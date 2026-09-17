using System.Reflection;
using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Task 4.9 (docs/09 §Phase 4, docs/25 "Untrusted Client Identity" / "Client-Selected Serving
/// Warehouse"). Every request/command DTO in <c>Inventory.Api</c> is scanned for a fixed set of
/// properties that must never be client-bindable anywhere: the tenant, the actor, and every
/// server-generated identifier. A future endpoint that declares one of these - even validated,
/// even ignored downstream - reintroduces the exact class of bug docs/25 already names, so this
/// fails the build the instant the property is declared, not when someone remembers to test it.
/// </summary>
public sealed class MassAssignmentRules
{
    private static readonly string[] ForbiddenPropertyNames =
    [
        "CompanyId", "UserId", "GeneratedCode", "DocumentNumber", "CreatedAt",
    ];

    [Fact]
    public void No_Request_Or_Command_Dto_May_Declare_A_Forbidden_Bindable_Property()
    {
        var offenders = new List<string>();

        IEnumerable<Type> dtoTypes = typeof(Program).Assembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                (type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Command", StringComparison.Ordinal)));

        foreach (Type type in dtoTypes)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (ForbiddenPropertyNames.Contains(property.Name, StringComparer.Ordinal) && property.CanRead)
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Request/Command DTOs must not declare {string.Join(", ", ForbiddenPropertyNames)} (task 4.9, docs/25). Found: {string.Join(", ", offenders)}.");
    }
}
