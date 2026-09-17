namespace Inventory.Application.MasterData;

public sealed record CreateRestaurantCommand(string NameArabic, string Code, Guid DefaultServingWarehouseId, string? Address, string? Description);

public sealed record UpdateRestaurantCommand(string NameArabic, string? Address, string? Description);

public sealed record RestaurantSummary(
    Guid Id, string NameArabic, string Code, Guid DefaultServingWarehouseId, bool IsActive,
    string? Address, string? Description, DateTimeOffset CreatedAt);
