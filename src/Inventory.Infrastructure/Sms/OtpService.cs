using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Inventory.Application.Auth;

namespace Inventory.Infrastructure.Sms;

/// <summary>SHA-256 is sufficient here, unlike passwords: a 6-digit OTP is single-use,
/// 5-minute-lived, and rate-limited at both the mobile and IP level (docs/08 §2/§5) - it does
/// not need to resist years of offline brute force the way a password hash does.</summary>
public sealed class OtpService : IOtpService
{
    public string GenerateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public string Hash(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string code, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        byte[] computed = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        byte[] expected = Convert.FromHexString(hash);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
