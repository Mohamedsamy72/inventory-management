# 11 — UI/UX Design System & Ergonomics

> **Document ID:** SPEC-11  
> **Aesthetic Foundation:** Enterprise Swiss Minimalism (Clean, high data density, restrained palette, zero visual bloat)  
> **Language & Direction:** Arabic-First, RTL Native

---

## 1. Typography & Hierarchy

- **Primary Arabic Font:** `Noto Sans Arabic` (Google Fonts / Self-hosted woff2)
- **Secondary Latin / Numerical Font:** `Plus Jakarta Sans`
- **Scale:**
  - `Display / Page Title`: 24px (Bold / 700)
  - `Section Header`: 18px (Semi-Bold / 600)
  - `Table & Form Label`: 14px (Medium / 500)
  - `Body & Data Cells`: 14px (Regular / 400)
  - `Metadata & Timestamps`: 12px (Regular / 400)

---

## 2. Color System & Semantic Tokens

Designed for high readability in harsh warehouse and kitchen lighting conditions:

| Token | Hex / HSL | Usage |
| :--- | :--- | :--- |
| `surface-bg` | `#F8FAFC` (Slate-50) | Main application canvas background. |
| `surface-card` | `#FFFFFF` (White) | Table and form container cards with subtle 1px border. |
| `border-subtle` | `#E2E8F0` (Slate-200) | Data table grid lines and card borders. |
| `brand-primary` | `#0F172A` (Slate-900) | Primary CTA buttons, active sidebar items, header branding. |
| `accent-action` | `#2563EB` (Blue-600) | Secondary links, interactive focus rings, quick-add triggers. |
| `status-success` | `#16A34A` (Green-600) | Verified receipts, confirmed supplies, approved stock counts. |
| `status-warning` | `#D97706` (Amber-600) | Open discrepancies, pending approvals, partial fulfillment. |
| `status-danger` | `#DC2626` (Red-600) | Rejections, cancellations, insufficient stock alerts. |

---

## 3. Core Component Specifications

### 3.1 Quick-Add Dialog Pattern (In-Form Master Data Creation)
When creating an Item, users frequently encounter unlisted categories or units. The UI must support inline creation without abandoning the form:

```
[ Item Form ]
     │
     ├── "القسم" [ Select Dropdown ] ──► Click "+ إضافة قسم"
     │                                           │
     │                                           ▼
     │                               ┌───────────────────────────┐
     │                               │ Modal: إضافة قسم جديد     │
     │                               │ Name (AR): [           ]  │
     │                               │ [ حفظ وإدراج تلقائي ]     │
     │                               └─────────────┬─────────────┘
     │                                             │
     │ ◄── (Option dynamically added & selected) ──┘
```

### 3.2 Enterprise Data Tables
- **Mobile Responsiveness:** Wide operational tables collapse into stacked key-value cards on mobile viewports (< 768px).
- **Sticky Headers & Keyset Pagination:** Sticky column headers for scrolling; clear next/prev cursor pagination controls.
- **Empty & Error States:** Never show a blank white box or fake zeros. Always display explicit Arabic empty states (`"لا توجد سجلات مطابقة"`) with a retry button.

### 3.3 Status Badges (Semantic Arabized Badges)
- `مسودة` (Draft) — Neutral Slate Badge
- `مقدم للمستودع` (Submitted) — Blue Badge
- `تم الشحن` (Dispatched) — Amber Badge
- `تم تأكيد الاستلام` (Confirmed) — Green Badge
- `يوجد فروقات` (Discrepancy) — Orange Badge
- `معتمد` (Approved) — Emerald Badge
- `ملغي / معكوس` (Reversed) — Red Badge
