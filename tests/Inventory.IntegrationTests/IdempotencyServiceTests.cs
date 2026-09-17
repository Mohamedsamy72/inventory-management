using System.Security.Claims;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 7.8 (docs/30 §6.2). No REQUIRED-idempotency endpoint exists yet (Phase 8+) - the
/// service itself is exercised directly against real PostgreSQL.</summary>
public sealed class IdempotencyServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdempotencyServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record Seed(Guid CompanyId, Guid UserId);

    private static void AuthenticateScopeAs(IServiceProvider scopeServices, Guid companyId, Guid userId)
    {
        var httpContextAccessor = scopeServices.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scopeServices };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CurrentUserService.CompanyIdClaimType, companyId.ToString()),
                new Claim(CurrentUserService.UserIdClaimType, userId.ToString()),
            ],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;
    }

    private async Task<Seed> SeedUserAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User user, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        return new Seed(company.Id, user.Id);
    }

    [Fact]
    public async Task A_Fresh_Key_Is_Reported_As_New()
    {
        Seed seed = await SeedUserAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
        var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

        IdempotencyCheckResult result = await service.CheckAsync(Guid.NewGuid().ToString(), "POST /api/v1/test", new { a = 1 }, CancellationToken.None);

        Assert.Equal(IdempotencyOutcome.New, result.Outcome);
    }

    [Fact]
    public async Task A_Replayed_Key_With_The_Same_Payload_Returns_The_Cached_Response()
    {
        Seed seed = await SeedUserAsync();
        string key = Guid.NewGuid().ToString();
        var payload = new { a = 1, b = "x" };

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

            IdempotencyCheckResult first = await service.CheckAsync(key, "POST /api/v1/test", payload, CancellationToken.None);
            Assert.Equal(IdempotencyOutcome.New, first.Outcome);

            service.RecordResponse(key, "POST /api/v1/test", payload, 201, new { id = "abc123" });
            await context.SaveChangesAsync();
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

            IdempotencyCheckResult replay = await service.CheckAsync(key, "POST /api/v1/test", payload, CancellationToken.None);

            Assert.Equal(IdempotencyOutcome.Replay, replay.Outcome);
            Assert.Equal(201, replay.CachedStatusCode);
            Assert.Contains("abc123", replay.CachedPayloadJson);
        }
    }

    [Fact]
    public async Task The_Same_Key_With_A_Different_Payload_Is_Reported_As_Reuse()
    {
        Seed seed = await SeedUserAsync();
        string key = Guid.NewGuid().ToString();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
            service.RecordResponse(key, "POST /api/v1/test", new { a = 1 }, 200, new { ok = true });
            await context.SaveChangesAsync();
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

            IdempotencyCheckResult result = await service.CheckAsync(key, "POST /api/v1/test", new { a = 2 }, CancellationToken.None);

            Assert.Equal(IdempotencyOutcome.KeyReuse, result.Outcome);
        }
    }

    /// <summary>docs/30 §6.3: a key is scoped to (company_id, user_id) - a different user, even
    /// in the SAME company, never sees another user's cached response.</summary>
    [Fact]
    public async Task A_Key_Never_Crosses_Users_Even_Within_The_Same_Company()
    {
        Seed seed = await SeedUserAsync();
        (User otherUser, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        string key = Guid.NewGuid().ToString();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
            service.RecordResponse(key, "POST /api/v1/test", new { a = 1 }, 200, new { ok = true });
            await context.SaveChangesAsync();
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, otherUser.Id);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

            IdempotencyCheckResult result = await service.CheckAsync(key, "POST /api/v1/test", new { a = 1 }, CancellationToken.None);

            Assert.Equal(IdempotencyOutcome.New, result.Outcome);
        }
    }

    [Fact]
    public async Task Concurrent_Duplicate_Requests_The_Second_Fails_On_The_Unique_Constraint()
    {
        Seed seed = await SeedUserAsync();
        string key = Guid.NewGuid().ToString();
        var payload = new { a = 1 };

        async Task RecordAsync()
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
            service.RecordResponse(key, "POST /api/v1/test", payload, 200, new { ok = true });
            await context.SaveChangesAsync();
        }

        Exception? firstException = null;
        Exception? secondException = null;
        try
        {
            await RecordAsync();
        }
        catch (Exception ex)
        {
            firstException = ex;
        }

        try
        {
            await RecordAsync();
        }
        catch (Exception ex)
        {
            secondException = ex;
        }

        // Exactly one of the two identical inserts succeeds; the other fails on
        // uq_idempotency_key (docs/30 §6.2 step 6 - "the unique constraint, not the lookup, is
        // the actual guarantee").
        Assert.True((firstException is null) != (secondException is null));
        Assert.True(firstException is null or DbUpdateException);
        Assert.True(secondException is null or DbUpdateException);
    }

    [Fact]
    public async Task Cleanup_Removes_Only_Expired_Records()
    {
        Seed seed = await SeedUserAsync();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var expired = new IdempotencyRecord(
                seed.CompanyId, seed.UserId, Guid.NewGuid().ToString(), "POST /api/v1/test", "hash1", 200, null,
                DateTimeOffset.UtcNow.AddHours(-1));
            var fresh = new IdempotencyRecord(
                seed.CompanyId, seed.UserId, Guid.NewGuid().ToString(), "POST /api/v1/test", "hash2", 200, null,
                DateTimeOffset.UtcNow.AddHours(23));
            context.IdempotencyRecords.AddRange(expired, fresh);
            await context.SaveChangesAsync();

            AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.UserId);
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
            int removed = await service.CleanupExpiredAsync(CancellationToken.None);

            Assert.True(removed >= 1);

            bool expiredStillExists = await context.IdempotencyRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == expired.Id);
            bool freshStillExists = await context.IdempotencyRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == fresh.Id);
            Assert.False(expiredStillExists);
            Assert.True(freshStillExists);
        }
    }
}
