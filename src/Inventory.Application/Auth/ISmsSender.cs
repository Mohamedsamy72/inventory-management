namespace Inventory.Application.Auth;

/// <summary>
/// Dispatches an SMS to a mobile number (docs/08 §2 point 3, OD-001). The OTP itself is never
/// returned in any HTTP response - this is the only channel it leaves the server through.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string mobileNumber, string message, CancellationToken cancellationToken);
}
