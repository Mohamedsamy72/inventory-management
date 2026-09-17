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

    // Phase 5 (docs/28, docs/13 §2 pre-existing rows unless noted).
    public const string GeneratedFieldNotAccepted = "GENERATED_FIELD_NOT_ACCEPTED";
    public const string SequenceExhausted = "SEQUENCE_EXHAUSTED";
    public const string ServingWarehouseUnavailable = "SERVING_WAREHOUSE_UNAVAILABLE";
    public const string DuplicateItemName = "DUPLICATE_ITEM_NAME";

    /// <summary>New for Phase 5: docs/13's `DUPLICATE_ITEM_NAME` is item-specific by name and by
    /// Arabic wording ("صنف" = item). Category/Unit/Warehouse/Restaurant have their own per-
    /// company uniqueness constraints (docs/06, the real migration's `uq_categories_company_name`
    /// etc.) but no dedicated code - this one is deliberately generic instead of inventing four
    /// more near-duplicate codes.</summary>
    public const string DuplicateName = "DUPLICATE_NAME";

    /// <summary>New for Phase 5 (ADR-023, task 5.11): an item's base unit is immutable once any
    /// `stock_ledger` row exists for it. No prior code covers this.</summary>
    public const string BaseUnitImmutable = "BASE_UNIT_IMMUTABLE";

    /// <summary>Phase 6, task 6.6 - already in docs/13's original catalogue.</summary>
    public const string ConversionNotDefined = "CONVERSION_NOT_DEFINED";
    public const string InvalidConversionFactor = "INVALID_CONVERSION_FACTOR";

    // Phase 7 (docs/30 §6) - both already in docs/13's original catalogue.
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyReuse = "IDEMPOTENCY_KEY_REUSE";

    // Phase 8 (docs/30, docs/04 §18) - all already in docs/13's original catalogue.
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string EmptyDocument = "EMPTY_DOCUMENT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string FinancialAccessDenied = "FINANCIAL_ACCESS_DENIED";

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
        GeneratedFieldNotAccepted => "لا يمكن إدخال أرقام المستندات يدوياً — يتم توليدها تلقائياً بواسطة النظام.",
        SequenceExhausted => "تم استنفاد نطاق ترقيم المستندات، يرجى مراجعة إدارة النظام.",
        ServingWarehouseUnavailable => "لا يوجد مستودع خدمة مفعّل لهذا الفرع، يرجى مراجعة إدارة النظام.",
        DuplicateItemName => "يوجد صنف مسجل مسبقاً بنفس الاسم العربي في المنشأة.",
        DuplicateName => "يوجد سجل مسجل مسبقاً بنفس الاسم أو الرمز في المنشأة.",
        BaseUnitImmutable => "لا يمكن تغيير وحدة القياس الأساسية بعد وجود حركات مخزنية لهذا الصنف.",
        ConversionNotDefined => "لا يوجد معامل تحويل مفعّل لهذه الوحدة لهذا الصنف.",
        InvalidConversionFactor => "معامل التحويل يجب أن يكون قيمة موجبة أكبر من الصفر.",
        IdempotencyKeyRequired => "مفتاح منع التكرار مطلوب لهذه العملية.",
        IdempotencyKeyReuse => "تم استخدام مفتاح منع التكرار هذا لعملية مختلفة.",
        InsufficientStock => "الرصيد المتاح في المستودع غير كافٍ لإتمام العملية.",
        InvalidStateTransition => "لا يمكن تغيير حالة المستند من حالته الحالية.",
        EmptyDocument => "يجب أن يحتوي المستند على صنف واحد على الأقل.",
        ConcurrencyConflict => "تم تعديل السجل بواسطة مستخدم آخر، يرجى تحديث الصفحة والمحاولة مجدداً.",
        FinancialAccessDenied => "غير مصرح لك بالاطلاع على التكاليف والبيانات المالية.",
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
        GeneratedFieldNotAccepted => "Generated identifiers cannot be submitted.",
        SequenceExhausted => "Document numbering range exhausted.",
        ServingWarehouseUnavailable => "The branch has no active serving warehouse.",
        DuplicateItemName => "An item with this Arabic name already exists.",
        DuplicateName => "A record with this name or code already exists.",
        BaseUnitImmutable => "The base unit cannot change once stock ledger entries exist for this item.",
        ConversionNotDefined => "No active conversion for this unit.",
        InvalidConversionFactor => "Conversion factor must be positive.",
        IdempotencyKeyRequired => "Idempotency key is required.",
        IdempotencyKeyReuse => "Key already used for a different request.",
        InsufficientStock => "Available warehouse stock is insufficient.",
        InvalidStateTransition => "Document cannot transition from current state.",
        EmptyDocument => "Document must contain at least one line.",
        ConcurrencyConflict => "Record was modified by another user.",
        FinancialAccessDenied => "Financial data access is restricted to Owner.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };
}
