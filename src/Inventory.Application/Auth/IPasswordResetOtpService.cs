namespace Inventory.Application.Auth;

public enum OtpVerificationOutcome
{
    Valid,
    Invalid,
    Expired,
    NotFound,
}

/// <summary>
/// Owns the full OTP lifecycle (docs/08 §2) - generation, hashed storage, and single-use
/// verification - behind one seam so Inventory.Api never touches
/// Inventory.Domain.Entities.PasswordResetOtp's persistence directly (ADR-002; only Program.cs
/// may name Inventory.Infrastructure).
/// </summary>
public interface IPasswordResetOtpService
{
    /// <summary>Generates, hashes, and stores a new OTP; returns the plaintext code to send via
    /// <see cref="ISmsSender"/> - the only place the plaintext ever exists outside the SMS
    /// gateway call itself.</summary>
    Task<string> IssueAsync(Guid companyId, Guid userId, string? createdIp, CancellationToken cancellationToken);

    /// <summary>Checks the code against the user's latest OTP. On <see cref="OtpVerificationOutcome.Valid"/>
    /// the OTP is immediately consumed (single-use - docs/08 §2 point 2) and cannot be verified again.</summary>
    Task<OtpVerificationOutcome> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken);
}
