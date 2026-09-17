using Inventory.Application.Auth;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Sms;

public sealed class PasswordResetOtpService : IPasswordResetOtpService
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

    private readonly InventoryDbContext _context;
    private readonly IOtpService _otpService;

    public PasswordResetOtpService(InventoryDbContext context, IOtpService otpService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _otpService = otpService ?? throw new ArgumentNullException(nameof(otpService));
    }

    public async Task<string> IssueAsync(Guid companyId, Guid userId, string? createdIp, CancellationToken cancellationToken)
    {
        string code = _otpService.GenerateCode();
        string hash = _otpService.Hash(code);

        var otp = new PasswordResetOtp(companyId, userId, hash, DateTimeOffset.UtcNow.Add(Expiry), createdIp);
        _context.PasswordResetOtps.Add(otp);
        await _context.SaveChangesAsync(cancellationToken);

        return code;
    }

    /// <summary>
    /// IgnoreQueryFilters for the same structural reason as <c>UserStore.FindByNameAsync</c>:
    /// this runs from the pre-authentication /forgot-password/verify endpoint, so
    /// ICurrentUserService.CompanyId still reads Guid.Empty here - the tenant filter would
    /// otherwise hide the real row for every caller and every outcome would collapse to
    /// NotFound (masking Expired/Invalid too). Safe to bypass because the lookup is scoped to
    /// a single userId, not client-supplied tenant data.
    /// </summary>
    public async Task<OtpVerificationOutcome> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        PasswordResetOtp? otp = await _context.PasswordResetOtps
            .IgnoreQueryFilters()
            .Where(o => o.UserId == userId && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return OtpVerificationOutcome.NotFound;
        }

        if (!otp.IsUsable)
        {
            return OtpVerificationOutcome.Expired;
        }

        if (!_otpService.Verify(code, otp.OtpHash))
        {
            otp.RecordFailedAttempt();
            await _context.SaveChangesAsync(cancellationToken);
            return OtpVerificationOutcome.Invalid;
        }

        otp.Consume();
        await _context.SaveChangesAsync(cancellationToken);
        return OtpVerificationOutcome.Valid;
    }
}
