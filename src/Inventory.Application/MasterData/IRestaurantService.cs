using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.9 - restaurants CRUD. A restaurant is pure master data (name/code/address) -
/// it no longer carries any warehouse reference (Change 1 reversed ADR-028's single default
/// serving warehouse; a restaurant can receive from more than one warehouse, chosen per supply
/// request instead).</summary>
public interface IRestaurantService
{
    Task<MasterDataResult<RestaurantSummary>> CreateAsync(CreateRestaurantCommand command, CancellationToken cancellationToken);

    Task<RestaurantSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<RestaurantSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> UpdateAsync(Guid id, UpdateRestaurantCommand command, CancellationToken cancellationToken);

    /// <summary>The ACTIVE warehouses explicitly allowed for this restaurant (Owner/Admin-managed
    /// mapping). Null when the restaurant does not exist in the caller's tenant.</summary>
    Task<IReadOnlyList<AllowedWarehouse>?> GetAllowedWarehousesAsync(Guid restaurantId, CancellationToken cancellationToken);

    /// <summary>Replaces the allowed set. Every id must be an Active warehouse of the caller's own
    /// company, else <see cref="MasterDataError.WarehouseUnavailable"/> and nothing changes.</summary>
    Task<MasterDataResult<IReadOnlyList<AllowedWarehouse>>> SetAllowedWarehousesAsync(Guid restaurantId, IReadOnlyList<Guid> warehouseIds, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
