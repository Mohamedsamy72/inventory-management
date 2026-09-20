namespace Inventory.Application.MasterData;

public sealed record CreateRestaurantCommand(string NameArabic, string Code, string? Address, string? Description);

public sealed record UpdateRestaurantCommand(string NameArabic, string? Address, string? Description);

public sealed record RestaurantSummary(
    Guid Id, string NameArabic, string Code, bool IsActive,
    string? Address, string? Description, DateTimeOffset CreatedAt);
