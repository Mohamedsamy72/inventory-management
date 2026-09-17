using System.Net;
using System.Net.Http.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Tasks 5.6-5.7. Focused coverage only - keyset pagination, permission gating, and
/// tenant isolation are already proven generically by <see cref="CategoriesEndpointTests"/>
/// against the exact same shared <c>KeysetPagination</c>/<c>MasterDataErrorWriter</c>
/// infrastructure these two resources also use; repeating that full battery per resource would
/// test the shared code, not this one.</summary>
public sealed class UnitsAndSuppliersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UnitsAndSuppliersEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAsOwnerAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        return await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
    }

    [Fact]
    public async Task Creating_Updating_And_Deactivating_A_Unit_Works_End_To_End()
    {
        using HttpClient client = await LoginAsOwnerAsync();

        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "كيلوجرام", abbreviation = "كجم" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var unit = await created.Content.ReadFromJsonAsync<UnitDto>();

        using HttpResponseMessage updated = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/units/{unit!.Id}", new { nameArabic = "كيلوجرام معدّل", abbreviation = "كجم" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedUnit = await updated.Content.ReadFromJsonAsync<UnitDto>();
        Assert.Equal("كيلوجرام معدّل", updatedUnit!.NameArabic);

        using HttpResponseMessage deactivated = await AuthTestHelpers.PostJsonAsync(client, $"/api/v1/units/{unit.Id}/deactivate", new { });
        var deactivatedUnit = await deactivated.Content.ReadFromJsonAsync<UnitDto>();
        Assert.False(deactivatedUnit!.IsActive);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool auditRowExists = await context.AuditLogs.IgnoreQueryFilters()
            .AnyAsync(a => a.EntityId == unit.Id && a.Action == "UNIT_DEACTIVATED");
        Assert.True(auditRowExists);
    }

    [Fact]
    public async Task Creating_A_Duplicate_Unit_Name_Returns_409()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        string name = "وحدة مكررة " + Guid.NewGuid().ToString("N")[..6];

        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/units", new { nameArabic = name, abbreviation = (string?)null });
        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(client, "/api/v1/units", new { nameArabic = name, abbreviation = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Creating_And_Updating_A_Supplier_Works_End_To_End()
    {
        using HttpClient client = await LoginAsOwnerAsync();

        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/suppliers",
            new { nameArabic = "شركة التوريد المصرية", phone = "01000000000", contactPerson = (string?)null, address = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var supplier = await created.Content.ReadFromJsonAsync<SupplierDto>();

        using HttpResponseMessage updated = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/suppliers/{supplier!.Id}",
            new { nameArabic = "شركة التوريد المصرية المحدثة", phone = "01000000000", contactPerson = (string?)null, address = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedSupplier = await updated.Content.ReadFromJsonAsync<SupplierDto>();
        Assert.Equal("شركة التوريد المصرية المحدثة", updatedSupplier!.NameArabic);
    }

    /// <summary>Two DIFFERENT suppliers with the same name in one company are allowed - unlike
    /// categories/units/items, `suppliers` has no per-company unique-name constraint in the real
    /// migration (no `uq_suppliers_*` exists), matching that two real-world suppliers can share
    /// a common business name.</summary>
    [Fact]
    public async Task Two_Suppliers_May_Share_The_Same_Name()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        string name = "مورد بنفس الاسم " + Guid.NewGuid().ToString("N")[..6];

        using HttpResponseMessage first = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/suppliers", new { nameArabic = name, phone = (string?)null, contactPerson = (string?)null, address = (string?)null, notes = (string?)null });
        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/suppliers", new { nameArabic = name, phone = (string?)null, contactPerson = (string?)null, address = (string?)null, notes = (string?)null });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    /// <summary>Warehouse Staff holds no `units:manage`/`suppliers:manage` (docs/03 §"Master
    /// Data" - ❌ for both), yet the receiving-order line editor they use needs a unit picker
    /// and the create form needs a supplier picker, both by name.</summary>
    [Fact]
    public async Task Warehouse_Staff_Can_List_Unit_And_Supplier_Names_But_Not_The_Full_Manage_Gated_Lists()
    {
        using HttpClient ownerClient = await LoginAsOwnerAsync();
        using HttpResponseMessage unitResponse = await AuthTestHelpers.PostJsonAsync(
            ownerClient, "/api/v1/units", new { nameArabic = "كيلوجرام " + Guid.NewGuid().ToString("N")[..6], abbreviation = (string?)null });
        var unit = await unitResponse.Content.ReadFromJsonAsync<UnitDto>();
        using HttpResponseMessage supplierResponse = await AuthTestHelpers.PostJsonAsync(
            ownerClient, "/api/v1/suppliers",
            new { nameArabic = "مورد " + Guid.NewGuid().ToString("N")[..6], phone = (string?)null, contactPerson = (string?)null, address = (string?)null, notes = (string?)null });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid companyId = (await context.Units.IgnoreQueryFilters().FirstAsync(u => u.Id == unit!.Id)).CompanyId;
        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, companyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        using HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage unitNames = await staffClient.GetAsync("/api/v1/units/names");
        Assert.Equal(HttpStatusCode.OK, unitNames.StatusCode);
        Assert.Contains((await unitNames.Content.ReadFromJsonAsync<List<NameDto>>())!, u => u.Id == unit!.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/v1/units")).StatusCode);

        using HttpResponseMessage supplierNames = await staffClient.GetAsync("/api/v1/suppliers/names");
        Assert.Equal(HttpStatusCode.OK, supplierNames.StatusCode);
        Assert.Contains((await supplierNames.Content.ReadFromJsonAsync<List<NameDto>>())!, s => s.Id == supplier!.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/v1/suppliers")).StatusCode);
    }

    private sealed record UnitDto(Guid Id, string NameArabic, string? Abbreviation, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record SupplierDto(Guid Id, string NameArabic, string? Phone, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record NameDto(Guid Id, string NameArabic);
}
