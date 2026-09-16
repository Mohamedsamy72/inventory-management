# 10 — Frontend Architecture (Next.js 16 / TypeScript / Tailwind)

> **Document ID:** SPEC-10  
> **Framework:** Next.js 16 (App Router), React 19, TypeScript, Tailwind CSS  
> **Direction:** Native Right-to-Left (RTL)

---

## 1. Directory & Layer Organization

The frontend codebase is organized under `frontend/` with strict layer separation:

```
frontend/
├── public/
│   └── fonts/                     # Noto Sans Arabic & Plus Jakarta Sans
├── src/
│   ├── app/                       # Next.js App Router Pages & Layouts
│   │   ├── (auth)/                # Login, Forgot Password, Reset OTP
│   │   ├── (dashboard)/           # Protected Application Layouts & Sub-routes
│   │   │   ├── items/             # Items Catalog, Quick-Add Dialogs
│   │   │   ├── receiving/         # Receiving Orders, Verification & Variance
│   │   │   ├── supply-requests/   # Branch Requisitions
│   │   │   ├── supplies/          # Dispatch & Receipt Confirmation
│   │   │   ├── stock-counts/      # Warehouse Physical Count Workflows
│   │   │   ├── users/             # User Management & Scopes
│   │   │   └── audit/             # Owner-only Audit Log Viewer
│   │   │   # NOTE: no reports/ directory — reporting is deferred (ADR-029);
│   │   │   # a deferred route is never registered, not even as a stub.
│   ├── components/                # Reusable UI Design System Components
│   │   ├── ui/                    # Button, Dialog, Input, Table, Badge, Card
│   │   ├── layout/                # Sidebar, Header, Breadcrumbs, RTL Shell
│   │   └── feedback/              # EmptyState, ErrorBanner, LoadingSkeleton
│   ├── features/                  # Domain-Specific Complex Components & Hooks
│   ├── lib/                       # Utilities & Centralized API Client
│   │   ├── api-client.ts          # Centralized Fetch Wrapper with CSRF & Error Parsing
│   │   └── formatters.ts          # Arabic Dates, Numbers, and Currency Formatters
│   ├── hooks/                     # Custom React Hooks (useAuth, usePermissions)
│   └── types/                     # Shared TypeScript API Request/Response Interfaces
```

---

## 2. Centralized API Client (`src/lib/api-client.ts`)

The application interacts with the backend strictly through a strongly typed centralized client:

### Requirements:
1. **Credentials & Cookies:** Configured with `credentials: 'include'` for HttpOnly session cookie transmission.
2. **CSRF Header Attachment:** Automatically injects `X-CSRF-TOKEN` on mutating HTTP methods.
3. **ProblemDetails Parsing:** Intercepts HTTP 4xx/5xx responses and parses RFC 7807 error objects, surfacing the Arabic error message (`messageAr`) to the UI.
4. **Session Expiry Handling:** Automatically redirects to `/login` when receiving an unhandled HTTP 401.

---

## 3. Native RTL & Bidirectional Text Handling

1. **Document Configuration:** `html` tag is configured with `dir="rtl"` and `lang="ar"`.
2. **Tailwind Logical Properties:** Uses standard logical utilities (`ps-`, `pe-`, `ms-`, `me-`, `text-start`, `text-end`) rather than hardcoded left/right alignments.
3. **Mixed Text Handling:** SKU identifiers, telephone numbers, and timestamps use directional isolation (`dir="ltr"` inline wrapper) to prevent punctuation inversion in Arabic sentences.

---

## 4. Reconciliation Amendment

**No `reports/` route (ADR-029).** Reporting is deferred. The route is **not registered**, so a direct URL yields the standard not-found page. No stub page, no "coming soon" screen, no placeholder chart — a placeholder is fake data with better styling (`docs/17 §4.6`).

**No file-upload components (ADR-029).** No approved workflow requires an attachment, so no upload control, attachment panel, or evidence thumbnail exists on any screen.

**No warehouse selector on the supply-request form (ADR-028).** The serving warehouse is derived server-side. The form never sends `warehouseId` and the client never constructs one — not even as a hidden field, which would look harmless in review while still shipping a client-supplied value.

**Arabic and RTL.** `docs/31-arabic-rtl-localization-spec.md` is authoritative for content language, direction, bidi isolation, numerals, dates, sorting, filtering, pagination, iconography, and export. §3 above is its summary.
