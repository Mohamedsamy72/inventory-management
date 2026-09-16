using System.Security.Claims;
using Inventory.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Inventory.Infrastructure.Services;

/// <summary>
/// Reads tenant/user identity from the authenticated principal's claims. Phase 3 is what
/// actually populates those claims on sign-in; until then (and for any unauthenticated request)
/// this correctly returns <see cref="Guid.Empty"/> and <c>IsAuthenticated = false</c>, which the
/// EF global query filter treats as "matches no tenant" - fail closed, never fail open.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    public const string CompanyIdClaimType = "company_id";
    public const string UserIdClaimType = "user_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid CompanyId => ReadGuidClaim(CompanyIdClaimType);

    public Guid UserId => ReadGuidClaim(UserIdClaimType);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    private Guid ReadGuidClaim(string claimType)
    {
        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        string? value = user?.FindFirstValue(claimType);
        return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
    }
}
