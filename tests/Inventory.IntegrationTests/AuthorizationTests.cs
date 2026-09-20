using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 4.15 - the authorization pipeline (docs/04 §15) and audit infrastructure
/// (docs/14 §1), exercised over the real <c>/api/v1/users*</c> endpoints (task 4.14) wherever a
/// real HTTP surface exists yet, and directly against the wired-up
/// <see cref="IAuthorizationService"/>/<see cref="IScopeGuard"/> where it does not (role denial
/// and warehouse/restaurant scope have no consuming business endpoint before Phase 5+ -
/// docs/27 §12 records this).</summary>
public sealed class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>Makes <see cref="IHttpContextAccessor.HttpContext"/> (and therefore
    /// <c>ICurrentUserService</c> and the tenant query filter) see the given identity, for
    /// service-level tests that call an authorization/scope service directly rather than over
    /// HTTP - AsyncLocal-backed, so it is visible only within this call's own async flow.</summary>
    private static void AuthenticateScopeAs(IServiceProvider scopeServices, Guid companyId, Guid userId, RoleName role)
    {
        var httpContextAccessor = scopeServices.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scopeServices };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CurrentUserService.CompanyIdClaimType, companyId.ToString()),
                new Claim(CurrentUserService.UserIdClaimType, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
            ],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;
    }

    private async Task<(Company Company, User Owner, string Password)> SeedCompanyWithOwnerAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        return (company, owner, password);
    }

    [Fact]
    public async Task Getting_A_User_From_A_Different_Company_Returns_404_Not_403()
    {
        (Company companyA, User owner, string password) = await SeedCompanyWithOwnerAsync();
        (Company companyB, _, _) = await SeedCompanyWithOwnerAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (User otherCompanyUser, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, companyB.Id);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/users/{otherCompanyUser.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_User_With_No_Permissions_Is_Forbidden_From_Listing_Users()
    {
        (Company company, _, _) = await SeedCompanyWithOwnerAsync();
        (User plainUser, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, plainUser.Id, RoleName.User);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, plainUser, password);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_Can_Create_Another_Owner()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/users",
            new { fullName = "Second Owner", mobileNumber = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999), password = "Str0ng!Passw0rd", role = "Owner" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_Non_Owner_Cannot_Create_An_Owner()
    {
        (Company company, User owner, string ownerPassword) = await SeedCompanyWithOwnerAsync();
        (User admin, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, admin, adminPassword);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/users",
            new { fullName = "Attempted Owner", mobileNumber = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999), password = "Str0ng!Passw0rd", role = "Owner" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PRIVILEGE_ESCALATION_DENIED", body, StringComparison.Ordinal);
    }

    /// <summary>An over-posted <c>companyId</c> in the request body must have zero effect - the
    /// created user must land in the ACTOR's company, not the injected one, proving task 4.9's
    /// guard holds end-to-end and not merely in the architecture test.</summary>
    [Fact]
    public async Task An_Over_Posted_CompanyId_In_The_Create_User_Body_Is_Ignored()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
        string mobileNumber = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

        using HttpResponseMessage tokenResponse = await client.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(new
            {
                fullName = "Injected Co Test",
                mobileNumber,
                password = "Str0ng!Passw0rd",
                role = "User",
                companyId = Guid.NewGuid(), // over-posted - CreateUserRequest has no such property
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenBody!["csrfToken"]);

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        User created = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.MobileNumber == mobileNumber);

        Assert.Equal(company.Id, created.CompanyId);
    }

    [Fact]
    public async Task Owner_Cannot_Change_Their_Own_Role()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/users/{owner.Id}/role", new { role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PRIVILEGE_ESCALATION_DENIED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Owner_Cannot_Change_Their_Own_Scope()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/users/{owner.Id}/scope",
            new { warehouseIds = Array.Empty<Guid>(), restaurantIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assigning_A_Non_Grantable_Permission_Returns_400()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.User);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/users/{target.Id}/permissions",
            new { permissions = new[] { new { code = "costs:view", isGranted = true } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("NON_GRANTABLE_PERMISSION", body, StringComparison.Ordinal);
    }

    /// <summary>An actor who can call the endpoint (holds `users:manage`) but does not
    /// personally hold `items:create` must not be able to grant `items:create` to someone else -
    /// this is a genuinely grantable code, so only the "cannot exceed own bounds" check (task
    /// 4.8), not the non-grantable check (task 4.4), can be what blocks it.</summary>
    [Fact]
    public async Task Granting_A_Permission_The_Actor_Does_Not_Hold_Is_Rejected()
    {
        (Company company, User owner, string ownerPassword) = await SeedCompanyWithOwnerAsync();
        (User limitedActor, string actorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, limitedActor.Id, RoleName.User);
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.User);

        using (HttpClient ownerClient = await AuthTestHelpers.LoginAsAsync(_factory, owner, ownerPassword))
        {
            using HttpResponseMessage grantResponse = await AuthTestHelpers.SendJsonAsync(
                ownerClient, HttpMethod.Put, $"/api/v1/users/{limitedActor.Id}/permissions",
                new { permissions = new[] { new { code = "users:manage", isGranted = true } } });
            Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
        }

        using HttpClient actorClient = await AuthTestHelpers.LoginAsAsync(_factory, limitedActor, actorPassword);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            actorClient, HttpMethod.Put, $"/api/v1/users/{target.Id}/permissions",
            new { permissions = new[] { new { code = "items:create", isGranted = true } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PRIVILEGE_ESCALATION_DENIED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_Change_Writes_An_Audit_Row_In_The_Same_Operation()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.User);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/users/{target.Id}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var correlationIdHeaderValues));
        string correlationIdHeader = correlationIdHeaderValues!.Single();

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        AuditLog auditRow = await context.AuditLogs.IgnoreQueryFilters()
            .SingleAsync(a => a.EntityId == target.Id && a.Action == "USER_ROLE_CHANGED");

        Assert.Equal(owner.Id, auditRow.ActorUserId);
        Assert.Equal(nameof(RoleName.Owner), auditRow.ActorRole);
        // Task 4.13: the SAME correlation id the client received back, proving propagation into
        // the audit row, not just into log lines.
        Assert.Equal(correlationIdHeader, auditRow.CorrelationId);
    }

    /// <summary>A scope change naming a warehouse id that does not exist violates the composite
    /// tenant FK (ADR-016) mid-<c>SaveChangesAsync</c> - the audit row was already added to the
    /// SAME change tracker (task 4.11), so it must be discarded along with the scope rows when
    /// that single SaveChanges call fails, never partially persisted.</summary>
    [Fact]
    public async Task No_Audit_Row_Is_Written_When_The_Operation_Is_Rolled_Back()
    {
        (Company company, User owner, string password) = await SeedCompanyWithOwnerAsync();
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.User);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/users/{target.Id}/scope",
            new { warehouseIds = new[] { Guid.NewGuid() }, restaurantIds = Array.Empty<Guid>() });

        Assert.True(
            response.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.BadRequest,
            $"Expected the nonexistent warehouse id to fail the operation, got {response.StatusCode}.");

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool auditRowExists = await context.AuditLogs.IgnoreQueryFilters()
            .AnyAsync(a => a.EntityId == target.Id && a.Action == "USER_SCOPE_CHANGED");
        bool scopeRowExists = await context.UserWarehouseScopes.IgnoreQueryFilters()
            .AnyAsync(s => s.UserId == target.Id);

        Assert.False(auditRowExists, "No audit row should survive a rolled-back operation.");
        Assert.False(scopeRowExists, "No scope row should survive a rolled-back operation.");
    }

    [Fact]
    public async Task Scope_Guard_Returns_Exactly_The_Assigned_Warehouse_Ids()
    {
        (Company company, User owner, _) = await SeedCompanyWithOwnerAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var warehouse1 = new Warehouse(company.Id, "مستودع 1", "WH-" + Guid.NewGuid().ToString("N")[..6], null, null);
        var warehouse2 = new Warehouse(company.Id, "مستودع 2", "WH-" + Guid.NewGuid().ToString("N")[..6], null, null);
        context.Warehouses.AddRange(warehouse1, warehouse2);
        (User staff, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        context.UserWarehouseScopes.Add(new UserWarehouseScope(company.Id, staff.Id, warehouse1.Id));
        context.UserWarehouseScopes.Add(new UserWarehouseScope(company.Id, staff.Id, warehouse2.Id));
        await context.SaveChangesAsync();

        AuthenticateScopeAs(scope.ServiceProvider, company.Id, owner.Id, RoleName.Owner);
        var scopeGuard = scope.ServiceProvider.GetRequiredService<Application.Common.IScopeGuard>();
        IReadOnlySet<Guid> authorized = await scopeGuard.GetAuthorizedWarehouseIdsAsync(staff.Id, CancellationToken.None);

        Assert.Equal(new HashSet<Guid> { warehouse1.Id, warehouse2.Id }, authorized);
    }

    [Fact]
    public async Task Scope_Guard_Returns_Empty_For_A_User_With_No_Scope_Rows()
    {
        (Company company, User owner, _) = await SeedCompanyWithOwnerAsync();
        (User staff, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);

        using IServiceScope scope = _factory.Services.CreateScope();
        AuthenticateScopeAs(scope.ServiceProvider, company.Id, owner.Id, RoleName.Owner);
        var scopeGuard = scope.ServiceProvider.GetRequiredService<Application.Common.IScopeGuard>();
        IReadOnlySet<Guid> authorized = await scopeGuard.GetAuthorizedRestaurantIdsAsync(staff.Id, CancellationToken.None);

        Assert.Empty(authorized);
    }

    /// <summary>ADR-012's "overrides any grant" clause, proven against a `user_permissions` row
    /// that exists despite the assignment endpoint refusing to create one - simulating a bad row
    /// reaching the database some other way, which is exactly the scenario the pipeline-level
    /// handler exists to still catch (docs/09 §4 "Risks").</summary>
    [Fact]
    public async Task Role_Denial_Overrides_A_Non_Grantable_Permission_Even_If_A_Grant_Row_Exists()
    {
        (Company company, _, _) = await SeedCompanyWithOwnerAsync();
        (User admin, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        Permission auditView = await context.Permissions.IgnoreQueryFilters().FirstAsync(p => p.Code == "audit:view");
        // Bypasses UserManagementService.SetPermissionsAsync's own guard on purpose - simulating
        // a bad row that reached the table some other way.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO user_permissions (user_id, permission_id, is_granted) VALUES ({admin.Id}, {auditView.Id}, true)");

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CurrentUserService.CompanyIdClaimType, company.Id.ToString()),
                new Claim(CurrentUserService.UserIdClaimType, admin.Id.ToString()),
                new Claim(ClaimTypes.Role, nameof(RoleName.Admin)),
            ],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;

        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        AuthorizationResult result = await authorizationService.AuthorizeAsync(fakeHttpContext.User, "audit:view");

        Assert.False(result.Succeeded, "Admin must be denied audit:view even with a grant row present (ADR-012).");
    }

    [Fact]
    public async Task Role_Denial_Allows_Owner_For_A_Non_Grantable_Permission()
    {
        (Company company, User owner, _) = await SeedCompanyWithOwnerAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CurrentUserService.CompanyIdClaimType, company.Id.ToString()),
                new Claim(CurrentUserService.UserIdClaimType, owner.Id.ToString()),
                new Claim(ClaimTypes.Role, nameof(RoleName.Owner)),
            ],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;

        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        AuthorizationResult result = await authorizationService.AuthorizeAsync(fakeHttpContext.User, "costs:view");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Financial_Projection_Reveals_The_Value_Only_To_Owner()
    {
        (Company company, User owner, _) = await SeedCompanyWithOwnerAsync();
        (User admin, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);

        using (IServiceScope ownerScope = _factory.Services.CreateScope())
        {
            AuthenticateScopeAs(ownerScope.ServiceProvider, company.Id, owner.Id, RoleName.Owner);
            var projection = ownerScope.ServiceProvider.GetRequiredService<Application.Common.IFinancialProjection>();

            Assert.True(projection.IsVisible);
            Assert.Equal(123.45m, projection.Apply(123.45m));
        }

        using (IServiceScope adminScope = _factory.Services.CreateScope())
        {
            AuthenticateScopeAs(adminScope.ServiceProvider, company.Id, admin.Id, RoleName.Admin);
            var projection = adminScope.ServiceProvider.GetRequiredService<Application.Common.IFinancialProjection>();

            Assert.False(projection.IsVisible);
            Assert.Null(projection.Apply(123.45m));
        }
    }

    [Fact]
    public async Task Owner_Cannot_Deactivate_Their_Own_Account()
    {
        (Company _, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(client, $"/api/v1/users/{owner.Id}/deactivate", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The real bug this test guards against: deactivating a user must not merely block
    /// their NEXT login - it must invalidate a session that is already live right now, via the
    /// same security-stamp rotation `OnValidatePrincipal` already checks on every request
    /// (docs/09 task 3.6). Before this was wired up, a deactivated user's existing cookie stayed
    /// valid for up to 8 hours regardless.</summary>
    [Fact]
    public async Task Deactivating_A_User_Immediately_Invalidates_Their_Already_Active_Session()
    {
        (Company company, User owner, string ownerPassword) = await SeedCompanyWithOwnerAsync();
        (User target, string targetPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.WarehouseStaff);

        using HttpClient targetClient = await AuthTestHelpers.LoginAsAsync(_factory, target, targetPassword);
        using HttpResponseMessage beforeDeactivation = await targetClient.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.OK, beforeDeactivation.StatusCode);

        using HttpClient ownerClient = await AuthTestHelpers.LoginAsAsync(_factory, owner, ownerPassword);
        using HttpResponseMessage deactivateResponse = await AuthTestHelpers.PostJsonAsync(ownerClient, $"/api/v1/users/{target.Id}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Same cookie jar as before deactivation - no new login - proving the EXISTING session
        // is rejected, not merely that a future login would fail.
        using HttpResponseMessage afterDeactivation = await targetClient.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    [Fact]
    public async Task Reactivating_A_User_Allows_Login_Again()
    {
        (Company company, User owner, string ownerPassword) = await SeedCompanyWithOwnerAsync();
        (User target, string targetPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.WarehouseStaff);

        using HttpClient ownerClient = await AuthTestHelpers.LoginAsAsync(_factory, owner, ownerPassword);
        await AuthTestHelpers.PostJsonAsync(ownerClient, $"/api/v1/users/{target.Id}/deactivate", new { });

        using HttpClient blockedClient = AuthTestHelpers.CreateClientWithCookies(_factory);
        using HttpResponseMessage blockedLogin = await AuthTestHelpers.PostJsonAsync(
            blockedClient, "/api/v1/auth/login", new { mobileNumber = target.MobileNumber, password = targetPassword });
        Assert.Equal(HttpStatusCode.Forbidden, blockedLogin.StatusCode);

        using HttpResponseMessage reactivateResponse = await AuthTestHelpers.PostJsonAsync(ownerClient, $"/api/v1/users/{target.Id}/reactivate", new { });
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);

        using HttpClient allowedClient = AuthTestHelpers.CreateClientWithCookies(_factory);
        using HttpResponseMessage allowedLogin = await AuthTestHelpers.PostJsonAsync(
            allowedClient, "/api/v1/auth/login", new { mobileNumber = target.MobileNumber, password = targetPassword });
        Assert.Equal(HttpStatusCode.OK, allowedLogin.StatusCode);
    }

    [Fact]
    public async Task Getting_A_Users_Scope_Returns_Their_Current_Assignment()
    {
        (Company company, User owner, string ownerPassword) = await SeedCompanyWithOwnerAsync();
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.WarehouseStaff);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var warehouse = new Warehouse(company.Id, "مخزن الاختبار", "WH-" + Guid.NewGuid().ToString("N")[..6], null, null);
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();

        using HttpClient ownerClient = await AuthTestHelpers.LoginAsAsync(_factory, owner, ownerPassword);
        await AuthTestHelpers.SendJsonAsync(
            ownerClient, HttpMethod.Put, $"/api/v1/users/{target.Id}/scope",
            new { warehouseIds = new[] { warehouse.Id }, restaurantIds = Array.Empty<Guid>() });

        using HttpResponseMessage response = await ownerClient.GetAsync($"/api/v1/users/{target.Id}/scope");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserScopeDto>();
        Assert.Contains(warehouse.Id, body!.WarehouseIds);
        Assert.Empty(body.RestaurantIds);
    }

    [Fact]
    public async Task Permission_Catalogue_Lists_Real_Codes_Including_Non_Grantable_Ones()
    {
        (Company _, User owner, string password) = await SeedCompanyWithOwnerAsync();
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalogue = await response.Content.ReadFromJsonAsync<List<PermissionCatalogueDto>>();
        Assert.Contains(catalogue!, p => p.Code == "items:view" && p.IsGrantable);
        Assert.Contains(catalogue!, p => p.Code == "audit:view" && !p.IsGrantable);
    }

    private sealed record UserScopeDto(List<Guid> WarehouseIds, List<Guid> RestaurantIds);
    private sealed record PermissionCatalogueDto(string Code, string Description, string Module, bool IsGrantable);
}
