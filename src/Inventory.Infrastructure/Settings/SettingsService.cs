using Inventory.Application.Common;
using Inventory.Application.Settings;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Settings;

/// <summary>See <see cref="ISettingsService"/>. <c>Company</c>/<c>CompanySettings</c> are not
/// tenant-filtered entities, so every query here is explicitly keyed on the caller's own
/// <see cref="ICurrentUserService.CompanyId"/> - never a client-supplied id.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public SettingsService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<CompanySettingsSummary> GetAsync(CancellationToken cancellationToken)
    {
        Guid companyId = _currentUserService.CompanyId;
        Company company = await _context.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, cancellationToken);
        CompanySettings? settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        settings ??= new CompanySettings(companyId);
        return ToSummary(company, settings);
    }

    public async Task<CompanySettingsSummary?> UpdateTimezoneAsync(string timezoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(timezoneId) || !IsValidTimeZone(timezoneId))
        {
            return null;
        }

        Guid companyId = _currentUserService.CompanyId;
        Company company = await _context.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, cancellationToken);
        CompanySettings? settings = await _context.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (settings is null)
        {
            settings = new CompanySettings(companyId);
            _context.CompanySettings.Add(settings);
        }

        string oldTimezone = settings.TimezoneId;
        settings.ChangeTimezone(timezoneId);

        _auditLogger.Record(new AuditEntry(
            "COMPANY_SETTINGS_UPDATED", nameof(CompanySettings), companyId,
            "تم تعديل إعدادات المنشأة (المنطقة الزمنية)",
            OldValues: new { TimezoneId = oldTimezone }, NewValues: new { TimezoneId = timezoneId }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return ToSummary(company, settings);
    }

    private static bool IsValidTimeZone(string id)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static CompanySettingsSummary ToSummary(Company company, CompanySettings settings) =>
        new(company.Name, company.Code, settings.TimezoneId, settings.CurrencyCode, settings.UpdatedAt);
}
