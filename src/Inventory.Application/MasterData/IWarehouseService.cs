using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.8 - warehouses CRUD with deactivate-not-delete. <c>Code</c> is client-supplied
/// at creation (unlike items/documents, docs/28 §4 does not list warehouses as a server-numbered
/// series) and immutable thereafter - no update method changes it.</summary>
public interface IWarehouseService
{
    Task<MasterDataResult<WarehouseSummary>> CreateAsync(CreateWarehouseCommand command, CancellationToken cancellationToken);

    Task<WarehouseSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<WarehouseSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<WarehouseSummary>> UpdateAsync(Guid id, UpdateWarehouseCommand command, CancellationToken cancellationToken);

    Task<MasterDataResult<WarehouseSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<WarehouseSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
