import { describe, expect, it } from 'vitest';
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  formatMobileNumber,
  formatNumber,
  formatPercentage,
  formatQuantity,
  formatRelativeTime,
} from '@/lib/formatters';

/**
 * docs/31 section 4.5 - the sole owner of every number/date/quantity/currency/phone
 * format in the product. Every case here traces directly to a row in that table.
 */
describe('formatNumber', () => {
  it('uses Western Arabic numerals with comma grouping and dot decimal', () => {
    expect(formatNumber(1250.5, 2)).toBe('1,250.50');
  });

  it('defaults to zero fraction digits', () => {
    expect(formatNumber(12500)).toBe('12,500');
  });
});

describe('formatQuantity', () => {
  it('renders number then Arabic unit with a single space', () => {
    expect(formatQuantity(15, 'كجم')).toBe('15.00 كجم');
  });
});

describe('formatCurrency', () => {
  it('renders two decimals with the ج.م suffix', () => {
    expect(formatCurrency(1250.5)).toBe('1,250.50 ج.م');
  });
});

describe('formatPercentage', () => {
  it('renders one decimal with a percent sign', () => {
    expect(formatPercentage(12.5)).toBe('12.5%');
  });
});

describe('formatDate', () => {
  it('renders Gregorian DD/MM/YYYY', () => {
    expect(formatDate(new Date(2026, 8, 10))).toBe('10/09/2026');
  });

  it('accepts an ISO string', () => {
    expect(formatDate('2026-09-10T00:00:00Z')).toMatch(/^\d{2}\/\d{2}\/2026$/);
  });
});

describe('formatDateTime', () => {
  it('renders DD/MM/YYYY - HH:mm in 24-hour time', () => {
    expect(formatDateTime(new Date(2026, 8, 10, 14, 30))).toBe('10/09/2026 - 14:30');
  });
});

describe('formatRelativeTime', () => {
  const now = new Date(2026, 8, 10, 12, 0, 0);

  it('renders "منذ لحظات" for very recent times', () => {
    expect(formatRelativeTime(new Date(2026, 8, 10, 11, 59, 50), now)).toBe('منذ لحظات');
  });

  it('renders minutes', () => {
    expect(formatRelativeTime(new Date(2026, 8, 10, 11, 55, 0), now)).toBe('منذ 5 دقيقة');
  });

  it('renders hours', () => {
    expect(formatRelativeTime(new Date(2026, 8, 10, 9, 0, 0), now)).toBe('منذ 3 ساعة');
  });

  it('renders days', () => {
    expect(formatRelativeTime(new Date(2026, 8, 8, 12, 0, 0), now)).toBe('منذ 2 يوم');
  });
});

describe('formatMobileNumber', () => {
  it('groups an 11-digit number as 3-4-4', () => {
    expect(formatMobileNumber('01012345678')).toBe('010 1234 5678');
  });

  it('returns the input unchanged if not 11 digits', () => {
    expect(formatMobileNumber('123')).toBe('123');
  });
});
