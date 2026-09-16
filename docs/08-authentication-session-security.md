# 08 — Authentication & Session Security

> **Document ID:** SPEC-08  
> **Topic:** Mobile Authentication, OTP Lifecycle, Cookie Sessions, CSRF, and Rate Limiting

---

## 1. Authentication Credentials & Lifecycle

Authentication is based strictly on **Normalized Mobile Number + Password**.

```
[ User Enters Mobile & Password ] ──► [ ASP.NET Core Identity Validation ]
                                                │
                                 ┌──────────────┴──────────────┐
                                 │ Valid Credentials           │ Invalid
                                 ▼                             ▼
                    ┌─────────────────────────┐   ┌─────────────────────────┐
                    │ Issue HttpOnly Cookie   │   │ Return 401 Unauthorized │
                    │ Issue CSRF Token Header │   │ Log Failed Login Audit  │
                    │ Reset Failed Counter    │   │ Increment Rate Limiter  │
                    └─────────────────────────┘   └─────────────────────────┘
```

### Key Rules:
- **No Self-Service Registration:** Normal users are provisioned exclusively by the `Owner` or authorized `Admin`.
- **Mobile Normalization:** Mobile numbers are stripped of non-numeric characters and stored in standard format (e.g., `01012345678`).
- **Inactive Accounts:** If `User.IsActive == false`, authentication is immediately rejected with `"الحساب معطل، يرجى التواصل مع الإدارة"`.

---

## 2. Password Reset & OTP Lifecycle

1. **OTP Request:** User submits registered mobile number via `/api/v1/auth/forgot-password/otp`.
2. **OTP Generation:**
   - Cryptographically secure 6-digit numeric token.
   - Expiration: strictly 5 minutes.
   - Stored in hashed format in cache/database (never stored in plaintext).
   - Single-use only: invalidates immediately upon verification.
3. **SMS Dispatch:** Dispatched via the SMS gateway abstraction (`ISmsSender`). In development mode, OTP is written to local console logs; it is **never returned in HTTP API responses**.
4. **Rate Limiting (corrected — ADR-026, CR-060):** **Both** windows apply simultaneously and independently: **3 requests per mobile per 15 minutes** and **5 requests per mobile per hour**, plus the per-IP limit in §5. Whichever triggers first returns `429 RATE_LIMIT_EXCEEDED`. The earlier single-window wording was contradicted by the §5 table and is superseded.

---

## 3. Session Management & Cookie Architecture

To eliminate XSS-based token theft vulnerabilities, the system forbids storing access tokens in browser `localStorage` or `sessionStorage`.

### Cookie Security Headers:
```http
Set-Cookie: __Host-InventorySession=<EncryptedSessionToken>; Path=/; Secure; HttpOnly; SameSite=Strict; Max-Age=28800
```

- **HttpOnly:** Prevents JavaScript document.cookie access.
- **SameSite=Strict:** Blocks cross-site request forgery in modern browsers.
- **Secure:** Enforced on HTTPS in production environments.
- **Security Stamp Rotation:** Changing passwords or revoking user access immediately invalidates all existing active cookies via ASP.NET Core Security Stamp validation.

---

## 4. Cross-Site Request Forgery (CSRF) Protection

Because session authentication relies on automatic cookie transmission, all state-mutating requests (`POST`, `PUT`, `DELETE`, `PATCH`) must supply a valid anti-forgery token in the HTTP header:

```http
X-CSRF-TOKEN: <Anti-Forgery-Token-Value>
```

The frontend retrieves this token via the `GET /api/v1/auth/csrf-token` endpoint (now catalogued in `docs/07 §2.1` — CR-063) upon initial application boot and attaches it to the centralized API client (`src/lib/api-client.ts`).

---

## 5. Rate Limiting & Anti-Brute-Force Policies

Configured using ASP.NET Core Rate Limiting Middleware:

| Endpoint Area | Rate Limit Policy | Window |
| :--- | :--- | :--- |
| `/api/v1/auth/login` | 5 requests per IP | 1 Minute |
| `/api/v1/auth/forgot-password/*` | 3 per IP / Mobile | 15 Minutes |
| `/api/v1/auth/forgot-password/*` | 5 per Mobile | 1 Hour |
| Sensitive Mutations (`/confirm`, `/submit`) | 30 requests per User | 1 Minute |
| General API Endpoints | 300 requests per User / IP | 1 Minute |

---

## 6. Reconciliation Amendment

| Change | Source |
| :--- | :--- |
| OTP rate limiting applies both windows. | ADR-026 / CR-060 |
| OTPs are stored hashed in the `password_reset_otps` table, which is specified in `docs/29 §4.2`. It did not previously exist. | CR-061 |
| `users.access_failed_count` and `users.lockout_end_at` added to back the "Reset Failed Counter" step in §1. | CR-062 |
| `GET /auth/csrf-token` catalogued in `docs/07`. | CR-063 |
