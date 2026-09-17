namespace Inventory.Application.Common;

/// <summary>
/// Task 4.10 - masks cost/valuation figures identically wherever a query projection would
/// otherwise include them (docs/15 §2). Financial visibility is Owner-only, unconditionally, for
/// every other role including a directly-granted `User` (docs/03 §5.1) - `costs:view` and
/// `valuation:view` are non-grantable, so this reduces to a single role check, but every
/// projection goes through here rather than repeating `role == Owner` inline so the rule can
/// never be applied inconsistently across handlers (docs/25 "Frontend Security Hiding" - masking
/// belongs to the query projection, never to conditional rendering downstream).
/// </summary>
public interface IFinancialProjection
{
    /// <summary>True only for the authenticated Owner of the current tenant.</summary>
    bool IsVisible { get; }

    /// <summary>Returns <paramref name="value"/> unchanged for Owner, <c>null</c> for every
    /// other role - the masking primitive every cost/valuation projection should call.</summary>
    decimal? Apply(decimal? value);
}
