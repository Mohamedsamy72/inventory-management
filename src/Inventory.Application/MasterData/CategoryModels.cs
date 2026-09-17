namespace Inventory.Application.MasterData;

public sealed record CreateCategoryCommand(string NameArabic, string? Description);

public sealed record UpdateCategoryCommand(string NameArabic, string? Description);

public sealed record CategorySummary(Guid Id, string NameArabic, string? Description, bool IsActive, DateTimeOffset CreatedAt);
