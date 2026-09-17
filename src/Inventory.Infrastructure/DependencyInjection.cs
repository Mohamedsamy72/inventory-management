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

    /// <summary>The __Host- prefixed session cookie name (docs/08 §3).</summary>
    public const string SessionCookieName = "__Host-InventorySession";

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
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        AddAuthentication(services);
        AddCors(services, configuration);
        AddCsrf(services);

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
    private static void AddAuthentication(IServiceCollection services)
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
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
    private static void AddCsrf(IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-InventoryCsrf";
            // The frontend never reads this cookie - it echoes back the separate RequestToken
            // value GET /auth/csrf-token returns in its JSON body, in the X-CSRF-TOKEN header.
            // The cookie itself only needs to travel automatically with the browser; nothing
            // reads it via JS, so HttpOnly stays on for defense in depth.
            options.Cookie.HttpOnly = true;
            // SameAsRequest, not Always: ASP.NET Core's antiforgery system (unlike the cookie
            // authentication handler used for the session cookie) actively THROWS if asked to
            // issue a Secure cookie over a non-HTTPS request - and local dev runs over plain
            // HTTP (docs/19 §1: http://localhost:5165). SameAsRequest still sets Secure under
            // real HTTPS (production, behind TLS termination) while not hard-crashing this
            // auxiliary CSRF-pairing cookie locally. docs/08 §3's unconditional "Secure" example
            // is for the __Host-InventorySession cookie specifically, which keeps Always.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
