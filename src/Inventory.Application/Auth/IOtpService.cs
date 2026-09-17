namespace Inventory.Application.Auth;

/// <summary>Generates and verifies the 6-digit password-reset OTP (docs/08 §2).</summary>
public interface IOtpService
{
    /// <summary>A cryptographically random 6-digit numeric code.</summary>
    string GenerateCode();

    /// <summary>Hashes a code for storage - the plaintext code is never persisted.</summary>
    string Hash(string code);

    /// <summary>Constant-time comparison against a stored hash.</summary>
    bool Verify(string code, string hash);
}
