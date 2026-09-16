# 31 — Arabic-Only Content & RTL Localization Specification

> **Document ID:** SPEC-31
> **Topic:** Arabic-only user-facing content, native RTL layout, bidirectional text, numerals, dates, phone numbers, sorting, filtering, pagination, and print/export
> **Status:** Authoritative
> **Authority:** Governs all user-facing text and layout direction. Subordinate only to `docs/decision-log.md`. Extends `docs/10`, `docs/11`, and `docs/frontend-ui-ux-implementation-guide.md`.

---

## 1. Purpose

The product is Arabic-only for user-facing business content and natively right-to-left. `docs/10 §3` and the UI/UX guide §3 cover fonts and basic bidi isolation, but the brief mandates coverage of validation, errors, statuses, dashboards, tables, print/export, numerals, phone numbers, dates, quantities, directional icons, pagination, breadcrumbs, sorting, and filtering. This document is the single authoritative home for all of it (CR-082).

## 2. Scope

Everything a user reads or a printer emits. **Out of scope:** source code identifiers, database column names, log messages, HTTP error `code` values, and the developer-only `messageEn` field — these remain English (`docs/04 §2` rule 13).

## 3. Terminology

| Term | Meaning |
| :--- | :--- |
| **Business content** | Anything a warehouse or restaurant user reads to do their job. |
| **Technical identifier** | A code, column name, or enum used only by engineers. |
| **Bidi isolation** | Wrapping LTR content so surrounding Arabic does not reorder its punctuation. |
| **Logical property** | A CSS property expressed as start/end rather than left/right. |

---

## 4. Requirements

### 4.1 Language Policy

| Surface | Language | Rule |
| :--- | :--- | :--- |
| Labels, headings, buttons, menus | **Arabic only** | No English fallback string may ship. |
| Validation messages | **Arabic only** | Server returns `messageAr`; the client renders only that. |
| Error messages | **Arabic only** | `docs/13` catalog. `messageEn` is a developer diagnostic and is **omitted entirely in the Production environment** (CR-072). |
| Status badges | **Arabic only** | Enum → Arabic mapping in §4.6; never render the raw enum. |
| Empty and error states | **Arabic only** | `"لا توجد سجلات مطابقة"`, `"تعذر تحميل البيانات"`. |
| Emails / SMS (OTP) | **Arabic only** | The OTP digits themselves stay Western numerals for keypad entry. |
| Print / PDF / CSV exports | **Arabic only** | Headers and labels Arabic; see §4.9. |
| Master-data content | **Arabic only** | `name_arabic` columns; validated per §4.2. |
| Business identifiers | **Latin** | `ITM-000142` — an identifier, not prose. Bidi-isolated per §4.4. |
| Source code, DB, logs | **English** | Unchanged. |

### 4.2 Arabic Content Validation
1. `name_arabic` fields must contain at least one character in the Arabic Unicode block (`U+0600–U+06FF`).
2. Latin characters and digits are **permitted** within them — real products carry names like `"جبنة موزاريلا 2 كجم"`. The rule requires Arabic presence, not Latin absence.
3. **Normalization before storage and before search** (this is what makes search usable and uniqueness real):
   - strip Arabic diacritics (`U+064B–U+0652`) and tatweel (`U+0640`);
   - unify alef forms `أ إ آ` → `ا`;
   - unify `ة` → `ه` and `ى` → `ي` **for the search key only**, never for the stored display value.
4. Normalization feeds a `name_normalized` search column and the `pg_trgm` index (`docs/29 §5.4`); the display value is stored exactly as typed.
5. Uniqueness (`uq_items_company_name`) is evaluated on the **normalized** value, so `"جبنه"` and `"جبنة"` cannot both be created as separate items.

### 4.3 Layout Direction
1. `<html dir="rtl" lang="ar">`.
2. **Only** logical CSS properties: `ps-`, `pe-`, `ms-`, `me-`, `text-start`, `text-end`, `border-s`, `border-e`, `start-`, `end-`. Physical `left`/`right` utilities are forbidden outside genuinely direction-neutral cases (a centred spinner).
3. The sidebar sits on the **right**; the main canvas on the left. Breadcrumbs read right-to-left with a start-pointing separator.
4. Tables: first logical column at the right edge. Numeric columns are `text-end` in the logical sense and align on the decimal point.
5. Modals, drawers, toasts, and tooltips originate from the logical start (right).

### 4.4 Bidirectional Text Rules
Any LTR run inside Arabic prose is wrapped in `<span dir="ltr" class="font-mono-code">` — CSS `unicode-bidi: isolate`, never `bidi-override`.

Applies to: item codes (`ITM-000142`), document numbers (`REC-202609-0001`), mobile numbers, email addresses, URLs, and timestamps.

Without isolation, `ITM-000142` renders as `142-000-ITM` inside an Arabic sentence — the single most common Arabic-UI defect.

### 4.5 Numerals, Quantities, Currency & Dates

