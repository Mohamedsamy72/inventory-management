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
    public const string NonGrantablePermission = "NON_GRANTABLE_PERMISSION";
    public const string ForbiddenScope = "FORBIDDEN_SCOPE";

    /// <summary>Not in docs/13's original catalogue - added for task 4.8 (docs/09 §2.4's
    /// privilege-escalation guards: no self role/scope change, only Owner creates Owner, Admin
    /// cannot grant beyond its own bounds). Recorded in docs/27 §12 as a deliberate catalogue
    /// addition, and added to docs/13 §2 itself, not just here - docs/13 §4 already documents
    /// this project's practice of extending the catalogue as a real requirement needs a code.</summary>
    public const string PrivilegeEscalationDenied = "PRIVILEGE_ESCALATION_DENIED";

    /// <summary>Also new for the same reason - ASP.NET Core Identity's password validator can
    /// reject user creation (task 4.14) for reasons docs/13 has no existing code for.</summary>
    public const string InvalidPassword = "INVALID_PASSWORD";

    public static string ArabicMessageFor(string code) => code switch
    {
        InvalidCredentials => "رقم الجوال أو كلمة المرور غير صحيحة.",
        AccountInactive => "هذا الحساب معطل، يرجى مراجعة إدارة النظام.",
        OtpExpired => "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد.",
        OtpInvalid => "رمز التحقق المدخل غير صحيح.",
        RateLimitExceeded => "تجاوزت الحد المسموح من المحاولات، يرجى الانتظار والمحاولة لاحقاً.",
        NonGrantablePermission => "هذه الصلاحية مخصصة للمالك فقط ولا يمكن منحها لأي مستخدم.",
        ForbiddenScope => "ليس لديك صلاحية للوصول إلى هذا المستودع أو الفرع.",
        PrivilegeEscalationDenied => "لا يمكنك تنفيذ هذا الإجراء، فهو يتجاوز الصلاحيات الممنوحة لك.",
        InvalidPassword => "كلمة المرور لا تحقق متطلبات القوة المطلوبة.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };

    public static string EnglishMessageFor(string code) => code switch
    {
        InvalidCredentials => "Invalid mobile number or password.",
        AccountInactive => "Account is deactivated.",
        OtpExpired => "OTP code has expired.",
        OtpInvalid => "Incorrect verification code.",
        RateLimitExceeded => "Too many requests. Please wait.",
        NonGrantablePermission => "This permission cannot be assigned.",
        ForbiddenScope => "You are not authorized for this location.",
        PrivilegeEscalationDenied => "This action exceeds your granted authority.",
        InvalidPassword => "Password does not meet the required complexity.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };
}
