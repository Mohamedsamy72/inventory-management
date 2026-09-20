using Inventory.Api.Errors;
using Inventory.Application.Auth;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Api.Features.Auth;

public sealed record OwnerPasswordVerifyRequest(string Code);

/// <summary>Never echoed, logged, or audited.</summary>
public sealed record OwnerPasswordChangeRequest(string ResetToken, string NewPassword);

public sealed record OwnerMobileOtpRequest(string NewMobileNumber);

public sealed record OwnerMobileConfirmRequest(string ChangeToken, string Code);

/// <summary>
/// The Owner's OWN security-sensitive account operations - deliberately separate from the
/// administrative "Owner resets another user's password" mechanism (which refuses the Owner's own
/// account). Both flows are OTP-gated, Owner-only, CSRF-protected and rate-limited:
/// <list type="bullet">
/// <item>Password: OTP to the Owner's CURRENTLY registered mobile -> verify -> new password.</item>
/// <item>Mobile number: OTP to the NEW number (proves possession) with the pending number carried
/// in a short-lived, tamper-proof, user-bound token - it only becomes the login/recovery number
/// after the code is verified.</item>
/// </list>
/// No mobile number is hard-coded anywhere; the number always comes from the account row.
/// </summary>
public static class AccountSecurityEndpoints
{
    private const string MobileChangePurpose = "Inventory.Account.MobileChange.v1";
    private static readonly TimeSpan MobileChangeTokenLifetime = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapAccountSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder account = app.MapGroup("/api/v1/account");

        account.MapPost("/password/otp", RequestPasswordOtpAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        account.MapPost("/password/verify", VerifyPasswordOtpAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        account.MapPost("/password/change", ChangePasswordAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        account.MapPost("/mobile/otp", RequestMobileOtpAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        account.MapPost("/mobile/confirm", ConfirmMobileAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<User?> CurrentOwnerAsync(HttpContext httpContext, UserManager<User> userManager, ICurrentUserService currentUser)
    {
        if (currentUser.Role != RoleName.Owner)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.OwnerOnlyOperation);
            return null;
        }

        User? user = await userManager.FindByIdAsync(currentUser.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.OwnerOnlyOperation);
            return null;
        }

        return user;
    }

    private static async Task<IResult> RequestPasswordOtpAsync(
        HttpContext httpContext, UserManager<User> userManager, ICurrentUserService currentUser,
        IOtpAttemptLimiter limiter, IPasswordResetOtpService otpService, ISmsSender smsSender)
    {
        User? owner = await CurrentOwnerAsync(httpContext, userManager, currentUser);
        if (owner is null)
        {
            return Results.Empty;
        }

        if (!limiter.TryRecordAttempt(owner.MobileNumber))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimitExceeded);
            return Results.Empty;
        }

        string code = await otpService.IssueAsync(owner.CompanyId, owner.Id, IpAddress(httpContext), httpContext.RequestAborted);
        await smsSender.SendAsync(owner.MobileNumber, $"رمز التحقق الخاص بك: {code}", httpContext.RequestAborted);
        return Results.Ok();
    }

    private static async Task<IResult> VerifyPasswordOtpAsync(
        OwnerPasswordVerifyRequest request, HttpContext httpContext, UserManager<User> userManager,
        ICurrentUserService currentUser, IPasswordResetOtpService otpService)
    {
        User? owner = await CurrentOwnerAsync(httpContext, userManager, currentUser);
        if (owner is null)
        {
            return Results.Empty;
        }

        OtpVerificationOutcome outcome = await otpService.VerifyAsync(owner.Id, request.Code ?? string.Empty, httpContext.RequestAborted);
        if (outcome == OtpVerificationOutcome.Valid)
        {
            return Results.Ok(new { resetToken = await userManager.GeneratePasswordResetTokenAsync(owner) });
        }

        await ProblemResponseWriter.WriteAsync(
            httpContext, StatusCodes.Status400BadRequest,
            outcome == OtpVerificationOutcome.Expired ? ErrorCodes.OtpExpired : ErrorCodes.OtpInvalid);
        return Results.Empty;
    }