| Element | Rule | Example |
| :--- | :--- | :--- |
| **Digits** | **Western Arabic numerals (0–9)** throughout. Warehouse and kitchen staff read scale displays and supplier invoices in Western digits; switching the UI to Eastern Arabic-Indic (٠–٩) would force constant mental transliteration. | `1,250.50` |
| **Grouping / decimal** | `,` thousands, `.` decimal — matching Egyptian commercial convention. | `12,500.75` |
| **Quantity + unit** | Number then Arabic unit, single space. | `15 كجم` |
| **Currency** | Number then `ج.م`; two decimals; **Owner only**. | `1,250.50 ج.م` |
| **Dates** | Gregorian, `DD/MM/YYYY`. Hijri is not displayed in v1.0. | `10/09/2026` |
| **Date-times** | `DD/MM/YYYY - HH:mm` (24-hour), rendered in the **tenant timezone**; stored and transmitted UTC. | `10/09/2026 - 14:30` |
| **Relative time** | Arabic relative phrasing on dashboards only. | `منذ 5 دقائق` |
| **Mobile numbers** | Grouped `010 1234 5678`, `dir="ltr"`. | |
| **Percentages** | Number then `%`, `dir="ltr"` on the pair. | `12.5%` |

A single `src/lib/formatters.ts` module owns every one of these. Ad-hoc `toLocaleString` calls in components are forbidden — they are how a codebase ends up with three date formats.

### 4.6 Canonical Status Vocabulary

Server enums are English; the UI renders only the Arabic label. This mapping is authoritative and must not be duplicated per screen.

| Entity | Enum | Arabic | Badge Tone |
| :--- | :--- | :--- | :--- |
| ReceivingOrder | `Draft` | `مسودة` | Neutral |
| ReceivingOrder | `Submitted` | `مرحل للمخزن` | Blue |
| ReceivingOrder | `Verified` | `تمت المطابقة` | Green |
| ReceivingOrder | `Reversed` | `معكوس` | Red |
| SupplyRequest | `Draft` | `مسودة` | Neutral |
| SupplyRequest | `Submitted` | `مقدم للمستودع` | Blue |
| SupplyRequest | `PartiallyFulfilled` | `تم تجهيز جزء من الطلب` | Amber |
| SupplyRequest | `Fulfilled` | `تم تجهيز الطلب بالكامل` | Green |
| SupplyRequest | `Cancelled` | `ملغي` | Red |
| Supply | `Prepared` | `قيد التجهيز` | Neutral |
| Supply | `Dispatched` | `تم الشحن / في الطريق` | Amber |
| Supply | `Confirmed` | `تم تأكيد الاستلام` | Green |
| Supply | `ConfirmedWithDiscrepancy` | `تم الاستلام مع وجود فروقات` | Orange |
| Supply | `RejectedAtDelivery` | `مرفوض عند التسليم` | Red |
| Supply | `Cancelled` | `ملغي` | Red |
| StockCount | `Draft` | `مسودة` | Neutral |
| StockCount | `InProgress` | `جاري العد` | Blue |
| StockCount | `PendingApproval` | `بانتظار الاعتماد` | Amber |
| StockCount | `Approved` | `معتمد وتمت التسوية` | Green |
| StockCount | `Rejected` | `مرفوض — إعادة الجرد` | Red |
| Discrepancy | `Open` | `مفتوح` | Amber |
| Discrepancy | `Investigating` | `قيد المراجعة` | Blue |
| Discrepancy | `Resolved` | `تمت المعالجة` | Green |

### 4.7 Directional Iconography

| Meaning | RTL Behaviour |
| :--- | :--- |
| Back / previous | Arrow points **right** (mirrored from the LTR default). |
| Forward / next | Arrow points **left**. |
| Breadcrumb separator | Points **left** (`‹`), reading right to left. |
| Pagination "next page" | Points **left**. |
| Expand / collapse chevron | Down when open; points **left** when closed. |
| Non-directional (search, trash, print, plus, warning) | **Never** mirrored. |
| Trend arrows (up/down) | **Never** mirrored — vertical semantics are direction-independent. |
| Progress and loading bars | Fill from the **right**. |

### 4.8 Sorting, Filtering, Pagination, Breadcrumbs
1. **Sorting** — Arabic text sorts using the `ar` collator (`Intl.Collator('ar')` client-side; PostgreSQL `COLLATE "ar-x-icu"` server-side). Byte-order sorting produces visibly wrong Arabic ordering.
2. Sort indicators appear at the logical end of the header cell.
3. **Filtering** — text filters normalize the query with the §4.2 rules before sending, so a user typing `"جبنه"` finds `"جبنة"`.
4. Active filters render as removable Arabic chips with an `×` at the logical end.
5. **Pagination** — keyset only (`docs/21`). Controls read `[ التالي ]` / `[ السابق ]` with left/right-pointing arrows per §4.7. Never invent a total page count; keyset pagination has no total, and fabricating one would violate the zero-fake-data rule.
6. **Breadcrumbs** — right to left, e.g. `الرئيسية ‹ الأصناف ‹ تفاصيل الصنف`. The final crumb is plain text, never a link.

