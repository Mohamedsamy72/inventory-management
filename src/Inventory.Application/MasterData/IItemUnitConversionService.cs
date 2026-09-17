using Inventory.Application.Common;

namespace Inventory.Application.MasterData;

/// <summary>
/// Task 6.1, 6.5 - conversions are immutable once created; "editing" one always deactivates
/// whatever active row already exists for the same (item, from-unit) pair and inserts a new one
/// in its place, in one call - there is deliberately no separate create-vs-correct distinction,
/// since a correction and a first-time definition look identical from the caller's side and
/// both must satisfy the same invariant (at most one active conversion per item/unit pair,
/// task 6.5 / ADR-023).
/// </summary>
public interface IItemUnitConversionService
{
    Task<MasterDataResult<ItemUnitConversionSummary>> CreateAsync(CreateItemUnitConversionCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemUnitConversionSummary>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken);

    Task<MasterDataResult<ItemUnitConversionSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
