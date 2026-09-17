namespace Inventory.Domain.Common;

/// <summary>Strips every non-digit character so a mobile number is stored and compared in one
/// canonical form, e.g. "010-1234 5678" and "01012345678" are the same login identifier
/// (docs/08 section 1).</summary>
public static class MobileNumberNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
