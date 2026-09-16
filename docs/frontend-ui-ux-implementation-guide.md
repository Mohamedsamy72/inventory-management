# Frontend UI/UX Implementation Guide

> **Document ID:** SPEC-UIUX-01  
> **Target Audience:** Frontend Implementation Agents, UI Engineers  
> **Status:** Authoritative & Mandatory  
> **Aesthetic Mandate:** Swiss-Inspired Enterprise Minimalism (Arabic-First RTL)

---

## 1. Mandatory UI/UX Pro Max Skill Directive

### 1.1 Mandatory Skill Invocation Rule
The frontend implementation agent (Claude) **MUST** consult and execute the `UI/UX Pro Max` skill (or equivalent design intelligence module) available in the runtime environment prior to authoring any component or page layout.

The agent must **NEVER** rely solely on:
- Generic Tailwind CSS default utility combinations (`bg-blue-500`, standard unstyled HTML inputs).
- Personal subjective styling preferences.
- Pre-made generic AI admin dashboard templates (which tend to be noisy, low-density, and LTR-centric).
- Old legacy components from prior iterations.

### 1.2 Skill Application & Reconciliation Workflow
Before writing frontend code, the implementation agent must execute the following protocol:
1. **Locate & Read:** Access the `UI/UX Pro Max` skill instructions and rules.
2. **Reconcile:** Align the skill's design suggestions with this project's **Arabic RTL enterprise requirements**:
   - High information density suitable for enterprise ERP users.
   - Clean typographic contrast optimized for Arabic script (`Noto Sans Arabic`).
   - Action-oriented, role-specific operational layouts.
3. **Document Decisions:** If a novel UX pattern is introduced, record it in this specification or `docs/decision-log.md`.
4. **Enforce Consistency:** Apply the reconciled design system uniformly across all screens.

### 1.3 Fallback Protocol (If Skill is Unavailable)
If the `UI/UX Pro Max` skill is not present in the runtime environment:
- **DO NOT** invent an ad-hoc aesthetic or use a random template.
- Log the notice in `docs/implementation-status.md`.
- Adhere strictly to the design tokens, layout grids, and component specs defined in this guide and `docs/11-ui-ux-design-system.md`.

---

## 2. Core Aesthetic & Visual Foundation: Swiss Enterprise Minimal

The visual philosophy draws from modern Swiss graphic design: clean grid alignment, generous yet controlled whitespace, restrained color palettes, functional hierarchy, and zero visual gimmicks.

```
┌────────────────────────────────────────────────────────────────────────┐
│ [Header: Company Brand | User Profile | Scopes | Role Badge]           │
├───────────────┬────────────────────────────────────────────────────────┤
│ [Sidebar]     │ [Main Operational Canvas: dir="rtl"]                   │
│ • Role-based  │ ┌────────────────────────────────────────────────────┐ │
│   navigation  │ │ Page Header (Breadcrumbs + Title + Primary Action) │ │
│ • Collapsible │ ├────────────────────────────────────────────────────┤ │
│ • Dense list  │ │ Action Cards / Operational Table Filter Bar        │ │
│               │ ├────────────────────────────────────────────────────┤ │
│               │ │ Enterprise Data Table (Sticky Header, Keyset Nav)  │ │
│               │ └────────────────────────────────────────────────────┘ │
└───────────────┴────────────────────────────────────────────────────────┘
```

---

## 3. Typography & Bidirectional (Bidi) Engine

### 3.1 Font Pairing & Load Strategy
- **Primary Script (Arabic):** `Noto Sans Arabic` (weights: 400, 500, 600, 700).
- **Secondary Script (Latin, Numbers, SKUs):** `Plus Jakarta Sans` (weights: 400, 500, 600, 700).

```css
/* src/styles/globals.css */
@import url('https://fonts.googleapis.com/css2?family=Noto+Sans+Arabic:wght@400;500;600;700&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap');

:root {
  --font-arabic: 'Noto Sans Arabic', -apple-system, BlinkMacSystemFont, sans-serif;
  --font-latin: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, sans-serif;
}

body {
  font-family: var(--font-arabic);
  direction: rtl;
}

.font-mono-code {
  font-family: var(--font-latin);
  direction: ltr;
  unicode-bidi: isolate;
}
```

### 3.2 Bidi Text Formatting Rules
1. **Item Codes & Document Numbers:** Always wrap in `<span dir="ltr" className="font-mono-code">ITM-000123</span>` to prevent reverse hyphenation in Arabic text.
2. **Mobile Numbers:** Format with `<span dir="ltr">010 1234 5678</span>`.
3. **Quantities with Units:** Format numbers alongside Arabic units cleanly: `١٥ كجم` or `15 كجم`.

