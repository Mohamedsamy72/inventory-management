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

/// <summary>Change 5 - GET/PUT /api/v1/settings (`settings:manage`, Owner/Admin only). Also proves
/// the Change 6 audit trail: an update writes a COMPANY_SETTINGS_UPDATED row the audit screen
/// ("الحركات") can read, and audit rows never cross companies.</summary>
public sealed class SettingsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SettingsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record SettingsDto(string CompanyName, string CompanyCode, string TimezoneId, string CurrencyCode);
    private sealed record AuditPageDto(List<AuditRowDto> Items);
    private sealed record AuditRowDto(Guid EntityId, string Action);

    private async Task<(Guid CompanyId, HttpClient Client)> LoginAsync(RoleName role, Guid? companyId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid id = companyId ?? (await AuthTestHelpers.CreateCompanyAsync(context)).Id;
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, id);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        return (id, await AuthTestHelpers.LoginAsAsync(_factory, user, password));
    }

    [Theory]
    [InlineData(RoleName.Owner)]
    [InlineData(RoleName.Admin)]
    public async Task Owner_And_Admin_Can_Read_And_Update_The_Timezone(RoleName role)
    {
        (_, HttpClient client) = await LoginAsync(role);

        using HttpResponseMessage get = await client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var before = await get.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal("Africa/Cairo", before!.TimezoneId);
        Assert.Equal("EGP", before.CurrencyCode);

        using HttpResponseMessage put = await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Put, "/api/v1/settings", new { timezoneId = "Europe/London" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using HttpResponseMessage after = await client.GetAsync("/api/v1/settings");
        Assert.Equal("Europe/London", (await after.Content.ReadFromJsonAsync<SettingsDto>())!.TimezoneId);
    }

    [Theory]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Other_Roles_Get_403_On_Read_And_Update(RoleName role)
    {
        (_, HttpClient client) = await LoginAsync(role);

        using HttpResponseMessage get = await client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

        using HttpResponseMessage put = await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Put, "/api/v1/settings", new { timezoneId = "Europe/London" });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task An_Invalid_Timezone_Returns_400_With_The_Standard_Error_And_Changes_Nothing()
    {
        (_, HttpClient client) = await LoginAsync(RoleName.Owner);

        using HttpResponseMessage put = await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Put, "/api/v1/settings", new { timezoneId = "Mars/Olympus" });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        string body = await put.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_TIMEZONE", body, StringComparison.Ordinal);
        Assert.Contains("messageAr", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Posted_CompanyId_Cannot_Redirect_The_Update_To_Another_Tenant()
    {
        (Guid ownCompany, HttpClient owner) = await LoginAsync(RoleName.Owner);
        (Guid otherCompany, HttpClient otherOwner) = await LoginAsync(RoleName.Owner);

        using HttpResponseMessage put = await AuthTestHelpers.SendJsonAsync(
            owner, HttpMethod.Put, "/api/v1/settings", new { timezoneId = "Asia/Tokyo", companyId = otherCompany });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using HttpResponseMessage otherGet = await otherOwner.GetAsync("/api/v1/settings");
        Assert.Equal("Africa/Cairo", (await otherGet.Content.ReadFromJsonAsync<SettingsDto>())!.TimezoneId);
        Assert.NotEqual(ownCompany, otherCompany);
    }

    [Fact]
    public async Task An_Update_Is_Audited_And_Audit_Rows_Never_Cross_Companies()
    {
        (Guid ownCompany, HttpClient owner) = await LoginAsync(RoleName.Owner);
        (_, HttpClient otherOwner) = await LoginAsync(RoleName.Owner);

        await AuthTestHelpers.SendJsonAsync(owner, HttpMethod.Put, "/api/v1/settings", new { timezoneId = "Asia/Tokyo" });

        var ownAudit = await owner.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=100");
        Assert.Contains(ownAudit!.Items, a => a.Action == "COMPANY_SETTINGS_UPDATED" && a.EntityId == ownCompany);

        var otherAudit = await otherOwner.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=100");
        Assert.DoesNotContain(otherAudit!.Items, a => a.Action == "COMPANY_SETTINGS_UPDATED" && a.EntityId == ownCompany);
    }
}
