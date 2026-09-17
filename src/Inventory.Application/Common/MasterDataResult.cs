namespace Inventory.Application.Common;

/// <summary>
/// Why a Phase 5 master-data operation did not succeed - shared across Category/Unit/Supplier/
/// Warehouse/Restaurant/Item because their failure surface is genuinely the same shape, unlike
/// Phase 4's Users domain (docs/27 §12's <c>UserManagementError</c>), which had escalation-guard
/// outcomes no other domain shares.
/// </summary>
public enum MasterDataError
{
    None,
    NotFound,
    DuplicateName,

    /// <summary>Items specifically (docs/13's pre-existing `DUPLICATE_ITEM_NAME`, distinct from
    /// the generic <see cref="DuplicateName"/> the other Phase 5 resources use).</summary>
    DuplicateItemName,

    GeneratedFieldNotAccepted,
    SequenceExhausted,
    ServingWarehouseUnavailable,
    BaseUnitImmutable,
    ConversionNotDefined,
    InvalidConversionFactor,
}

public sealed record MasterDataResult<T>(bool Succeeded, T? Value, MasterDataError Error, string? ErrorDetail = null);

/// <summary>Factory helpers for <see cref="MasterDataResult{T}"/> (CA1000: a generic type must
/// not declare static members).</summary>
public static class MasterDataResult
{
    public static MasterDataResult<T> Success<T>(T value) => new(true, value, MasterDataError.None);

    public static MasterDataResult<T> Failure<T>(MasterDataError error, string? detail = null) =>
        new(false, default, error, detail);
}