---

## 4. Enterprise Color Palette & Semantic Tokens

```javascript
// tailwind.config.js snippet
module.exports = {
  theme: {
    extend: {
      colors: {
        surface: {
          canvas: '#F8FAFC',  // Slate-50
          card: '#FFFFFF',    // White
          border: '#E2E8F0',  // Slate-200
          hover: '#F1F5F9',   // Slate-100
        },
        brand: {
          primary: '#0F172A', // Slate-900 (High contrast header & primary CTA)
          secondary: '#334155', // Slate-700
          accent: '#2563EB',  // Blue-600 (Interactive links, active tabs)
        },
        status: {
          successBg: '#DCFCE7', // Emerald-100
          successText: '#15803D', // Emerald-700
          warningBg: '#FEF3C7', // Amber-100
          warningText: '#B45309', // Amber-700
          dangerBg: '#FEE2E2',  // Red-100
          dangerText: '#B91C1C',  // Red-700
          neutralBg: '#F1F5F9', // Slate-100
          neutralText: '#475569', // Slate-600
        }
      }
    }
  }
}
```

---

## 5. Core UI Component Specifications

### 5.1 Interactive Buttons (`src/components/ui/Button.tsx`)
- **Primary:** Dark Slate (`bg-brand-primary hover:bg-slate-800 text-white shadow-sm transition-all duration-150 active:scale-[0.98]`).
- **Secondary / Outline:** Bordered Slate (`border border-surface-border bg-white text-slate-700 hover:bg-surface-hover`).
- **Destructive:** Red accent (`bg-red-600 hover:bg-red-700 text-white`).
- **Loading State:** Displays an integrated SVG spinner with disabled pointer events and preserved button width to prevent layout shift.

### 5.2 Form Inputs & Validation Ergonomics (`src/components/ui/Input.tsx`)
- **Labels:** Crisp 13px medium typography (`text-slate-700 font-medium mb-1.5 block`).
- **Input Field:** `h-10 px-3.5 bg-white border border-surface-border rounded-md text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand-accent/20 focus:border-brand-accent transition-all`.
- **Inline Error Feedback:** Renders immediately beneath input in Arabic (`text-xs text-red-600 font-medium mt-1`).
- **Required Markers:** Red asterisk with accessible `aria-hidden="true"`.

### 5.3 Quick-Add Master Data Modal Pattern (`src/components/features/QuickAddModal.tsx`)
Used inside Item creation for Categories and Units:
1. User clicks `[ + إضافة قسم جديد ]` next to the Category dropdown.
2. Accessible dialog opens with backdrop blur (`backdrop-blur-sm bg-slate-900/40`).
3. Focus traps automatically inside the modal input.
4. On submit, executes `POST /api/v1/categories`.
5. On success:
   - Closes modal.
   - Shows brief success feedback toast.
   - Adds new category to local select options and **automatically selects it**.
   - User continues filling the Item form with zero page refreshes or state loss.

### 5.4 Enterprise Data Tables & Mobile Card Collapse (`src/components/ui/DataTable.tsx`)
- **Desktop (>= 768px):** Clean table layout with subtle horizontal borders (`border-b border-surface-border`), sticky header, hover highlight (`hover:bg-slate-50/80`), and aligned numerical columns.
- **Mobile (< 768px):** The table automatically collapses into stacked card items displaying key metadata, status badges, and action buttons without horizontal scrollbar overflow.

---

## 6. Complete State Handling (The 5 Mandatory API States)

Every screen fetching or mutating API data must explicitly implement all 5 states:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Component State Machine                         │
├───────────────────┬────────────────────────────────────────────────────┤
│ 1. Loading        │ Renders pulsing skeleton loaders matching table    │
│                   │ geometry (Never a blank white box).                │
├───────────────────┼────────────────────────────────────────────────────┤
│ 2. Success (Data) │ Renders populated data table or form cards.        │
├───────────────────┼────────────────────────────────────────────────────┤
│ 3. Empty          │ Clear Arabic message: "لا توجد سجلات مطابقة"      │
│                   │ with icon and clear action button.                 │
├───────────────────┼────────────────────────────────────────────────────┤
│ 4. Error          │ High-visibility red/amber error banner with parsed │
│                   │ Arabic message and `[ إعادة المحاولة ]` CTA button. │
├───────────────────┼────────────────────────────────────────────────────┤
│ 5. Mutation Lock  │ Form buttons disabled with inline spinner during   │
│                   │ POST/PUT requests to prevent duplicate submission.  │
└───────────────────┴────────────────────────────────────────────────────┘
```

---

## 7. Accessibility (WCAG 2.1 AA Checklist)

1. **Keyboard Navigability:** All buttons, dropdowns, pagination links, and modals must be operable via `Tab`, `Shift+Tab`, `Enter`, `Space`, and `Escape`.
2. **Focus Rings:** Distinct, accessible focus rings on all interactive elements (`focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2`).
3. **Contrast Ratios:** Text-to-background contrast ratio $\ge 4.5:1$ for standard body text and $\ge 3:1$ for large headings.
4. **Screen Reader Attributes:** Modals must have `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, and proper Arabic labels on icon-only buttons.

