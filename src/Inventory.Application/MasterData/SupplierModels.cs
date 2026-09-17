namespace Inventory.Application.MasterData;

public sealed record CreateSupplierCommand(string NameArabic, string? Phone, string? ContactPerson, string? Address, string? Notes);

public sealed record UpdateSupplierCommand(string NameArabic, string? Phone, string? ContactPerson, string? Address, string? Notes);

public sealed record SupplierSummary(
    Guid Id, string NameArabic, string? Phone, string? ContactPerson, string? Address, string? Notes, bool IsActive, DateTimeOffset CreatedAt);
