using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>A person who signs in. Mobile-number identified (docs/08), not email.</summary>
public sealed class User : Entity, ITenantScopedEntity
{
    private User()
    {
    }

    public User(Guid companyId, string fullName, string mobileNumber, string passwordHash, string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        FullName = fullName;
        MobileNumber = mobileNumber;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        IsActive = true;
        AccessFailedCount = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string MobileNumber { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordFailedLogin(int lockoutThreshold, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= lockoutThreshold)
        {
            LockoutEndAt = DateTimeOffset.UtcNow.Add(lockoutDuration);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RotateSecurityStamp(string newStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStamp);
        SecurityStamp = newStamp;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangePasswordHash(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
