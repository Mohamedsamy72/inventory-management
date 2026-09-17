/**
 * The sole owner of number, currency, date, quantity, and phone formatting
 * (docs/31 section 4.5). Ad-hoc `toLocaleString` calls in components are forbidden -
 * they are how a codebase ends up with three date formats. Every rule here traces to a
 * row in that table; do not "improve" a format without updating the doc first.
 */

const NUMBER_LOCALE = 'en-US';

/**
 * Western Arabic numerals throughout (docs/31 4.5) - `en-US` locale is used
 * deliberately for its grouping/decimal convention (`,` thousands, `.` decimal), NOT
 * for language. Warehouse and kitchen staff read scale displays and supplier invoices
 * in Western digits; Eastern Arabic-Indic numerals would force constant mental
 * transliteration.
 */
export function formatNumber(value: number, fractionDigits = 0): string {
  return value.toLocaleString(NUMBER_LOCALE, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  });
}

/** `15 كجم` - number then Arabic unit, single space. */
export function formatQuantity(value: number, unitArabicName: string): string {
  return `${formatNumber(value, 2)} ${unitArabicName}`;
}

/**
 * `1,250.50 ج.م` - two decimals, Owner-only. Callers must not invoke this for any
 * non-Owner viewer; the API layer already omits cost fields entirely for everyone
 * else, so this only ever runs against a value the caller was actually allowed to see.
 */
export function formatCurrency(value: number): string {
  return `${formatNumber(value, 2)} ج.م`;
}

/** `12.5%` - the whole pair is `dir="ltr"`; apply that at the call site's markup. */
export function formatPercentage(value: number): string {
  return `${formatNumber(value, 1)}%`;
}

/** `10/09/2026` - Gregorian, `DD/MM/YYYY`. Hijri is not displayed in v1.0. */
export function formatDate(value: Date | string): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const year = date.getFullYear();
  return `${day}/${month}/${year}`;
}

/**
 * `10/09/2026 - 14:30` (24-hour), rendered in the tenant timezone. The value arriving
 * here is already the tenant's local wall-clock time - conversion from the UTC the API
 * transmits happens once, at the API client boundary, never per-formatter-call.
 */
export function formatDateTime(value: Date | string): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${formatDate(date)} - ${hours}:${minutes}`;
}

/**
 * `منذ 5 دقائق` - dashboards only, not for tabular/document timestamps (those use
 * {@link formatDateTime}). Coarse buckets deliberately - a live-updating relative
 * clock is a Phase F6 dashboard concern, not this module's.
 */
export function formatRelativeTime(value: Date | string, now: Date = new Date()): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const diffSeconds = Math.max(0, Math.floor((now.getTime() - date.getTime()) / 1000));

  if (diffSeconds < 60) {
    return 'منذ لحظات';
  }

  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) {
    return `منذ ${formatNumber(diffMinutes)} دقيقة`;
  }

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) {
    return `منذ ${formatNumber(diffHours)} ساعة`;
  }

  const diffDays = Math.floor(diffHours / 24);
  return `منذ ${formatNumber(diffDays)} يوم`;
}

/** `010 1234 5678` - grouped; render with `dir="ltr"` at the call site. */
export function formatMobileNumber(value: string): string {
  const digits = value.replace(/\D/g, '');
  if (digits.length !== 11) {
    return value;
  }

  return `${digits.slice(0, 3)} ${digits.slice(3, 7)} ${digits.slice(7)}`;
}
