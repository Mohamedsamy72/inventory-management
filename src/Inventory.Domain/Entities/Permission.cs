using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>
/// One entry in the permission catalogue, e.g. <c>items:create</c>. Global reference data.
/// Four codes (<c>costs:view</c>, <c>valuation:view</c>, <c>audit:view</c>, <c>audit:export</c>)
/// are non-grantable (ADR-012) - they exist to document Owner capability and can never be
/// assigned to any user of any role.
/// </summary>
public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string code, string description, string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        Id = Guid.NewGuid();
        Code = code;
        Description = description;
        Module = module;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private static readonly HashSet<string> NonGrantableCodes = new(StringComparer.Ordinal)
    {
        "costs:view",
        "valuation:view",
        "audit:view",
        "audit:export",
    };

    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;

    /// <summary>
    /// False for exactly the four non-grantable codes (ADR-012). Computed from
    /// <see cref="Code"/> on every access - not persisted, and not set-once at construction -
    /// so it is correct both for a freshly constructed instance and for one EF materialises
    /// directly from the database (which bypasses the public constructor).
    /// </summary>
    public bool IsGrantable => !NonGrantableCodes.Contains(Code);

    public DateTimeOffset CreatedAt { get; private set; }
}
