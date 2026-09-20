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

    Task<MasterDataResult<RestaurantSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<RestaurantSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