---

## 8. Reconciliation Amendment — Workflow Coverage & Localization

> Added by the Clean-Restart reconciliation pass. Resolves CR-082. Arabic content, numerals, dates, sorting, filtering, pagination, iconography and print/export are specified in full in **`docs/31-arabic-rtl-localization-spec.md`**, which this guide defers to.

### 8.1 UI/UX Pro Max Skill — Location

The skill is present in this repository at `ui-ux-pro-max/` (`SKILL.md`, `data/`, `scripts/`). §1.1 of this guide is therefore **binding**: read `ui-ux-pro-max/SKILL.md` before authoring any component, and prefer its `data/stacks/nextjs.csv`, `shadcn.csv`, `colors.csv`, `typography.csv`, and `ux-guidelines.csv` guidance over ad-hoc styling. The §1.3 fallback protocol does not apply.

### 8.2 Every Backend Workflow Has a Frontend Workflow

No backend capability may ship without its screen, and no screen may exist without its backend endpoint.

| Workflow | Screen | Primary Actor | Key States |
| :--- | :--- | :--- | :--- |
| Login / OTP reset | `(auth)/login`, `(auth)/forgot-password` | All | Loading, error, rate-limited |
| Item catalogue + quick-add | `/items` | Owner, Admin | 5 states; code field read-only |
| Categories / units / conversions | `/master-data` | Owner, Admin | Quick-add modal |
| Suppliers | `/suppliers` | Owner, Admin | — |
| Warehouses / restaurants | `/locations` | Owner, Admin | — |
| Users, roles, scopes, permissions | `/users` | Owner, Admin | Non-grantable codes never listed |
| Receiving: draft → post → verify → reverse | `/receiving` | Warehouse Staff, Admin, Owner | Variance entry; costs Owner-only |
| Warehouse stock view | `/locations/{id}/stock` | Owner, Admin, WH Staff (scoped) | Balance / in-transit / available |
| Supply request: draft, lines, submit, cancel | `/supply-requests` | Restaurant Supervisor (scoped) | Multi-line editor; **no warehouse selector** (ADR-028) |
| Request queue and fulfilment | `/supply-requests` | Warehouse Staff (scoped) | Per-line fulfilled quantity |
| Dispatch | `/supplies` | Warehouse Staff (scoped) | Sufficiency advisory banner |
| Receipt confirmation | `/supplies/{id}/confirm` | Restaurant Supervisor (scoped) | Per-line received quantity |
| Discrepancies | `/discrepancies` | Per scope | Resolve flow |
| Consumption log | `/consumption` | Restaurant Supervisor (scoped) | — |
| Stock counts | `/stock-counts` | WH Staff counts; Owner/Admin approve | Blind-count mode |
| ~~Reports~~ | 🚫 **DEFERRED (ADR-029)** — route **not registered**, nothing rendered | — | — |
| Audit log & activity monitor | `/audit` | **Owner only** | Route absent for all others |

### 8.3 Multi-Item Supply Request Editor

The single most important screen in the product.

0. **There is no warehouse selector on this screen (ADR-028).** The serving warehouse is derived by the server from the restaurant's configuration. After creation, the resolved warehouse is displayed **read-only** on the request header — so the supervisor can see where their request went without being able to change it. The form never sends `warehouseId`, and the client never constructs one.
1. A line row is: item selector (searchable, Arabic-normalized) · quantity · unit selector · notes · remove.
2. `[ + إضافة صنف ]` appends an empty row and focuses its item selector.
3. Selecting an item **already on the request** focuses the existing row and highlights it rather than creating a duplicate — the UI expression of `UNIQUE (supply_request_id, item_id)` (CR-023).
4. The unit selector lists only units with an active conversion for that item, plus its base unit.
5. **The client never computes a base quantity.** It may show the server-resolved conversion as read-only helper text (`1 كرتونة = 12 كجم`), and must never send a factor.
6. `[ إرسال الطلب الى المخزن ]` is disabled while zero lines exist or any quantity is `≤ 0`, with an Arabic reason shown.
7. Lines are editable **only** in `Draft`. In `Submitted` the editor is read-only with an Arabic explanation and a `[ إلغاء الطلب ]` action.
8. Unsaved changes prompt on navigate-away.

