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

        string normalizedMobileNumber = MobileNumberNormalizer.Normalize(mobileNumber);
        if (normalizedMobileNumber.Length == 0)
        {
            throw new ArgumentException("Mobile number must contain at least one digit.", nameof(mobileNumber));
        }

        Id = Guid.NewGuid();
        CompanyId = companyId;
        FullName = fullName;
        MobileNumber = normalizedMobileNumber;
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

    /// <summary>
    /// Raw increment only - deciding whether the new count crosses the lockout threshold and
    /// calling <see cref="SetLockoutEnd"/> belongs to the caller (docs/08 §1's "Reset Failed
    /// Counter" step is orchestrated by ASP.NET Core Identity's UserManager against
    /// IdentityOptions.Lockout, not by this entity - mirroring IUserLockoutStore's granular
    /// shape rather than baking the threshold into the domain).
    /// </summary>
    public int IncrementAccessFailedCount()
    {
        AccessFailedCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return AccessFailedCount;
    }

    public void ResetAccessFailedCount()
    {
        AccessFailedCount = 0;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetLockoutEnd(DateTimeOffset? lockoutEnd)
    {
        LockoutEndAt = lockoutEnd;
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
