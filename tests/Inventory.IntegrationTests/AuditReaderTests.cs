using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Phase 14 - Audit Viewer & Activity Monitor (docs/09 tasks 14.1-14.5, ADR-013).
/// `audit:view`/`audit:export` are `NON_GRANTABLE` - Owner only, with no grant path, verified
/// here at the real HTTP layer (Phase 4's `AuthorizationTests` already proved the same guarantee
/// at the raw `IAuthorizationService` layer).</summary>
public sealed class AuditReaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuditReaderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record AuditLogDto(Guid Id, Guid ActorUserId, string ActorRole, string Action, string EntityType, Guid EntityId, string DescriptionArabic, string? OldValuesJson, string? NewValuesJson, string Result, Guid? WarehouseId, Guid? RestaurantId, string CorrelationId, DateTimeOffset CreatedAt);
    private sealed record PageDto<T>(List<T> Items, string? NextCursor);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid OwnerId);

    private async Task<Seed> SeedAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
        return new Seed(company.Id, client, owner.Id);
    }

    [Fact]
    public async Task Owner_Sees_Audit_Entries_From_Multiple_Modules()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/categories", new { nameArabic = "قسم " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage warehouseResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/warehouses", new { nameArabic = "مستودع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<AuditLogDto>>();

        Assert.Contains(page!.Items, a => a.EntityId == category!.Id && a.Action == "CATEGORY_CREATED");
        Assert.Contains(page.Items, a => a.EntityId == warehouse!.Id && a.Action == "WAREHOUSE_CREATED");

        using HttpResponseMessage activityResponse = await seed.OwnerClient.GetAsync("/api/v1/audit/activity");
        Assert.Equal(HttpStatusCode.OK, activityResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_Gets_403_On_View_And_Export()
    {
        Seed seed = await SeedAsync();

        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);

        using HttpResponseMessage viewResponse = await adminClient.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.Forbidden, viewResponse.StatusCode);

        using HttpResponseMessage activityResponse = await adminClient.GetAsync("/api/v1/audit/activity");
        Assert.Equal(HttpStatusCode.Forbidden, activityResponse.StatusCode);

        using HttpResponseMessage exportResponse = await adminClient.GetAsync("/api/v1/audit/export");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }

    /// <summary>Task 14.5: the assignment endpoint refuses to create this grant row in the first
    /// place (`NON_GRANTABLE`, task 4.4) - this test bypasses it on purpose to simulate a bad
    /// row reaching the table some other way, proving the real HTTP endpoint is still denied by
    /// the pipeline-level role-denial check (ADR-012/013), not merely the grant-time guard.</summary>
    [Fact]
    public async Task A_Stray_Grant_Row_For_Audit_View_Still_Yields_403_At_The_Real_Endpoint()
    {
        Seed seed = await SeedAsync();

        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            Permission auditView = await context.Permissions.IgnoreQueryFilters().FirstAsync(p => p.Code == "audit:view");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO user_permissions (user_id, permission_id, is_granted) VALUES ({admin.Id}, {auditView.Id}, true)");
        }

        HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);

        using HttpResponseMessage viewResponse = await adminClient.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.Forbidden, viewResponse.StatusCode);
    }

    [Fact]
    public async Task No_Password_Value_Ever_Appears_In_Any_Audit_Row()
    {
        Seed seed = await SeedAsync();

        const string plaintextPassword = "Str0ng!Passw0rd";
        using HttpResponseMessage createUserResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Post, "/api/v1/users",
            new { fullName = "مستخدم اختبار", mobileNumber = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999), password = plaintextPassword, role = "WarehouseStaff" });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync("/api/v1/audit?limit=100");
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<AuditLogDto>>();

        foreach (AuditLogDto entry in page!.Items)
        {
            Assert.DoesNotContain(plaintextPassword, entry.OldValuesJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(plaintextPassword, entry.NewValuesJson ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Export_Writes_Its_Own_Audit_Entry()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage exportResponse = await seed.OwnerClient.GetAsync("/api/v1/audit/export");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync("/api/v1/audit?limit=50");
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<AuditLogDto>>();
        Assert.Contains(page!.Items, a => a.Action == "AUDIT_LOG_EXPORTED" && a.ActorUserId == seed.OwnerId);
    }

    [Fact]
    public async Task Date_Range_Filter_Excludes_Rows_Outside_The_Window()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/categories", new { nameArabic = "قسم " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        DateTimeOffset farPast = DateTimeOffset.UtcNow.AddYears(-2);
        DateTimeOffset stillPast = DateTimeOffset.UtcNow.AddYears(-1);

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync(
            $"/api/v1/audit?from={Uri.EscapeDataString(farPast.ToString("O"))}&to={Uri.EscapeDataString(stillPast.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<AuditLogDto>>();

        Assert.DoesNotContain(page!.Items, a => a.EntityId == category!.Id);
    }
}
