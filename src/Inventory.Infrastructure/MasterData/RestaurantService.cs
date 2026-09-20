using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IRestaurantService"/>.</summary>
public sealed class RestaurantService : IRestaurantService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public RestaurantService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<RestaurantSummary>> CreateAsync(CreateRestaurantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var restaurant = new Restaurant(
            _currentUserService.CompanyId, command.NameArabic, command.Code, command.Address, command.Description);
        _context.Restaurants.Add(restaurant);

        _auditLogger.Record(new AuditEntry(
            "RESTAURANT_CREATED", nameof(Restaurant), restaurant.Id,
            $"تم إنشاء فرع جديد: {restaurant.NameArabic} ({restaurant.Code})",
            OldValues: null, NewValues: new { restaurant.NameArabic, restaurant.Code }, Domain.Enums.AuditResult.Success,
            RestaurantId: restaurant.Id));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<RestaurantSummary>(error)
            : MasterDataResult.Success(ToSummary(restaurant));
    }

    public async Task<RestaurantSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Restaurant? restaurant = await _context.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return restaurant is null ? null : ToSummary(restaurant);
    }

    public async Task<KeysetPage<RestaurantSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Restaurant> query = _context.Restaurants.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Restaurant> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Restaurant> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<RestaurantSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<RestaurantSummary>> UpdateAsync(Guid id, UpdateRestaurantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Restaurant? restaurant = await _context.Restaurants.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (restaurant is null)
        {
            return MasterDataResult.Failure<RestaurantSummary>(MasterDataError.NotFound);
        }

        string oldName = restaurant.NameArabic;
        restaurant.Update(command.NameArabic, command.Address, command.Description);

        _auditLogger.Record(new AuditEntry(
            "RESTAURANT_UPDATED", nameof(Restaurant), restaurant.Id,
            $"تم تعديل الفرع: {oldName} → {restaurant.NameArabic}",
            OldValues: new { NameArabic = oldName }, NewValues: new { restaurant.NameArabic }, Domain.Enums.AuditResult.Success,
            RestaurantId: restaurant.Id));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<RestaurantSummary>(error)
            : MasterDataResult.Success(ToSummary(restaurant));
    }

    public Task<MasterDataResult<RestaurantSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<RestaurantSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<RestaurantSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Restaurant? restaurant = await _context.Restaurants.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (restaurant is null)
        {
            return MasterDataResult.Failure<RestaurantSummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            restaurant.Reactivate();
        }
        else
        {
            restaurant.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "RESTAURANT_REACTIVATED" : "RESTAURANT_DEACTIVATED", nameof(Restaurant), restaurant.Id,
            active ? $"تم تفعيل الفرع: {restaurant.NameArabic}" : $"تم تعطيل الفرع: {restaurant.NameArabic}",
            OldValues: null, NewValues: new { IsActive = active }, Domain.Enums.AuditResult.Success,
            RestaurantId: restaurant.Id));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(restaurant));
    }

    private async Task<MasterDataError?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return MasterDataError.DuplicateName;
        }
    }

    private static RestaurantSummary ToSummary(Restaurant restaurant) => new(
        restaurant.Id, restaurant.NameArabic, restaurant.Code,
        restaurant.Status == RestaurantStatus.Active, restaurant.Address, restaurant.Description, restaurant.CreatedAt);
}
