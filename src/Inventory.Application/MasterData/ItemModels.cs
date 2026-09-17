namespace Inventory.Application.MasterData;

public sealed record CreateItemCommand(
    string NameArabic, Guid CategoryId, Guid BaseUnitId, Guid? PurchaseUnitId, Guid? DefaultSupplierId, string? Description);

public sealed record UpdateItemCommand(
    string NameArabic, Guid CategoryId, Guid? PurchaseUnitId, Guid? DefaultSupplierId, string? Description);

public sealed record ItemSummary(
    Guid Id, string GeneratedCode, string NameArabic, Guid CategoryId, Guid BaseUnitId,
    Guid? PurchaseUnitId, Guid? DefaultSupplierId, string? Description, bool IsActive, DateTimeOffset CreatedAt);
