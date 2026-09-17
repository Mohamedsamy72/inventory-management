using Inventory.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Sms;

/// <summary>
/// Development-only <see cref="ISmsSender"/>: writes to the console log instead of dispatching
/// a real SMS (OD-001). A production deployment swaps this registration for a real gateway
/// client behind the same interface - nothing else changes.
/// </summary>
public sealed partial class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendAsync(string mobileNumber, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        LogDevSms(_logger, mobileNumber, message);

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[DEV SMS] To {MobileNumber}: {Message}")]
    private static partial void LogDevSms(ILogger logger, string mobileNumber, string message);
}
