using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>A hashed, single-use, expiring OTP (docs/08). The plaintext code is never stored
/// and never returned in any response - only its hash lives here.</summary>
public sealed class PasswordResetOtp : Entity, ITenantScopedEntity
{
    private PasswordResetOtp()
    {
    }

    public PasswordResetOtp(Guid companyId, Guid userId, string otpHash, DateTimeOffset expiresAt, string? createdIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otpHash);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        OtpHash = otpHash;
        AttemptCount = 0;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedIp = createdIp;
    }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public string OtpHash { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedIp { get; private set; }

    public bool IsUsable => ConsumedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    public void RecordFailedAttempt() => AttemptCount++;

    public void Consume()
    {
        if (ConsumedAt is not null)
        {
            throw new InvalidOperationException($"PasswordResetOtp {Id} was already consumed - OTPs are single-use.");
        }

        ConsumedAt = DateTimeOffset.UtcNow;
    }
}
