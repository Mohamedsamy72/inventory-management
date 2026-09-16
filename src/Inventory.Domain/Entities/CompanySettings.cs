namespace Inventory.Domain.Entities;

/// <summary>
/// One row per tenant, keyed on <see cref="CompanyId"/> itself (one-to-one with
/// <see cref="Company"/>). Document-numbering periods are derived from
/// <see cref="TimezoneId"/>, not UTC (ADR-024) - a late-evening Cairo document must land in the
/// correct month.
/// </summary>
public sealed class CompanySettings
{
    private CompanySettings()
    {
    }

    public CompanySettings(Guid companyId)
    {
        CompanyId = companyId;
        TimezoneId = "Africa/Cairo";
        CurrencyCode = "EGP";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public string TimezoneId { get; private set; } = "Africa/Cairo";
    public string CurrencyCode { get; private set; } = "EGP";
    public DateTimeOffset UpdatedAt { get; private set; }

    public void ChangeTimezone(string timezoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timezoneId);
        TimezoneId = timezoneId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
