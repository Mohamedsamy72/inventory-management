namespace Inventory.Application.MasterData;

public sealed record CreateWarehouseCommand(string NameArabic, string Code, string? Address, string? Description);

public sealed record UpdateWarehouseCommand(string NameArabic, string? Address, string? Description);

public sealed record WarehouseSummary(
    Guid Id, string NameArabic, string Code, bool IsActive, string? Address, string? Description, DateTimeOffset CreatedAt);
