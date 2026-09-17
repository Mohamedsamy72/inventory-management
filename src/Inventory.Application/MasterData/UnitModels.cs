namespace Inventory.Application.MasterData;

public sealed record CreateUnitCommand(string NameArabic, string? Abbreviation);

public sealed record UpdateUnitCommand(string NameArabic, string? Abbreviation);

public sealed record UnitSummary(Guid Id, string NameArabic, string? Abbreviation, bool IsActive, DateTimeOffset CreatedAt);
