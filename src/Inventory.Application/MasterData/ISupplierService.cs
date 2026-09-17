using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.7 - suppliers CRUD with deactivate-not-delete.</summary>
public interface ISupplierService
{
    Task<MasterDataResult<SupplierSummary>> CreateAsync(CreateSupplierCommand command, CancellationToken cancellationToken);

    Task<SupplierSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<SupplierSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<SupplierSummary>> UpdateAsync(Guid id, UpdateSupplierCommand command, CancellationToken cancellationToken);

    Task<MasterDataResult<SupplierSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<SupplierSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