### 4.9 Print & Export
1. **PDF** — server-side rendering with an engine performing Arabic glyph shaping and the Unicode bidi algorithm (QuestPDF or a headless-Chromium pipeline). `Noto Sans Arabic` embedded. **Naive PDF libraries render Arabic as disconnected, reversed letterforms** — the output must be visually verified, not merely generated without error.
2. Page direction RTL; headers and footers Arabic; page numbers `صفحة 1 من 4`.
3. **CSV** — UTF-8 **with BOM**. Without the BOM, Microsoft Excel on Windows renders Arabic as mojibake, and the target users are on Windows.
4. Exports respect role projection: an Admin's export contains no cost column at all — not a blank one (`docs/15`).
5. Every export writes an `audit_logs` entry (`docs/17 §3`).

### 4.10 Accessibility in Arabic
1. `lang="ar"` on `<html>`; any embedded LTR run carries its own `lang`/`dir`.
2. Icon-only buttons carry Arabic `aria-label`.
3. Screen-reader announcements (`aria-live`) are Arabic.
4. Contrast ratios per the UI/UX guide §7 — Arabic script at small sizes needs the full 4.5:1; do not thin it below weight 400.
5. Minimum Arabic body size **14px**; Arabic diacritic-free text below 13px loses legibility faster than Latin at the same size.

---

## 5. Business Rules

| ID | Rule |
| :--- | :--- |
| **BR-31-1** | No English string is ever rendered to a business user. |
| **BR-31-2** | Every LTR run inside Arabic prose is bidi-isolated. |
| **BR-31-3** | All formatting flows through `src/lib/formatters.ts`. |
| **BR-31-4** | Status enums are rendered only via the §4.6 mapping. |
| **BR-31-5** | Arabic search and uniqueness operate on normalized text; display uses the original. |
| **BR-31-6** | Arabic sorting uses the `ar` collator, never byte order. |
| **BR-31-7** | CSV exports carry a UTF-8 BOM; PDFs embed a shaping-capable Arabic font. |
| **BR-31-8** | Physical CSS direction utilities are forbidden. |

## 6. Edge Cases

1. **Mixed content** — `"استلام REC-202609-0001 من المورد"`: only the code is isolated.
2. **Arabic name with Latin digits** — `"زيت ذرة 5 لتر"` is valid (§4.2 rule 2).
3. **Number at the start of an Arabic sentence** — bidi isolation prevents it jumping to the wrong end.
4. **Zero quantity** — render `0` explicitly with an Arabic unit; never an em dash, which reads as "unknown".
5. **Long Arabic item names in a table** — truncate with an ellipsis at the logical end plus a full-text tooltip; never mid-word.
6. **Negative variance** — `2- كجم` renders confusingly; use `نقص 2 كجم` / `زيادة 2 كجم` instead of a bare sign.
7. **Text expansion** — Arabic labels typically run shorter than English but taller in line-height; layouts must not assume fixed label widths.

## 7. Security Implications

- `messageEn` suppression in Production prevents leaking internal English diagnostics to end users (CR-072).
- Arabic normalization must not be used as a security boundary: two visually distinct names normalizing to one value is a **data-quality** guard, not an authorization guard.
- Right-to-left override characters (`U+202E`) must be **stripped** from all user input. Left in place, they enable spoofed filenames and display-order attacks in tables and file lists.

## 8. Dependencies

`docs/10-frontend-architecture.md`, `docs/11-ui-ux-design-system.md`, `docs/frontend-ui-ux-implementation-guide.md`, `docs/13-validation-error-handling.md`, `docs/17-reporting.md`, `docs/29 §5.4`, `docs/15-costing-financial-data.md`.

## 9. Acceptance Criteria

- `AC-31-1` A repository scan finds no user-facing English string literal in `frontend/src`.
- `AC-31-2` Every rendered item code and document number carries `dir="ltr"`.
- `AC-31-3` No `pl-`, `pr-`, `ml-`, `mr-`, `text-left`, `text-right` utility appears outside the documented direction-neutral exceptions.
- `AC-31-4` Searching `"جبنه"` returns an item stored as `"جبنة"`.
- `AC-31-5` A CSV export opens with correct Arabic in Excel on Windows.
- `AC-31-6` A generated PDF shows connected, correctly ordered Arabic glyphs (verified visually, not by absence of error).
- `AC-31-7` Arabic sorting matches `Intl.Collator('ar')` ordering.
- `AC-31-8` Every status badge in a Playwright run renders Arabic, never a raw enum.
- `AC-31-9` `U+202E` submitted in any text field is stripped before storage.

## 10. Related Documents

`docs/04-end-to-end-business-flow.md` §2 rule 13, `docs/10`, `docs/11`, `docs/12`, `docs/13`, `docs/17`, `docs/29`, `docs/frontend-ui-ux-implementation-guide.md`, `docs/32`.
