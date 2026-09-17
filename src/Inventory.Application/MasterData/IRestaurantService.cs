using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.9 - restaurants CRUD, including the required
/// <c>defaultServingWarehouseId</c> (ADR-028 SW-6): creating a restaurant whose named warehouse
/// does not exist, or is not <c>Active</c>, fails with <see cref="MasterDataError.ServingWarehouseUnavailable"/>
/// rather than silently accepting an unusable configuration a supply request would only fail on
/// much later.</summary>
public interface IRestaurantService
{
    Task<MasterDataResult<RestaurantSummary>> CreateAsync(CreateRestaurantCommand command, CancellationToken cancellationToken);

    Task<RestaurantSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<RestaurantSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> UpdateAsync(Guid id, UpdateRestaurantCommand command, CancellationToken cancellationToken);

    /// <summary>ADR-028 SW-8: changes ONLY this restaurant's configuration going forward - any
    /// supply request already created keeps pointing at the warehouse resolved at its own
    /// creation time (docs/09 task 9.8b), since that resolution is copied onto the request, not
    /// looked up live.</summary>
    Task<MasterDataResult<RestaurantSummary>> ChangeServingWarehouseAsync(Guid id, Guid warehouseId, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
