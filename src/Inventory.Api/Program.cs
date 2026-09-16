using Inventory.Api.Middleware;
using Inventory.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

// ---------------------------------------------------------------------------
//  Inventory.Api — composition root.
//
//  Phase 1 deliberately hosts no business endpoint. It establishes the pipeline
//  every later phase plugs into: structured logging, correlation, RFC 7807 error
//  handling, health probes, and Development-only OpenAPI.
//  See docs/09-implementation-plan.md, Phase 1.
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// ---- Structured logging (docs/21 section 3) -------------------------------
// JSON to stdout so a log shipper can consume it without a parser. Redaction of
// passwords, OTPs and Authorization headers is enforced at the call sites that
// introduce them (Phase 3); nothing in Phase 1 logs a credential.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Inventory.Api")
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// ---- Services -------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI document only, and only in Development (docs/32 CR-098).
// The interactive UI arrives in Phase 3 with the first real endpoints; a UI over
// an empty document would be decoration, not verification.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

// ---- Pipeline -------------------------------------------------------------
app.UseExceptionHandler();

// Correlation must wrap everything downstream so that every log line and every
// error response carries the same id.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    // ElapsedMs is emitted by the sink itself; this adds the request shape.
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
    };
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ---- Health probes (docs/32 CR-064) ---------------------------------------
// Both are unauthenticated, so both disclose liveness and nothing else: no
// version, no environment name, no schema detail, no exception text.

// Liveness: is the process up and serving?
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = static async (context, _) =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Healthy");
    },
});

// Readiness: can it reach its database? Reports Unhealthy until PostgreSQL 16+
// is installed and configured, which is the truthful answer (docs/33 section 4.3).
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static registration => registration.Tags.Contains(DependencyInjection.ReadinessTag),
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(report.Status.ToString());
    },
});

await app.RunAsync();

/// <summary>
/// Exposed so that <c>WebApplicationFactory&lt;Program&gt;</c> can host this API in
/// integration tests. Top-level statements otherwise generate an internal entry point.
/// </summary>
public partial class Program;