### 8.4 Fulfilment and Confirmation Quantity Entry

- **Fulfilment** — requested quantity is shown read-only beside a fulfilled-quantity input, defaulted to the requested value, capped at it, and allowed to be `0` (out of stock). Each row shows the warehouse's available quantity. A row exceeding available shows an **amber advisory**, never a block (ADR-019).
- **Confirmation** — dispatched quantity read-only beside a received-quantity input, defaulted to dispatched, clamped to `0 … dispatched`. Any row where received `<` dispatched reveals a required Arabic reason field. A confirmation summary dialog lists every variance before submit.
- Confirmation submits with an `X-Idempotency-Key` generated **once per form instance** — never regenerated on retry, which is the entire point.

### 8.5 Generated Codes in the UI

Create forms show a disabled field reading `"توليد تلقائي بواسطة النظام"`. No form ever submits `generatedCode` or `documentNumber`. Displayed codes use `<span dir="ltr" class="font-mono-code">` (`docs/31 §4.4`).

### 8.6 Authorization Is UX Only

Navigation and controls are hidden by permission for clarity, never for security. The server is authoritative (`docs/09`). Three distinct states must be distinguishable:

| State | HTTP | Screen |
| :--- | :---: | :--- |
| Not authenticated | 401 | Redirect to `/login`. |
| Authenticated, forbidden | 403 | `"غير مصرح لك بالوصول إلى هذه الصفحة"` + back action. **Never** a blank screen or a silent redirect. |
| Out of scope | 403/404 | `"هذا السجل خارج نطاق صلاحياتك"`. |

The Owner-only `/audit` route is **not registered** in the router for other roles, so a direct URL yields the standard not-found page rather than a hint that the feature exists.

### 8.7 Zero Fake Data — Enforced

Never ship: hardcoded `0` counts, mock arrays, placeholder charts, lorem text, sample rows, or an optimistic figure standing in for a failed request. A failed widget shows an Arabic error banner with `[ إعادة المحاولة ]`. An empty widget shows an Arabic empty state. **A number on screen is always a number the server returned.**

### 8.8 Frontend Testing (CR-080)

`frontend/__tests__` using Vitest + React Testing Library, covering: the formatter module (`docs/31 §4.5`), status-badge mapping, permission-driven navigation, the five API states per screen family, the multi-item editor's duplicate-merge behaviour, and idempotency-key stability across retries. Playwright covers full-flow browser E2E (`docs/18`).

### 8.9 Related Documents

`docs/31-arabic-rtl-localization-spec.md`, `docs/10`, `docs/11`, `docs/12`, `docs/13`, `docs/30`, `docs/32`, `ui-ux-pro-max/SKILL.md`.

---

## 9. Decision-Closure Amendment (ADR-028, ADR-029)

### 9.1 No Warehouse Selector (ADR-028)
The Restaurant Supervisor's request screen has **no** warehouse field of any kind — not a dropdown, not a hidden input, not a default the form posts back. The serving warehouse is derived server-side and returned on the created request for display only. A hidden field would be worse than a visible one: it would look harmless in review while still shipping a client-supplied value the server must then be trusted to ignore.

### 9.2 Deferred Features Render Nothing (ADR-029)
| Feature | Frontend treatment |
| :--- | :--- |
| Reports | `/reports` route **not registered**. No nav entry rendered, no page, no chart, no KPI card, no "coming soon" screen. A direct URL yields the standard not-found page. |
| File attachments | No upload control, attachment panel, or evidence thumbnail on any screen. |

§8.7's zero-fake-data rule covers deferred features explicitly: a placeholder chart is fake data with better styling. If a surface has nothing real to show, it does not exist yet.

### 9.3 New Error States
`SERVING_WAREHOUSE_UNAVAILABLE` (409) renders as a standard Arabic error banner — `"لا يوجد مستودع خدمة مفعّل لهذا الفرع، يرجى مراجعة إدارة النظام"` — with no retry action, because retrying cannot succeed. The action is to contact an administrator, and the banner says so.
