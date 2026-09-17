using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IItemUnitConversionService"/>.</summary>
public sealed class ItemUnitConversionService : IItemUnitConversionService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public ItemUnitConversionService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<ItemUnitConversionSummary>> CreateAsync(
        CreateItemUnitConversionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ConversionFactor <= 0)
        {
            return MasterDataResult.Failure<ItemUnitConversionSummary>(MasterDataError.InvalidConversionFactor);
        }

        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == command.ItemId, cancellationToken);
        if (item is null)
        {
            return MasterDataResult.Failure<ItemUnitConversionSummary>(MasterDataError.NotFound);
        }

        bool fromUnitExists = await _context.Units.AnyAsync(u => u.Id == command.FromUnitId, cancellationToken);
        if (!fromUnitExists)
        {
            return MasterDataResult.Failure<ItemUnitConversionSummary>(MasterDataError.NotFound);
        }

        // Task 6.5/ADR-023: a "correction" and a first-time definition are the same call - any
        // active row for this exact (item, from-unit) pair is deactivated before the new one is
        // inserted, both in the same SaveChangesAsync so they commit or roll back together.
        ItemUnitConversion? existingActive = await _context.ItemUnitConversions
            .Where(c => c.ItemId == command.ItemId && c.FromUnitId == command.FromUnitId && c.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        existingActive?.Deactivate();

        var conversion = new ItemUnitConversion(
            _currentUserService.CompanyId, command.ItemId, command.FromUnitId, item.BaseUnitId, command.ConversionFactor);
        _context.ItemUnitConversions.Add(conversion);

        _auditLogger.Record(new AuditEntry(
            existingActive is null ? "ITEM_CONVERSION_CREATED" : "ITEM_CONVERSION_CORRECTED",
            nameof(ItemUnitConversion), conversion.Id,
            $"تم تعريف معامل تحويل للصنف {item.NameArabic}: {command.ConversionFactor}",
            OldValues: existingActive is null ? null : new { existingActive.ConversionFactor },
            NewValues: new { conversion.FromUnitId, conversion.ConversionFactor },
            Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(conversion));
    }

    public async Task<IReadOnlyList<ItemUnitConversionSummary>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        List<ItemUnitConversion> conversions = await _context.ItemUnitConversions.AsNoTracking()
            .Where(c => c.ItemId == itemId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return conversions.Select(ToSummary).ToList();
    }

    public async Task<MasterDataResult<ItemUnitConversionSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        ItemUnitConversion? conversion = await _context.ItemUnitConversions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conversion is null)
        {
            return MasterDataResult.Failure<ItemUnitConversionSummary>(MasterDataError.NotFound);
        }

        conversion.Deactivate();

        _auditLogger.Record(new AuditEntry(
            "ITEM_CONVERSION_DEACTIVATED", nameof(ItemUnitConversion), conversion.Id,
            "تم تعطيل معامل تحويل",
            OldValues: null, NewValues: new { conversion.IsActive }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(conversion));
    }

    private static ItemUnitConversionSummary ToSummary(ItemUnitConversion conversion) => new(
        conversion.Id, conversion.ItemId, conversion.FromUnitId, conversion.ToBaseUnitId,
        conversion.ConversionFactor, conversion.IsActive, conversion.CreatedAt);
}
