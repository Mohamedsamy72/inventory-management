using System.Security.Claims;
using Inventory.Application.Auth;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Audit;
using Inventory.Infrastructure.Authorization;
using Inventory.Infrastructure.HealthChecks;
using Inventory.Infrastructure.Identity;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Inventory.Infrastructure.Sms;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Inventory.Infrastructure;

/// <summary>
/// The infrastructure composition root.
/// </summary>
/// <remarks>
/// This is the single seam through which <c>Inventory.Api</c> is allowed to reach the
/// infrastructure layer. No Api type may reference an infrastructure type directly
/// (ADR-002), which keeps the dependency direction inward and the host thin.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Name of the readiness health check, used to tag and filter it.</summary>
    public const string DatabaseReadinessCheckName = "postgresql";

    /// <summary>Tag applied to checks that belong to the readiness probe.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Configuration key holding the primary database connection string.</summary>
    public const string DatabaseConnectionName = "InventoryDatabase";

    /// <summary>The __Host- prefixed session cookie name (docs/08 §3) - Production/Staging
    /// only. See <see cref="AddAuthentication"/> for why Development uses a different name.</summary>
    public const string SessionCookieName = "__Host-InventorySession";

    /// <summary>Development's session cookie name - deliberately NOT `__Host-`-prefixed. See
    /// <see cref="AddAuthentication"/>.</summary>
    public const string DevelopmentSessionCookieName = "InventorySession";

    /// <summary>Configuration key holding the allowed frontend origin for CORS (docs/08 §CORS).</summary>
    public const string CorsAllowedOriginKey = "Cors:AllowedOrigin";

    public const string CorsPolicyName = "frontend";

    /// <summary>
    /// Registers infrastructure services and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string? connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        services.AddHealthChecks()
            .AddCheck(
                name: DatabaseReadinessCheckName,
                instance: new PostgreSqlReadinessHealthCheck(connectionString),
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag]);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));

        AddAuthentication(services, environment);
        AddCors(services, configuration);
        AddCsrf(services, environment);

        // Rate limiting (task 3.11) is registered directly in Program.cs, not here - see
        // Inventory.Api.RateLimiting.RateLimitingExtensions for why.
        services.AddScoped<ISmsSender, ConsoleSmsSender>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IOtpAttemptLimiter, RateLimiting.OtpAttemptLimiter>();
        services.AddScoped<IPasswordResetOtpService, PasswordResetOtpService>();
        services.AddScoped<IAccountProfileReader, Identity.AccountProfileReader>();

        // Phase 4: authorization pipeline (docs/04 §15) and audit infrastructure (docs/14 §1).
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IScopeGuard, ScopeGuard>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IFinancialProjection, Services.FinancialProjection>();
        services.AddScoped<Application.Users.IUserManagementService, Users.UserManagementService>();
        services.AddScoped<IDocumentSequenceService, Sequencing.DocumentSequenceService>();
        services.AddScoped<Application.MasterData.ICategoryService, MasterData.CategoryService>();
        services.AddScoped<Application.MasterData.IUnitService, MasterData.UnitService>();
        services.AddScoped<Application.MasterData.ISupplierService, MasterData.SupplierService>();
        services.AddScoped<Application.MasterData.IWarehouseService, MasterData.WarehouseService>();
        services.AddScoped<Application.MasterData.IRestaurantService, MasterData.RestaurantService>();
        services.AddScoped<Application.MasterData.IItemService, MasterData.ItemService>();
        services.AddScoped<Application.MasterData.IItemUnitConversionService, MasterData.ItemUnitConversionService>();
        services.AddScoped<IUnitConversionResolver, MasterData.UnitConversionResolver>();
        services.AddSingleton<Domain.Services.ICostingEngine, Domain.Services.CostingEngine>();
        services.AddScoped<IStockPostingService, Stock.StockPostingService>();
        services.AddScoped<IInTransitCalculator, Stock.InTransitCalculator>();
        services.AddScoped<IIdempotencyService, Idempotency.IdempotencyService>();

        // Phase 8: receiving (docs/09 8.1-8.13, docs/30 §4 T1/T2/T3).
        services.AddScoped<Application.Receiving.IReceivingOrderService, Receiving.ReceivingOrderService>();

        // Phase 9: multi-item supply requests (docs/09 9.1-9.14, ADR-028). Zero stock effect.
        services.AddScoped<Application.SupplyRequests.ISupplyRequestService, SupplyRequests.SupplyRequestService>();

        // Phase 10: fulfilment & dispatch (docs/09 10.1-10.8, ADR-017). Zero stock effect.
        services.AddScoped<Application.Supplies.ISupplyService, Supplies.SupplyService>();

        // Phase 12: discrepancies & reconciliation (docs/09 12.1-12.7, ADR-021). Resolution never
        // posts a stock movement.
        services.AddScoped<Application.Discrepancies.IDiscrepancyService, Discrepancies.DiscrepancyService>();

        // Phase 13: physical stock counts & adjustments (docs/09 13.1-13.11, ADR-018/020/021).
        services.AddScoped<Application.StockCounts.IStockCountService, StockCounts.StockCountService>();

        // Phase 14: audit viewer & activity monitor (docs/09 14.1-14.5, ADR-013). Owner only.
        services.AddScoped<Application.Audit.IAuditReaderService, Audit.AuditReaderService>();
        services.AddScoped<Application.Settings.ISettingsService, Settings.SettingsService>();

        // REQ-07 (docs/26, docs/04 §12) - restaurant consumption logging. Never scheduled by name
        // in docs/09; closed as a Phase T1 traceability gap. Zero stock effect.
        services.AddScoped<Application.Consumption.IConsumptionService, Consumption.ConsumptionService>();
        services.AddHostedService<Idempotency.MaintenanceBackgroundService>();
        // Scoped, not Singleton: this handler depends on ICurrentUserService and
        // IPermissionEvaluator, both request-scoped. A Singleton registration here would repeat
        // exactly the captive-dependency bug already fixed once in InventoryDbContext's tenant
        // filter (docs/27 §11.2 row 1) - freezing the FIRST request's scoped instances into a
        // handler reused by every later request/tenant.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }

    /// <summary>
    /// Task 3.1-3.6: ASP.NET Core Identity (task 3.1) over the custom <see cref="UserStore"/>,
    /// with cookie authentication configured to the exact flags docs/08 §3 specifies. Security
    /// stamp validation (task 3.6) runs on every request via
    /// <c>SecurityStampValidator.ValidatePrincipalAsync</c> wired through
    /// <see cref="CookieAuthenticationEvents.OnValidatePrincipal"/>.
    /// </summary>
    /// <remarks>
    /// Found via real-browser testing (Phase F2), not the C# integration suite: a `__Host-`
    /// prefixed cookie is REJECTED BY THE BROWSER OUTRIGHT unless it also carries `Secure`, and
    /// `Secure` cookies require a secure (HTTPS) context to be stored at all - not merely to be
    /// re-transmitted later. docs/19 §1 mandates plain `http://localhost:5165` for local dev, so
    /// the unconditional `__Host-InventorySession` + `SecurePolicy.Always` combination silently
    /// discarded the cookie in every real browser, while a 200 from `/login` looked identical to
    /// success. The existing `WebApplicationFactory`-based tests never caught this because they
    /// deliberately fake an `https://` base address to satisfy `Secure` cookie storage inside
    /// `TestServer` - a workaround that has no equivalent for an actual browser hitting the real
    /// local process. Development now uses a plain, non-`Secure` cookie name instead of
    /// swallowing the mismatch; Production/Staging keep the original `__Host-` name and
    /// `Secure.Always`, matching docs/20 §1's TLS-terminated topology exactly.
    /// </remarks>
    private static void AddAuthentication(IServiceCollection services, IHostEnvironment environment)
    {
        services.AddIdentityCore<User>(options =>
            {
                // Password/complexity policy is not specified by docs/08 - ASP.NET Core
                // Identity's own defaults (length 6, requires digit/upper/lower/non-alnum) are
                // used unless a future spec revises them. Lockout: docs/08 says "Reset Failed
                // Counter" but never states a threshold or duration, so a conservative,
                // undisputed default is set here - 5 attempts, 15-minute lockout.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = false;
                // Product decision (2026-09-20, explicit request by the project owner): password policy
                // relaxed to a 5-character minimum with no complexity requirements.
                options.Password.RequiredLength = 5;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
            })
            .AddUserStore<UserStore>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // AddIdentityCore (deliberately, for an API-only host - unlike AddIdentity) registers
        // no authentication scheme by itself. The cookie scheme is added explicitly here, under
        // Identity's own well-known scheme name so SignInManager's SignInAsync/SignOutAsync
        // target it without extra configuration.
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = environment.IsDevelopment() ? DevelopmentSessionCookieName : SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;

                // An unauthenticated request to a protected endpoint gets a 401/403 JSON
                // response, never an HTML login-page redirect - this is an API, not a
                // server-rendered app.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                // Task 3.6: security-stamp validation on every request - rotation on password
                // change or role/scope change invalidates every live cookie immediately, not
                // just future ones. Runs at a fixed interval (not literally every request) per
                // ASP.NET Core's own recommended pattern, trading a few seconds of staleness
                // for not hitting the database on every single call.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<User>>();
                    User? validatedUser = await signInManager.ValidateSecurityStampAsync(context.Principal);
                    if (validatedUser is null)
                    {
                        context.RejectPrincipal();
                        await signInManager.SignOutAsync();
                    }
                };
            });

        services.AddAuthorization();
    }

    /// <summary>
    /// Task 3.7: anti-forgery over a header (this is an API, not a form-posting app), so the
    /// frontend's central API client can attach the value docs/08 §4 names,
    /// <c>X-CSRF-TOKEN</c>, to every mutating request. The pairing cookie is separate from the
    /// session cookie and does not require authentication to obtain.
    /// </summary>
    /// <remarks>
    /// Same real-browser finding as <see cref="AddAuthentication"/>'s remarks: a `__Host-`
    /// prefixed cookie needs `Secure` to be stored at all, which needs HTTPS, which docs/19 §1's
    /// local dev does not use. The previous `SameAsRequest` policy avoided ASP.NET Core's
    /// antiforgery system THROWING when asked to issue a Secure cookie over plain HTTP, but the
    /// `__Host-` prefix on the cookie NAME still triggered the browser's own storage rejection
    /// regardless of the resulting Secure flag being off - confirmed by a real POST failing CSRF
    /// validation end to end (`400 CSRF_TOKEN_INVALID`) despite the token being fetched and
    /// attached correctly. Development drops the prefix entirely instead of relying on a cookie
    /// attribute the browser was never going to honour for a `__Host-` name anyway.
    /// </remarks>
    private static void AddCsrf(IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = environment.IsDevelopment() ? "InventoryCsrf" : "__Host-InventoryCsrf";
            // The frontend never reads this cookie - it echoes back the separate RequestToken
            // value GET /auth/csrf-token returns in its JSON body, in the X-CSRF-TOKEN header.
            // The cookie itself only needs to travel automatically with the browser; nothing
            // reads it via JS, so HttpOnly stays on for defense in depth.
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
    }

    /// <summary>
    /// Task 3.13: CORS restricted to the configured frontend origin, credentials allowed, no
    /// wildcard. An empty/missing configuration value deliberately allows no origin at all
    /// rather than silently falling back to a permissive default.
    /// </summary>
    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        string? allowedOrigin = configuration[CorsAllowedOriginKey];

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (!string.IsNullOrWhiteSpace(allowedOrigin))
                {
                    policy.WithOrigins(allowedOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });
    }
}
