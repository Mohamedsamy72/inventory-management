using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Authorization;

/// <summary>
/// Lets an endpoint write <c>.RequireAuthorization("items:view")</c> directly, using the
/// `&lt;module&gt;:&lt;action&gt;` permission code itself as the policy name (task 4.2), instead
/// of hand-registering one named policy per permission in <c>AddAuthorization</c> - which would
/// silently drift from docs/03 §3's catalogue the first time a code is added there and not here.
/// Falls back to the default provider for any policy name that is not a permission code (colon
/// present), so pre-existing named policies keep working unchanged.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (IsPermissionCode(policyName))
        {
            AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    /// <summary>`&lt;module&gt;:&lt;action&gt;` (docs/03 §3) - a single colon, letters/digits/
    /// underscore on both sides. Deliberately strict: a typo'd policy name should fail loudly as
    /// "unknown policy" rather than silently compile into a permission requirement nobody has.</summary>
    private static bool IsPermissionCode(string policyName)
    {
        string[] parts = policyName.Split(':');
        return parts.Length == 2
            && parts[0].Length > 0 && parts[1].Length > 0
            && parts[0].All(static c => char.IsAsciiLetterLower(c) || c == '_')
            && parts[1].All(static c => char.IsAsciiLetterLower(c) || c == '_');
    }
}
