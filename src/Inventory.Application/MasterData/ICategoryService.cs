using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>Task 5.5 - categories CRUD with deactivate-not-delete (docs/04 §4: an entity
/// referenced by transactions can be deactivated but never deleted - no delete method exists
/// here at all, not merely an unexposed one).</summary>
public interface ICategoryService
{
    Task<MasterDataResult<CategorySummary>> CreateAsync(CreateCategoryCommand command, CancellationToken cancellationToken);

    Task<CategorySummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<CategorySummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    Task<MasterDataResult<CategorySummary>> UpdateAsync(Guid id, UpdateCategoryCommand command, CancellationToken cancellationToken);

    /// <summary>Hard delete, ONLY for a record never referenced by any transaction; otherwise `MasterDataError.InUse` and nothing changes (deactivate instead).</summary>
    Task<MasterDataResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<CategorySummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<MasterDataResult<CategorySummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
