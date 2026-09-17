using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.6 - units CRUD with deactivate-not-delete.</summary>
public interface IUnitService
{
    Task<MasterDataResult<UnitSummary>> CreateAsync(CreateUnitCommand command, CancellationToken cancellationToken);

    Task<UnitSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<UnitSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<UnitSummary>> UpdateAsync(Guid id, UpdateUnitCommand command, CancellationToken cancellationToken);

    Task<MasterDataResult<UnitSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<UnitSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
