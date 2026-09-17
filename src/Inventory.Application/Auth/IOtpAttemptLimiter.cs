namespace Inventory.Application.Auth;

/// <summary>
/// The two per-mobile-number OTP rate-limit windows that ASP.NET Core's rate-limiting
/// middleware cannot enforce on its own, because it runs before model binding parses the mobile
/// number out of the request body (docs/08 §5, ADR-026/CR-060): 3 requests per mobile per 15
/// minutes AND 5 per mobile per hour, both independently, in addition to (not instead of) the
/// per-IP window the middleware handles.
/// </summary>
public interface IOtpAttemptLimiter
{
    /// <summary>True if both windows still have room; also records this attempt.</summary>
    bool TryRecordAttempt(string normalizedMobileNumber);
}
