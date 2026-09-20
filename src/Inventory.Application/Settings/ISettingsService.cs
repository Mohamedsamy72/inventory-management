namespace Inventory.Application.Settings;

/// <summary>Change 5 - the tenant settings that actually exist (`company_settings`, docs/29 §4.2):
/// timezone (drives document-numbering periods, ADR-024) and currency. Company name/code are
/// shown read-only. Only the timezone is editable; currency has no setter by design.</summary>
public interface ISettingsService
{
    Task<CompanySettingsSummary> GetAsync(CancellationToken cancellationToken);

    /// <summary>Returns null when <paramref name="timezoneId"/> is not a valid time-zone id.</summary>
    Task<CompanySettingsSummary?> UpdateTimezoneAsync(string timezoneId, CancellationToken cancellationToken);
}

public sealed record CompanySettingsSummary(string CompanyName, string CompanyCode, string TimezoneId, string CurrencyCode, DateTimeOffset UpdatedAt);
