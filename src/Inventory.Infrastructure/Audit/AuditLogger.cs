using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Inventory.Infrastructure.Audit;

/// <summary>See <see cref="IAuditLogger"/>.</summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(InventoryDbContext context, ICurrentUserService currentUserService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public void Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        string correlationId = httpContext?.Items[CorrelationIdHeader.Name] as string
            ?? Guid.NewGuid().ToString("D");

        var auditLog = new AuditLog(
            _currentUserService.CompanyId,
            _currentUserService.UserId,
            _currentUserService.Role?.ToString() ?? "Unknown",
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.DescriptionArabic,
            AuditSanitizer.Sanitize(entry.OldValues),
            AuditSanitizer.Sanitize(entry.NewValues),
            entry.Result,
            entry.WarehouseId,
            entry.RestaurantId,
            correlationId,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString());

        // Task 4.11: added to the SAME context the caller's business change already lives in -
        // not saved here. Whatever SaveChangesAsync the caller runs next commits both, or (on a
        // rolled-back operation) neither, automatically.
        _context.AuditLogs.Add(auditLog);
    }
}
