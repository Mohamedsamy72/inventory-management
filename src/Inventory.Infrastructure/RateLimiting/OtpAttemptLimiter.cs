using System.Threading.RateLimiting;
using Inventory.Application.Auth;

namespace Inventory.Infrastructure.RateLimiting;

/// <summary>Singleton (one process-lifetime pair of counters) - see the interface doc for why
/// this exists separately from the ASP.NET Core rate-limiting middleware.</summary>
public sealed class OtpAttemptLimiter : IOtpAttemptLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _per15Minutes = PartitionedRateLimiter.Create<string, string>(
        mobile => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: mobile,
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            }));

    private readonly PartitionedRateLimiter<string> _perHour = PartitionedRateLimiter.Create<string, string>(
        mobile => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: mobile,
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));

    public bool TryRecordAttempt(string normalizedMobileNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedMobileNumber);

        RateLimitLease fifteenMinuteLease = _per15Minutes.AttemptAcquire(normalizedMobileNumber);
        RateLimitLease hourLease = _perHour.AttemptAcquire(normalizedMobileNumber);

        return fifteenMinuteLease.IsAcquired && hourLease.IsAcquired;
    }

    public void Dispose()
    {
        _per15Minutes.Dispose();
        _perHour.Dispose();
    }
}
