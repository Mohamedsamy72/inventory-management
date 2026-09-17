/**
 * docs/31 §4.2 point 3 / guide §8.3 point 3: text filters normalize the query with these
 * same rules before sending, so a user typing "جبنه" finds "جبنة" - the backend applies
 * the identical normalization to `name_normalized` (docs/29 §5.4), so the two sides agree.
 * Diacritics/tatweel stripped, alef forms unified, ة→ه and ى→ي **for matching only** -
 * never applied to a value before it is displayed or stored.
 */
export function normalizeArabicSearch(value: string): string {
  return value
    .replace(/[ً-ْـ]/g, '')
    .replace(/[أإآ]/g, 'ا')
    .replace(/ة/g, 'ه')
    .replace(/ى/g, 'ي')
    .trim();
}
