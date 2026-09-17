namespace Inventory.Api.Errors;

/// <summary>The docs/13 §2 Arabic error catalogue entries this phase introduces. Not the whole
/// catalogue - only the codes an endpoint that exists today can actually return.</summary>
internal static class ErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpInvalid = "OTP_INVALID";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    public static string ArabicMessageFor(string code) => code switch
    {
        InvalidCredentials => "رقم الجوال أو كلمة المرور غير صحيحة.",
        AccountInactive => "هذا الحساب معطل، يرجى مراجعة إدارة النظام.",
        OtpExpired => "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد.",
        OtpInvalid => "رمز التحقق المدخل غير صحيح.",
        RateLimitExceeded => "تجاوزت الحد المسموح من المحاولات، يرجى الانتظار والمحاولة لاحقاً.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };

    public static string EnglishMessageFor(string code) => code switch
    {
        InvalidCredentials => "Invalid mobile number or password.",
        AccountInactive => "Account is deactivated.",
        OtpExpired => "OTP code has expired.",
        OtpInvalid => "Incorrect verification code.",
        RateLimitExceeded => "Too many requests. Please wait.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };
}
