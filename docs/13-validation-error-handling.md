# 13 — Validation & Error Handling Architecture

> **Document ID:** SPEC-13  
> **Topic:** Server Validation Engine, FluentValidation Rules, and Standardized Arabic Error Catalog

---

## 1. Validation Architecture & Authority

1. **Server Authority:** Validation in the browser (React Hook Form / Zod) exists purely for real-time UI feedback. The backend API validates every rule independently.
2. **FluentValidation Integration:** Command DTOs in `Inventory.Application` are validated via FluentValidation before reaching domain handlers.
3. **No Silent Business Math:** Quantities, variances, base quantities, and cost averages are computed by domain services. Client-submitted calculated numbers are ignored.

---

## 2. Standard Arabic Error Catalog (RFC 7807)

| Error Code | HTTP Status | English Message | Arabic Message (User-Facing) |
| :--- | :---: | :--- | :--- |
| `INVALID_CREDENTIALS` | 401 | Invalid mobile number or password. | رقم الجوال أو كلمة المرور غير صحيحة. |
| `ACCOUNT_INACTIVE` | 403 | Account is deactivated. | هذا الحساب معطل، يرجى مراجعة إدارة النظام. |
| `INSUFFICIENT_STOCK` | 400 | Available warehouse stock is insufficient. | الرصيد المتاح في المستودع غير كافٍ لإتمام العملية. |
| `FORBIDDEN_SCOPE` | 403 | You are not authorized for this location. | ليس لديك صلاحية للوصول إلى هذا المستودع أو الفرع. |
| `FINANCIAL_ACCESS_DENIED` | 403 | Financial data access is restricted to Owner. | غير مصرح لك بالاطلاع على التكاليف والبيانات المالية. |
| `DUPLICATE_ITEM_NAME` | 409 | An item with this Arabic name already exists. | يوجد صنف مسجل مسبقاً بنفس الاسم العربي في المنشأة. |
| `INVALID_QUANTITY` | 400 | Quantity must be greater than zero. | يجب أن تكون الكمية المدخلة أكبر من الصفر. |
| `INVALID_STATE_TRANSITION` | 400 | Document cannot transition from current state. | لا يمكن تغيير حالة المستند من حالته الحالية. |
| `ALREADY_CONFIRMED` | 409 | This supply has already been confirmed. | تم تأكيد استلام هذه التوريدة مسبقاً ولا يمكن تكرار العملية. |
| `INVALID_CONVERSION_FACTOR`| 400 | Conversion factor must be positive. | معامل التحويل يجب أن يكون قيمة موجبة أكبر من الصفر. |
| `OTP_EXPIRED` | 400 | OTP code has expired. | انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد. |
| `OTP_INVALID` | 400 | Incorrect verification code. | رمز التحقق المدخل غير صحيح. |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests. Please wait. | تجاوزت الحد المسموح من المحاولات، يرجى الانتظار والمحاولة لاحقاً. |
| `CONCURRENCY_CONFLICT` | 409 | Record was modified by another user. | تم تعديل السجل بواسطة مستخدم آخر، يرجى تحديث الصفحة والمحاولة مجدداً. |
| `SERVING_WAREHOUSE_UNAVAILABLE` | 409 | The branch has no active serving warehouse. | لا يوجد مستودع خدمة مفعّل لهذا الفرع، يرجى مراجعة إدارة النظام. |
| `GENERATED_FIELD_NOT_ACCEPTED` | 400 | Generated identifiers cannot be submitted. | لا يمكن إدخال أرقام المستندات يدوياً — يتم توليدها تلقائياً بواسطة النظام. |
| `NON_GRANTABLE_PERMISSION` | 400 | This permission cannot be assigned. | هذه الصلاحية مخصصة للمالك فقط ولا يمكن منحها لأي مستخدم. |
| `IDEMPOTENCY_KEY_REQUIRED` | 400 | Idempotency key is required. | مفتاح منع التكرار مطلوب لهذه العملية. |
| `IDEMPOTENCY_KEY_REUSE` | 409 | Key already used for a different request. | تم استخدام مفتاح منع التكرار هذا لعملية مختلفة. |
| `CONVERSION_NOT_DEFINED` | 409 | No active conversion for this unit. | لا يوجد معامل تحويل مفعّل لهذه الوحدة لهذا الصنف. |
| `SEQUENCE_EXHAUSTED` | 409 | Document numbering range exhausted. | تم استنفاد نطاق ترقيم المستندات، يرجى مراجعة إدارة النظام. |
| `EMPTY_DOCUMENT` | 400 | Document must contain at least one line. | يجب أن يحتوي المستند على صنف واحد على الأقل. |
| `PRIVILEGE_ESCALATION_DENIED` | 403 | This action exceeds your granted authority. | لا يمكنك تنفيذ هذا الإجراء، فهو يتجاوز الصلاحيات الممنوحة لك. |
| `INVALID_PASSWORD` | 400 | Password does not meet the required complexity. | كلمة المرور لا تحقق متطلبات القوة المطلوبة. |

---

## 3. Global Exception Handling Middleware

All unhandled exceptions are caught by ASP.NET Core `ExceptionHandlerMiddleware`:
- Logged with correlation ID, stack trace, and actor context in server logs.
- Sanitized for the client: internal SQL errors and stack traces are **never** exposed to HTTP responses.
- Returned as an RFC 7807 `ProblemDetails` JSON object.

---

## 4. Reconciliation Amendment

Nine error codes were added above to cover behaviour specified in `docs/28`, `docs/30`, `docs/31`, and ADR-012 / ADR-028, none of which previously had an entry in this catalog.

`SERVING_WAREHOUSE_UNAVAILABLE` (CR-092) is the failure mode introduced by ADR-028: a restaurant whose default serving warehouse is missing or inactive. The server returns it and **stops** — it never falls back to another warehouse, because a silently redirected requisition is worse than a refused one.

Two further codes were added during Phase 4 implementation (docs/09 §Phase 4 task 4.8, task 4.14): `PRIVILEGE_ESCALATION_DENIED` for the privilege-escalation guards docs/09-authorization-security.md §2.4 requires (no self role/scope change, only Owner creates Owner, Admin cannot grant beyond its own bounds) — none of the nine codes above fit a permission/role-authority violation distinct from `FORBIDDEN_SCOPE`'s warehouse/restaurant-location meaning — and `INVALID_PASSWORD` for ASP.NET Core Identity's own password-complexity rejection during user creation (task 4.14), which had no prior code either.

**`messageEn` is a developer diagnostic.** It is never rendered to a user and is omitted entirely in the `Production` environment (`docs/31 §4.1`, CR-072). Only `messageAr` reaches the interface.