    private static async Task<IResult> ChangePasswordAsync(
        OwnerPasswordChangeRequest request, HttpContext httpContext, UserManager<User> userManager,
        SignInManager<User> signInManager, ICurrentUserService currentUser, IAuditLogger auditLogger)
    {
        User? owner = await CurrentOwnerAsync(httpContext, userManager, currentUser);
        if (owner is null)
        {
            return Results.Empty;
        }

        // Recorded BEFORE the Identity calls: their internal SaveChanges flushes the audit row in
        // the same round trip. The entry carries no password, token, or code.
        auditLogger.Record(new AuditEntry(
            "OWNER_PASSWORD_CHANGED_WITH_OTP", nameof(User), owner.Id,
            "قام المالك بتغيير كلمة مروره بعد التحقق برمز OTP",
            OldValues: null, NewValues: null, AuditResult.Success));

        IdentityResult result = await userManager.ResetPasswordAsync(owner, request.ResetToken ?? string.Empty, request.NewPassword ?? string.Empty);
        if (!result.Succeeded)
        {
            bool policy = result.Errors.All(e => e.Code.StartsWith("Password", StringComparison.Ordinal));
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, policy ? ErrorCodes.InvalidPassword : ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        await userManager.UpdateSecurityStampAsync(owner);
        // Every OTHER session dies with the stamp; the Owner's current one is re-issued.
        await signInManager.RefreshSignInAsync(owner);
        return Results.NoContent();
    }

    private static async Task<IResult> RequestMobileOtpAsync(
        OwnerMobileOtpRequest request, HttpContext httpContext, UserManager<User> userManager, ICurrentUserService currentUser,
        IOtpAttemptLimiter limiter, IPasswordResetOtpService otpService, ISmsSender smsSender, IDataProtectionProvider dataProtection)
    {
        User? owner = await CurrentOwnerAsync(httpContext, userManager, currentUser);
        if (owner is null)
        {
            return Results.Empty;
        }

        string newMobile = MobileNumberNormalizer.Normalize(request.NewMobileNumber ?? string.Empty);
        if (newMobile.Length is < 10 or > 15)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.InvalidMobile);
            return Results.Empty;
        }

        if (await userManager.FindByNameAsync(newMobile) is not null)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status409Conflict, ErrorCodes.DuplicateMobile);
            return Results.Empty;
        }

        if (!limiter.TryRecordAttempt(owner.MobileNumber))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimitExceeded);
            return Results.Empty;
        }

        string code = await otpService.IssueAsync(owner.CompanyId, owner.Id, IpAddress(httpContext), httpContext.RequestAborted);
        await smsSender.SendAsync(newMobile, $"رمز التحقق الخاص بك: {code}", httpContext.RequestAborted);

        // The pending number is bound to THIS user and expires; the code alone is not enough to
        // change anything and the token alone is useless without the code.
        string changeToken = Protector(dataProtection).Protect($"{owner.Id:N}|{newMobile}", MobileChangeTokenLifetime);
        return Results.Ok(new { changeToken });
    }

    private static async Task<IResult> ConfirmMobileAsync(
        OwnerMobileConfirmRequest request, HttpContext httpContext, UserManager<User> userManager, ICurrentUserService currentUser,
        IPasswordResetOtpService otpService, IDataProtectionProvider dataProtection, IAuditLogger auditLogger, SignInManager<User> signInManager)
    {
        User? owner = await CurrentOwnerAsync(httpContext, userManager, currentUser);
        if (owner is null)
        {
            return Results.Empty;
        }

        string payload;
        try
        {
            payload = Protector(dataProtection).Unprotect(request.ChangeToken ?? string.Empty);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpExpired);
            return Results.Empty;
        }

        string[] parts = payload.Split('|');
        if (parts.Length != 2 || parts[0] != owner.Id.ToString("N"))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        string newMobile = parts[1];
        OtpVerificationOutcome outcome = await otpService.VerifyAsync(owner.Id, request.Code ?? string.Empty, httpContext.RequestAborted);
        if (outcome != OtpVerificationOutcome.Valid)
        {
            await ProblemResponseWriter.WriteAsync(
                httpContext, StatusCodes.Status400BadRequest,
                outcome == OtpVerificationOutcome.Expired ? ErrorCodes.OtpExpired : ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        // Re-check at confirm time: someone may have taken the number since the OTP was issued.
        User? holder = await userManager.FindByNameAsync(newMobile);
        if (holder is not null && holder.Id != owner.Id)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status409Conflict, ErrorCodes.DuplicateMobile);
            return Results.Empty;
        }

        string oldMobile = owner.MobileNumber;
        auditLogger.Record(new AuditEntry(
            "OWNER_MOBILE_CHANGED_WITH_OTP", nameof(User), owner.Id,
            "قام المالك بتغيير رقم جواله بعد التحقق برمز OTP",
            OldValues: new { MobileNumber = oldMobile }, NewValues: new { MobileNumber = newMobile }, AuditResult.Success));

        owner.UpdateProfile(owner.FullName, newMobile);
        IdentityResult update = await userManager.UpdateAsync(owner);
        if (!update.Succeeded)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status409Conflict, ErrorCodes.DuplicateMobile);
            return Results.Empty;
        }

        await userManager.UpdateSecurityStampAsync(owner);
        await signInManager.RefreshSignInAsync(owner);
        return Results.NoContent();
    }

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider) =>
        provider.CreateProtector(MobileChangePurpose).ToTimeLimitedDataProtector();

    private static string IpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
