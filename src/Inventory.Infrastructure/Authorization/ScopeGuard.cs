using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Authorization;

/// <summary>See <see cref="IScopeGuard"/>.</summary>
public sealed class ScopeGuard : IScopeGuard
{
    private readonly InventoryDbContext _context;

    public ScopeGuard(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlySet<Guid>> GetAuthorizedWarehouseIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<Guid> ids = await _context.UserWarehouseScopes
            .Where(s => s.UserId == userId)
            .Select(s => s.WarehouseId)
            .ToListAsync(cancellationToken);

        return new HashSet<Guid>(ids);
    }

    public async Task<IReadOnlySet<Guid>> GetAuthorizedRestaurantIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<Guid> ids = await _context.UserRestaurantScopes
            .Where(s => s.UserId == userId)
            .Select(s => s.RestaurantId)
            .ToListAsync(cancellationToken);

        return new HashSet<Guid>(ids);
    }
}
