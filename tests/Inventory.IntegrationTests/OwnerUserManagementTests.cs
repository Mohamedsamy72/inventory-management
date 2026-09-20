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

/// <summary>Owner user management: creation rules, mobile uniqueness, scope validation, the two
/// password mechanisms (administrative reset vs the Owner's own OTP-gated change), and Owner
/// mobile change. Every path is exercised against the real endpoints; secrets must never appear in
/// responses or audit rows.</summary>
public sealed class OwnerUserManagementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string NewPassword = "Brand-New-Passw0rd!";
    private readonly WebApplicationFactory<Program> _factory;

    public OwnerUserManagementTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record UserDto(Guid Id, string FullName, string MobileNumber, string? Role, bool IsActive);
    private sealed record Ctx(Guid CompanyId, User Owner, string OwnerPassword, HttpClient OwnerClient);

    private async Task<Ctx> NewOwnerAsync(Guid? companyId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid id = companyId ?? (await AuthTestHelpers.CreateCompanyAsync(context)).Id;
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        return new Ctx(id, owner, password, await AuthTestHelpers.LoginAsAsync(_factory, owner, password));
    }

    private static string NewMobile() => "010" + Random.Shared.NextInt64(10_000_000, 99_999_999);

    private static Task<HttpResponseMessage> CreateUserAsync(HttpClient client, string role, string? mobile = null, object? extra = null) =>
        AuthTestHelpers.PostJsonAsync(client, "/api/v1/users", new
        {
            fullName = "مستخدم اختبار",
            mobileNumber = mobile ?? NewMobile(),
            password = "Init1al-Passw0rd!",
            role,
            warehouseIds = (extra as dynamic)?.warehouseIds,
            restaurantIds = (extra as dynamic)?.restaurantIds,
        });

    private static async Task<Guid> NewWarehouseAsync(HttpClient client)
    {
        using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع " + Guid.NewGuid().ToString("N")[..4], code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static async Task<Guid> NewRestaurantAsync(HttpClient client)
    {
        using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants", new { nameArabic = "فرع", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    // ---------------- creation ----------------

    [Theory]
    [InlineData("Admin")]
    [InlineData("WarehouseStaff")]
    [InlineData("RestaurantSupervisor")]
    [InlineData("User")]
    public async Task Owner_Can_Create_Every_Assignable_Role(string role)
    {
        Ctx ctx = await NewOwnerAsync();

        using HttpResponseMessage response = await CreateUserAsync(ctx.OwnerClient, role);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(role, created!.Role);
        Assert.True(created.IsActive);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Init1al-Passw0rd!", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creation_With_Scope_Applies_It_Atomically_And_Audits_Without_The_Password()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid warehouse = await NewWarehouseAsync(ctx.OwnerClient);

        using HttpResponseMessage response = await CreateUserAsync(ctx.OwnerClient, "WarehouseStaff", extra: new { warehouseIds = new[] { warehouse }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid userId = (await response.Content.ReadFromJsonAsync<UserDto>())!.Id;

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(warehouse, (await context.UserWarehouseScopes.IgnoreQueryFilters().SingleAsync(s => s.UserId == userId)).WarehouseId);
        var createdRows = await context.AuditLogs.IgnoreQueryFilters().Where(a => a.CompanyId == ctx.CompanyId && a.Action == "USER_CREATED").ToListAsync();
        string auditJson = string.Join("|", createdRows.Select(a => a.NewValuesJson + a.DescriptionArabic));
        Assert.DoesNotContain("Init1al-Passw0rd!", auditJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Accountant")]
    [InlineData("SuperUser")]
    [InlineData("99")]
    [InlineData("")]
    public async Task Retired_Or_Invalid_Roles_Are_Rejected(string role)
    {
        Ctx ctx = await NewOwnerAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/users", new
        {
            fullName = "x", mobileNumber = NewMobile(), password = "Init1al-Passw0rd!", role,
        });

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"role '{role}' returned {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Duplicate_Mobile_Is_Rejected_Within_And_Across_Companies_And_The_Number_Is_Normalized()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        string mobile = NewMobile();

        using HttpResponseMessage first = await CreateUserAsync(ctx.OwnerClient, "User", mobile);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage sameCompany = await CreateUserAsync(ctx.OwnerClient, "User", mobile);
        Assert.Equal(HttpStatusCode.Conflict, sameCompany.StatusCode);
        Assert.Contains("DUPLICATE_MOBILE", await sameCompany.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The mobile number is the LOGIN id and login resolves it across companies - a second
        // company must not be able to reuse it (formatting differences included).
        string formatted = $"{mobile[..3]}-{mobile[3..7]} {mobile[7..]}";
        using HttpResponseMessage otherCompany = await CreateUserAsync(other.OwnerClient, "User", formatted);
        Assert.Equal(HttpStatusCode.Conflict, otherCompany.StatusCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghijk")]
    [InlineData("")]
    public async Task Invalid_Mobile_Numbers_Are_Rejected(string mobile)
    {
        Ctx ctx = await NewOwnerAsync();

        using HttpResponseMessage response = await CreateUserAsync(ctx.OwnerClient, "User", mobile);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_And_Cross_Company_And_Role_Mismatched_Scope_Is_Rejected_And_No_User_Is_Created()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        Guid foreignWarehouse = await NewWarehouseAsync(other.OwnerClient);
        Guid ownWarehouse = await NewWarehouseAsync(ctx.OwnerClient);
        Guid ownRestaurant = await NewRestaurantAsync(ctx.OwnerClient);

        string mobile1 = NewMobile();
        using HttpResponseMessage unknown = await CreateUserAsync(ctx.OwnerClient, "WarehouseStaff", mobile1, new { warehouseIds = new[] { Guid.NewGuid() }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("INVALID_SCOPE", await unknown.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using HttpResponseMessage crossCompany = await CreateUserAsync(ctx.OwnerClient, "WarehouseStaff", NewMobile(), new { warehouseIds = new[] { foreignWarehouse }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, crossCompany.StatusCode);

        // A warehouse scope makes no sense for a Restaurant Supervisor, nor a restaurant scope for Warehouse Staff.
        using HttpResponseMessage wrongKind1 = await CreateUserAsync(ctx.OwnerClient, "RestaurantSupervisor", NewMobile(), new { warehouseIds = new[] { ownWarehouse }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, wrongKind1.StatusCode);
        using HttpResponseMessage wrongKind2 = await CreateUserAsync(ctx.OwnerClient, "WarehouseStaff", NewMobile(), new { warehouseIds = Array.Empty<Guid>(), restaurantIds = new[] { ownRestaurant } });
        Assert.Equal(HttpStatusCode.BadRequest, wrongKind2.StatusCode);
        using HttpResponseMessage adminWithScope = await CreateUserAsync(ctx.OwnerClient, "Admin", NewMobile(), new { warehouseIds = new[] { ownWarehouse }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, adminWithScope.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(1, await context.Users.IgnoreQueryFilters().CountAsync(u => u.CompanyId == ctx.CompanyId)); // only the owner
    }

    [Fact]
    public async Task Setting_Scope_On_An_Existing_User_Is_Validated_Too()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        Guid foreignWarehouse = await NewWarehouseAsync(other.OwnerClient);
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "WarehouseStaff");
        Guid userId = (await created.Content.ReadFromJsonAsync<UserDto>())!.Id;

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            ctx.OwnerClient, HttpMethod.Put, $"/api/v1/users/{userId}/scope", new { warehouseIds = new[] { foreignWarehouse }, restaurantIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Cannot_Create_An_Owner_And_Client_Supplied_CompanyId_Is_Ignored()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        using HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);

        using HttpResponseMessage asOwner = await CreateUserAsync(adminClient, "Owner");
        Assert.Equal(HttpStatusCode.Forbidden, asOwner.StatusCode);

        using HttpResponseMessage overPosted = await AuthTestHelpers.PostJsonAsync(adminClient, "/api/v1/users", new
        {
            fullName = "x", mobileNumber = NewMobile(), password = "Init1al-Passw0rd!", role = "User", companyId = other.CompanyId,
        });
        Assert.Equal(HttpStatusCode.Created, overPosted.StatusCode);
        Guid created = (await overPosted.Content.ReadFromJsonAsync<UserDto>())!.Id;

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(ctx.CompanyId, (await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == created)).CompanyId);
    }

    // ---------------- administrative password reset ----------------

    [Fact]
    public async Task Owner_Resets_A_NonOwner_Password_Without_The_Old_One_And_Old_Sessions_Die()
    {
        Ctx ctx = await NewOwnerAsync();
        string mobile = NewMobile();
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "User", mobile);
        Guid userId = (await created.Content.ReadFromJsonAsync<UserDto>())!.Id;

        // A live session for the target, created BEFORE the reset.
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        User target = await context.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == userId);
        using HttpClient targetClient = await AuthTestHelpers.LoginAsAsync(_factory, target, "Init1al-Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/v1/account/me")).StatusCode);

        using HttpResponseMessage reset = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{userId}/password", new { newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.DoesNotContain(NewPassword, await reset.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/v1/account/me")).StatusCode);

        using HttpClient fresh = AuthTestHelpers.CreateClientWithCookies(_factory);
        using HttpResponseMessage oldLogin = await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = mobile, password = "Init1al-Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using HttpResponseMessage newLogin = await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = mobile, password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var rows = await context.AuditLogs.IgnoreQueryFilters().Where(a => a.CompanyId == ctx.CompanyId && a.EntityId == userId).ToListAsync();
        string audit = string.Join("|", rows.Select(a => a.Action + a.DescriptionArabic + a.OldValuesJson + a.NewValuesJson));
        Assert.Contains("USER_PASSWORD_RESET_BY_ADMIN", audit, StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Weak_Password_Is_Rejected_By_The_Identity_Policy_And_Nothing_Changes()
    {
        Ctx ctx = await NewOwnerAsync();
        string mobile = NewMobile();
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "User", mobile);
        Guid userId = (await created.Content.ReadFromJsonAsync<UserDto>())!.Id;

        using HttpResponseMessage reset = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{userId}/password", new { newPassword = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        using HttpClient fresh = AuthTestHelpers.CreateClientWithCookies(_factory);
        using HttpResponseMessage login = await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = mobile, password = "Init1al-Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_And_Others_Cannot_Use_Password_Administration_And_It_Never_Targets_An_Owner()
    {
        Ctx ctx = await NewOwnerAsync();
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "User");
        Guid userId = (await created.Content.ReadFromJsonAsync<UserDto>())!.Id;

        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        using HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);
        using HttpResponseMessage byAdmin = await AuthTestHelpers.PostJsonAsync(adminClient, $"/api/v1/users/{userId}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);

        // Admin cannot reset the Owner's password either.
        using HttpResponseMessage adminVsOwner = await AuthTestHelpers.PostJsonAsync(adminClient, $"/api/v1/users/{ctx.Owner.Id}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Forbidden, adminVsOwner.StatusCode);

        // The Owner cannot use the administrative mechanism on their OWN account...
        using HttpResponseMessage self = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{ctx.Owner.Id}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);

        // ...nor on another Owner of the same company.
        Ctx second = await NewOwnerAsync(ctx.CompanyId);
        using HttpResponseMessage otherOwner = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{second.Owner.Id}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Forbidden, otherOwner.StatusCode);
    }

    [Fact]
    public async Task Cross_Company_Or_Unknown_Target_Returns_404_For_Password_Reset()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        using HttpResponseMessage created = await CreateUserAsync(other.OwnerClient, "User");
        Guid foreignUser = (await created.Content.ReadFromJsonAsync<UserDto>())!.Id;

        using HttpResponseMessage foreign = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{foreignUser}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        using HttpResponseMessage unknown = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{Guid.NewGuid()}/password", new { newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // ---------------- profile / mobile edit ----------------

    [Fact]
    public async Task Owner_Edits_Name_And_Mobile_Of_A_NonOwner_And_Duplicate_Or_Owner_Targets_Are_Refused()
    {
        Ctx ctx = await NewOwnerAsync();
        string mobileA = NewMobile();
        using HttpResponseMessage createdA = await CreateUserAsync(ctx.OwnerClient, "User", mobileA);
        Guid a = (await createdA.Content.ReadFromJsonAsync<UserDto>())!.Id;
        string mobileB = NewMobile();
        await CreateUserAsync(ctx.OwnerClient, "User", mobileB);

        string newMobile = NewMobile();
        using HttpResponseMessage ok = await AuthTestHelpers.SendJsonAsync(ctx.OwnerClient, HttpMethod.Put, $"/api/v1/users/{a}", new { fullName = "اسم جديد", mobileNumber = newMobile });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var updated = await ok.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("اسم جديد", updated!.FullName);
        Assert.Equal(newMobile, updated.MobileNumber);

        using HttpResponseMessage duplicate = await AuthTestHelpers.SendJsonAsync(ctx.OwnerClient, HttpMethod.Put, $"/api/v1/users/{a}", new { fullName = "x", mobileNumber = mobileB });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using HttpResponseMessage ownOwner = await AuthTestHelpers.SendJsonAsync(ctx.OwnerClient, HttpMethod.Put, $"/api/v1/users/{ctx.Owner.Id}", new { fullName = "x", mobileNumber = NewMobile() });
        Assert.Equal(HttpStatusCode.Forbidden, ownOwner.StatusCode);
    }

    [Fact]
    public async Task Admin_Cannot_Change_A_Mobile_Number_But_May_Rename()
    {
        Ctx ctx = await NewOwnerAsync();
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "User");
        var target = (await created.Content.ReadFromJsonAsync<UserDto>())!;
        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        using HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);

        using HttpResponseMessage mobile = await AuthTestHelpers.SendJsonAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{target.Id}", new { fullName = "x", mobileNumber = NewMobile() });
        Assert.Equal(HttpStatusCode.Forbidden, mobile.StatusCode);

        using HttpResponseMessage rename = await AuthTestHelpers.SendJsonAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{target.Id}", new { fullName = "اسم", mobileNumber = target.MobileNumber });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    // ---------------- Owner's own password: OTP ----------------

    [Fact]
    public async Task Owner_Own_Password_Change_Requires_The_OTP_And_Works_End_To_End()
    {
        Ctx ctx = await NewOwnerAsync();

        // Without the OTP step a made-up token is refused.
        using HttpResponseMessage bogus = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/change", new { resetToken = "nope", newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, bogus.StatusCode);

        using HttpResponseMessage otp = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/otp", new { });
        Assert.Equal(HttpStatusCode.OK, otp.StatusCode);

        using HttpResponseMessage wrong = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/verify", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        string code = await AuthTestHelpers.ForceKnownOtpAsync(_factory, ctx.Owner.Id);
        using HttpResponseMessage verify = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/verify", new { code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        string resetToken = (await verify.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["resetToken"];

        // The code is single-use.
        using HttpResponseMessage replay = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/verify", new { code });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        using HttpResponseMessage weak = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/change", new { resetToken, newPassword = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        using HttpResponseMessage change = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/password/change", new { resetToken, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.DoesNotContain(NewPassword, await change.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The Owner's current session survives (re-issued); the new password works, the old does not.
        Assert.Equal(HttpStatusCode.OK, (await ctx.OwnerClient.GetAsync("/api/v1/account/me")).StatusCode);
        using HttpClient fresh = AuthTestHelpers.CreateClientWithCookies(_factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = ctx.Owner.MobileNumber, password = ctx.OwnerPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = ctx.Owner.MobileNumber, password = NewPassword })).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var otpRows = await context.AuditLogs.IgnoreQueryFilters().Where(a => a.CompanyId == ctx.CompanyId && a.Action == "OWNER_PASSWORD_CHANGED_WITH_OTP").ToListAsync();
        string audit = string.Join("|", otpRows.Select(a => a.DescriptionArabic + a.OldValuesJson + a.NewValuesJson));
        Assert.NotEmpty(audit);
        Assert.DoesNotContain(NewPassword, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(resetToken, audit, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RoleName.Admin)]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Only_The_Owner_Can_Use_The_Owner_Self_Service_Security_Endpoints(RoleName role)
    {
        Ctx ctx = await NewOwnerAsync();
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, password);

        Assert.Equal(HttpStatusCode.Forbidden, (await AuthTestHelpers.PostJsonAsync(client, "/api/v1/account/password/otp", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthTestHelpers.PostJsonAsync(client, "/api/v1/account/password/change", new { resetToken = "x", newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthTestHelpers.PostJsonAsync(client, "/api/v1/account/mobile/otp", new { newMobileNumber = NewMobile() })).StatusCode);
    }

    // ---------------- Owner's own mobile: OTP ----------------

    [Fact]
    public async Task Owner_Mobile_Change_Is_Only_Active_After_OTP_Verification()
    {
        Ctx ctx = await NewOwnerAsync();
        string oldMobile = ctx.Owner.MobileNumber;
        string newMobile = NewMobile();

        using HttpResponseMessage otp = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/otp", new { newMobileNumber = newMobile });
        Assert.Equal(HttpStatusCode.OK, otp.StatusCode);
        string changeToken = (await otp.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["changeToken"];

        // Requesting the OTP changes nothing yet.
        using HttpClient before = AuthTestHelpers.CreateClientWithCookies(_factory);
        Assert.Equal(HttpStatusCode.OK, (await AuthTestHelpers.PostJsonAsync(before, "/api/v1/auth/login", new { mobileNumber = oldMobile, password = ctx.OwnerPassword })).StatusCode);

        using HttpResponseMessage wrongCode = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/confirm", new { changeToken, code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        using HttpResponseMessage tamperedToken = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/confirm", new { changeToken = changeToken + "x", code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, tamperedToken.StatusCode);

        string code = await AuthTestHelpers.ForceKnownOtpAsync(_factory, ctx.Owner.Id);
        using HttpResponseMessage confirm = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/confirm", new { changeToken, code });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using HttpClient fresh = AuthTestHelpers.CreateClientWithCookies(_factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = oldMobile, password = ctx.OwnerPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = newMobile, password = ctx.OwnerPassword })).StatusCode);
    }

    [Fact]
    public async Task Owner_Mobile_Change_Refuses_A_Number_That_Is_Taken_Or_Invalid()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();

        using HttpResponseMessage taken = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/otp", new { newMobileNumber = other.Owner.MobileNumber });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        using HttpResponseMessage invalid = await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, "/api/v1/account/mobile/otp", new { newMobileNumber = "12" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ---------------- Owner forgot-password ----------------

    [Fact]
    public async Task Owner_Can_Recover_Through_The_Normal_Forgot_Password_OTP_Flow()
    {
        Ctx ctx = await NewOwnerAsync();
        using HttpClient anonymous = AuthTestHelpers.CreateClientWithCookies(_factory);

        await AuthTestHelpers.PostJsonAsync(anonymous, "/api/v1/auth/forgot-password/otp", new { mobileNumber = ctx.Owner.MobileNumber });
        string code = await AuthTestHelpers.ForceKnownOtpAsync(_factory, ctx.Owner.Id);
        using HttpResponseMessage verify = await AuthTestHelpers.PostJsonAsync(anonymous, "/api/v1/auth/forgot-password/verify", new { mobileNumber = ctx.Owner.MobileNumber, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        string resetToken = (await verify.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["resetToken"];

        using HttpResponseMessage reset = await AuthTestHelpers.PostJsonAsync(anonymous, "/api/v1/auth/forgot-password/reset", new { mobileNumber = ctx.Owner.MobileNumber, resetToken, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using HttpClient fresh = AuthTestHelpers.CreateClientWithCookies(_factory);
        Assert.Equal(HttpStatusCode.OK, (await AuthTestHelpers.PostJsonAsync(fresh, "/api/v1/auth/login", new { mobileNumber = ctx.Owner.MobileNumber, password = NewPassword })).StatusCode);
    }

    // ---------------- deactivate / reactivate ----------------

    [Fact]
    public async Task Owner_Can_Deactivate_And_Reactivate_And_The_Database_Enforces_Global_Mobile_Uniqueness()
    {
        Ctx ctx = await NewOwnerAsync();
        using HttpResponseMessage created = await CreateUserAsync(ctx.OwnerClient, "User");
        var user = (await created.Content.ReadFromJsonAsync<UserDto>())!;

        Assert.False((await (await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{user.Id}/deactivate", new { })).Content.ReadFromJsonAsync<UserDto>())!.IsActive);
        Assert.True((await (await AuthTestHelpers.PostJsonAsync(ctx.OwnerClient, $"/api/v1/users/{user.Id}/reactivate", new { })).Content.ReadFromJsonAsync<UserDto>())!.IsActive);

        // Bypass the application layer entirely: the unique index itself must refuse a duplicate.
        Ctx other = await NewOwnerAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        context.Users.Add(new User(other.CompanyId, "dup", user.MobileNumber, "hash-placeholder", "stamp-placeholder"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
