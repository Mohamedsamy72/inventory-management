using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Middleware;

/// <summary>
/// Converts any unhandled exception into an RFC 7807 <see cref="ProblemDetails"/> response
/// carrying an Arabic, user-facing message.
/// </summary>
/// <remarks>
/// Two rules govern this handler (docs/13 section 3):
/// <list type="number">
///   <item>
///     The response is sanitised. Exception type, message, stack trace and SQL text are
///     logged server-side with the correlation id and never cross the wire — an error
///     response is an excellent place to leak a schema, so it carries none.
///   </item>
///   <item>
///     The user-facing text is Arabic (<c>messageAr</c>). <c>messageEn</c> is a developer
///     diagnostic and is omitted entirely outside Development (docs/31 section 4.1, CR-072).
///   </item>
/// </list>
/// <para>
/// This handler covers <em>unexpected</em> failures only. Business rule violations get
/// specific codes from the <c>docs/13</c> catalog and are mapped by the phases that
/// introduce them; inventing those codes here, before any rule exists, would be guessing.
/// </para>
/// </remarks>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private const string UnexpectedErrorCode = "UNEXPECTED_ERROR";
    private const string UnexpectedMessageAr = "حدث خطأ غير متوقع، يرجى المحاولة مرة أخرى أو التواصل مع إدارة النظام.";
    private const string UnexpectedMessageEn = "An unexpected error occurred.";

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the handler.</summary>
    /// <param name="logger">Logger used for the full, unsanitised server-side record.</param>
    /// <param name="environment">Host environment, used to gate developer diagnostics.</param>
    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        string correlationId =
            httpContext.Items[CorrelationIdMiddleware.HeaderName] as string
            ?? httpContext.TraceIdentifier;

        LogUnhandledException(
            _logger,
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty,
            correlationId);

        var problem = new ProblemDetails
        {
            Type = $"https://errors.inventory.local/{UnexpectedErrorCode}",
            Title = "Unexpected Error",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path.Value,
        };

        problem.Extensions["code"] = UnexpectedErrorCode;
        problem.Extensions["messageAr"] = UnexpectedMessageAr;
        problem.Extensions["correlationId"] = correlationId;

        if (_environment.IsDevelopment())
        {
            problem.Extensions["messageEn"] = UnexpectedMessageEn;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception for {Method} {Path}. CorrelationId={CorrelationId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string method,
        string path,
        string correlationId);
}
