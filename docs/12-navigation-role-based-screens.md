# 12 — Navigation & Role-Based Screen Specifications

> **Document ID:** SPEC-12  
> **Topic:** Role-Tailored Navigation Menus, Dashboard Objectives, and Zero-Fake-Data Rules

---

## 1. Role-Tailored Navigation Menus

To eliminate cognitive clutter, navigation is rendered dynamically based on the authenticated user's role and permissions.

### 1.1 Owner Navigation Menu (Full Access)
- 📊 **لوحة التحكم الرئيسية** (`/dashboard`)
- 📦 **الأصناف** (`/items`)
- 🏷️ **الأقسام والوحدات** (`/master-data`)
- 🏢 **المخازن والفروع** (`/locations`)
- 🚚 **الموردون** (`/suppliers`)
- 📥 **الداخل الى المخزن** (`/receiving`)
- 📝 **طلبات البضاعه** (`/supply-requests`)
- 🚛 **الصادر الى المطعم** (`/supplies`)
- 📋 **الجرد الفعلي** (`/stock-counts`)
- ⚠️ **سجل الفروقات** (`/discrepancies`)
- 🍳 **سجلات الاستهلاك** (`/consumption`)
- 📈 **التقارير المالية والمخزنية** (`/reports`) — 🚫 *deferred (ADR-029); the entry is **not rendered** until the reports phase ships*
- 🛡️ **سجل التدقيق** (`/audit`)
- 👥 **ادارة المستخدمين** (`/users`)
- ⚙️ **إعدادات المنشأة** (`/settings`)

---

### 1.2 Admin Navigation Menu (Operational Only — NO Financials/Audit)
- 📊 **لوحة التحكم التشغيلية** (`/dashboard`)
- 📦 **الأصناف** (`/items`)
- 🏷️ **الأقسام والوحدات** (`/master-data`)
- 🏢 **المخازن والفروع** (`/locations`)
- 🚚 **الموردون** (`/suppliers`)
- 📥 **الداخل الى المخزن** (`/receiving`)
- 📝 **طلبات البضاعه** (`/supply-requests`)
- 🚛 **الصادر الى المطعم** (`/supplies`)
- 📋 **الجرد الفعلي** (`/stock-counts`)
- ⚠️ **سجل الفروقات** (`/discrepancies`)
- 👥 **ادارة المستخدمين** (`/users`)
- ⚙️ **الإعدادات التشغيلية** (`/settings`)
- ❌ *(Hidden & Forbidden: سجل التدقيق، البيانات المالية، التكاليف)*

---

### 1.3 Warehouse Staff Navigation Menu (Logistics Execution)
- ⚡ **مهام المخزن** (`/dashboard`)
- 📥 **الداخل الى المخزن** (`/receiving`)
- 📝 **الطلبيات الواردة من المطعم** (`/supply-requests`)
- 🚛 **الصادر الى المطعم** (`/supplies`)

---

### 1.4 Restaurant Supervisor Navigation Menu (Branch Requisitions & Confirmation)
- ⚡ **مهام الفرع** (`/dashboard`)
- 📝 **طلبات البضاعه** (`/supply-requests`)
- 🚛 **تأكيد استلام البضاعه** (`/supplies`)
- ⚠️ **فروقات الاستلام** (`/discrepancies`)

---

## 2. Action-Oriented Dashboard Specifications

Dashboards are designed as operational action centers answering the immediate question: **"What needs my attention right now?"**

### 2.1 Warehouse Staff Dashboard (لوحة مهام المخزن)
- **Primary Question:** "ما الذي يحتاج إلى تلبية أو استلام الآن في المخزن؟"
- **Key Widgets:**
  - `الطلبيات الواردة من المطعم بانتظار التجهيز` (Count & list of pending restaurant requests).
  - `شحنات الموردين الداخلة الى المخزن` (Incoming receiving orders in Draft/Submitted).
  - `فروقات الاستلام المفتوحة` (Active discrepancies requiring review).
- **Primary CTAs:**
  - `[ + استلام وارد جديد ]`
  - `[ معالجة طلبيات المطعم ]`

### 2.2 Restaurant Supervisor Dashboard (لوحة مهام الفرع)
- **Primary Question:** "ما الذي يجب أن أطلبه أو أؤكد استلامه اليوم للفرع؟"
- **Key Widgets:**
  - `الصادر من المخزن بانتظار تأكيد الاستلام` (Dispatched supplies arriving at branch).
  - `طلبات البضاعه المقدمة للمخزن` (Status tracker of active branch orders).
  - `فروقات استلام معلقة` (Open receipt discrepancies).
- **Primary CTAs:**
  - `[ + طلب بضاعه جديد ]`
  - `[ تأكيد استلام البضاعه ]`

### 2.3 Zero Fake Data Invariant
- **Strict Rule:** Dashboards must **never** display hardcoded mock zeros (`0`), fake placeholder charts, or estimated sums.
- If an endpoint returns an error, the widget displays an error banner with a retry button.
- If no records exist, the widget displays a clean empty state: `"لا توجد طلبات معلقة حالياً"`.

---

## 3. Reconciliation Amendment — Deferred Surfaces (ADR-029, CR-094)

**Reports.** The Owner navigation entry `التقارير المالية والمخزنية` is retained above as a **future** item and is **not rendered** until the reports phase ships. An unimplemented route is never shown — not as a disabled item, not as a "coming soon" screen, not as an empty chart. Registering a route that renders a fabricated surface would violate §2.3 as directly as a hardcoded zero would.

**Files.** No attachment control, upload button, or evidence panel appears on any screen, because no approved workflow requires an attachment (ADR-029).

**Serving warehouse (ADR-028).** The Restaurant Supervisor's `طلب بضاعه جديد` screen contains **no warehouse selector**. The serving warehouse is derived by the server and shown read-only on the created request, so the supervisor can see where their request went without being able to change it.

**Zero fake data, restated.** §2.3 is not limited to dashboards. It governs every surface: a figure, chart, table, or route exists only when it is backed by real data the server returned.
