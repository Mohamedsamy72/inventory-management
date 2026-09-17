using Inventory.Application.Common;
using Inventory.Domain.Enums;

namespace Inventory.Infrastructure.Services;

/// <summary>See <see cref="IFinancialProjection"/>.</summary>
public sealed class FinancialProjection : IFinancialProjection
{
    private readonly ICurrentUserService _currentUserService;

    public FinancialProjection(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public bool IsVisible => _currentUserService.Role == RoleName.Owner;

    public decimal? Apply(decimal? value) => IsVisible ? value : null;
}
