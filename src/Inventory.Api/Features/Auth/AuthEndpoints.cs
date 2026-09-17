using Inventory.Api.Errors;
using Inventory.Api.RateLimiting;
using Inventory.Application.Auth;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.Features.Auth;

public sealed record LoginRequest(string MobileNumber, string Password);
public sealed record ForgotPasswordOtpRequest(string MobileNumber);
public sealed record ForgotPasswordVerifyRequest(string MobileNumber, string Code);
public sealed record ForgotPasswordResetRequest(string MobileNumber, string ResetToken, string NewPassword);

/// <summary>Phase 3 (docs/09 tasks 3.2, 3.5, 3.7, 3.9, 3.10, 3.12) - mobile+password login over
/// hardened cookie sessions, OTP password reset, and the authenticated identity endpoint.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder auth = app.MapGroup("/api/v1/auth");

        auth.MapPost("/login", LoginAsync)
            .RequireRateLimiting(RateLimitingExtensions.LoginPolicy)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        auth.MapPost("/logout", LogoutAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        auth.MapGet("/csrf-token", GetCsrfTokenAsync);
        auth.MapPost("/forgot-password/otp", RequestOtpAsync)
            .RequireRateLimiting(RateLimitingExtensions.OtpIpPolicy)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        auth.MapPost("/forgot-password/verify", VerifyOtpAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        auth.MapPost("/forgot-password/reset", ResetPasswordAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/account/me", GetAccountMeAsync).RequireAuthorization();

        return app;
    }

    /// <summary>Tasks 3.2-3.4: issues the session cookie on success; rejects inactive accounts
    /// with ACCOUNT_INACTIVE before even checking the password; increments/locks out on failure
    /// and resets on success via <see cref="SignInManager{TUser}.CheckPasswordSignInAsync"/>,
    /// which persists lockout state through the registered <c>IUserLockoutStore</c>.</summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager)
    {
        User? user = await userManager.FindByNameAsync(request.MobileNumber);
        if (user is null)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
            return Results.Empty;
        }

        if (!user.IsActive)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.AccountInactive);
            return Results.Empty;
        }

        SignInResult result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
            return Results.Empty;
        }

        user.RecordSuccessfulLogin();
        await userManager.UpdateAsync(user);

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok();
    }

    private static async Task<IResult> LogoutAsync(SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    /// <summary>Task 3.7 (docs/08 §4, CR-063). The frontend fetches this once at application
    /// boot and attaches the value to every mutating request's X-CSRF-TOKEN header.</summary>
    private static IResult GetCsrfTokenAsync(HttpContext httpContext, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new { csrfToken = tokens.RequestToken });
    }

    /// <summary>
    /// Task 3.9. Deliberately returns 200 whether or not the mobile number matches an active
    /// account - a different response shape here would let an attacker enumerate registered
    /// mobile numbers. The per-mobile rate-limit windows (docs/08 §5, ADR-026) are checked
    /// before any database lookup, so probing costs the same 429 regardless of account
    /// existence.
    /// </summary>
    private static async Task<IResult> RequestOtpAsync(
        ForgotPasswordOtpRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        IOtpAttemptLimiter otpAttemptLimiter,
        IPasswordResetOtpService otpService,
        ISmsSender smsSender)
    {
        string normalizedMobile = MobileNumberNormalizer.Normalize(request.MobileNumber);

        if (!otpAttemptLimiter.TryRecordAttempt(normalizedMobile))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimitExceeded);
            return Results.Empty;
        }

        User? user = await userManager.FindByNameAsync(normalizedMobile);
        if (user is not null && user.IsActive)
        {
            string code = await otpService.IssueAsync(user.CompanyId, user.Id, IpAddress(httpContext), httpContext.RequestAborted);
            await smsSender.SendAsync(user.MobileNumber, $"رمز التحقق الخاص بك: {code}", httpContext.RequestAborted);
        }

        return Results.Ok();
    }

    /// <summary>Task 3.10, first half. The OTP is consumed here (single-use, docs/08 §2 point
    /// 2) and a short-lived Identity password-reset token is handed back for the immediately
    /// following /reset call - the OTP code itself cannot be replayed against /reset.</summary>
    private static async Task<IResult> VerifyOtpAsync(
        ForgotPasswordVerifyRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        IPasswordResetOtpService otpService)
    {
        User? user = await userManager.FindByNameAsync(request.MobileNumber);
        if (user is null)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        OtpVerificationOutcome outcome = await otpService.VerifyAsync(user.Id, request.Code, httpContext.RequestAborted);

        switch (outcome)
        {
            case OtpVerificationOutcome.Valid:
                string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                return Results.Ok(new { resetToken });

            case OtpVerificationOutcome.Expired:
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpExpired);
                return Results.Empty;

            case OtpVerificationOutcome.Invalid:
            case OtpVerificationOutcome.NotFound:
            default:
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpInvalid);
                return Results.Empty;
        }
    }

    /// <summary>Task 3.10, second half. Rotates the security stamp explicitly (docs/08 §3) so
    /// every cookie issued before this password change is invalidated on its next request,
    /// even though ResetPasswordAsync already does this internally - asserted here rather than
    /// relied upon, since that is an internal Identity implementation detail this spec's
    /// requirement should not silently depend on.</summary>
    private static async Task<IResult> ResetPasswordAsync(
        ForgotPasswordResetRequest request,
        HttpContext httpContext,
        UserManager<User> userManager)
    {
        User? user = await userManager.FindByNameAsync(request.MobileNumber);
        if (user is null)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, request.ResetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.OtpInvalid);
            return Results.Empty;
        }

        await userManager.UpdateSecurityStampAsync(user);
        return Results.Ok();
    }

    /// <summary>Task 3.12.</summary>
    private static async Task<IResult> GetAccountMeAsync(
        HttpContext httpContext,
        IAccountProfileReader profileReader)
    {
        string? userIdClaim = httpContext.User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Results.Unauthorized();
        }

        AccountProfile? profile = await profileReader.GetProfileAsync(userId, httpContext.RequestAborted);
        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }

    private static string IpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
