using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.10 - items CRUD. <c>generatedCode</c> is never accepted from a client (docs/28
/// §5.1) - allocated here, inside the same transaction as the insert, via
/// <see cref="IDocumentSequenceService"/>.</summary>
public interface IItemService
{
    Task<MasterDataResult<ItemSummary>> CreateAsync(CreateItemCommand command, CancellationToken cancellationToken);

    Task<ItemSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<ItemSummary>> ListAsync(int limit, string? cursor, string? searchQuery, CancellationToken cancellationToken);

    Task<MasterDataResult<ItemSummary>> UpdateAsync(Guid id, UpdateItemCommand command, CancellationToken cancellationToken);

    /// <summary>ADR-023: fails with <see cref="MasterDataError.BaseUnitImmutable"/> once any
    /// `stock_ledger` row exists for this item.</summary>
    Task<MasterDataResult<ItemSummary>> ChangeBaseUnitAsync(Guid id, Guid newBaseUnitId, CancellationToken cancellationToken);

    /// <summary>Hard delete, ONLY for a record never referenced by any transaction; otherwise `MasterDataError.InUse` and nothing changes (deactivate instead).</summary>
    Task<MasterDataResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<ItemSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<ItemSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
