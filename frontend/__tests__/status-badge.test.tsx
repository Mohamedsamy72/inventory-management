import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { StatusBadge } from '@/components/domain/status-badge';

/**
 * docs/31 section 4.6 - the canonical status vocabulary is authoritative and must not
 * be duplicated per screen. These tests pin every entity/status pair to its documented
 * Arabic label so a future edit to the map is caught immediately, not discovered on a
 * screen months later.
 */
describe('StatusBadge', () => {
  it.each([
    ['ReceivingOrder', 'Draft', 'مسودة'],
    ['ReceivingOrder', 'Submitted', 'مرحل للمخزن'],
    ['ReceivingOrder', 'Verified', 'تمت المطابقة'],
    ['ReceivingOrder', 'Reversed', 'معكوس'],
    ['SupplyRequest', 'PartiallyFulfilled', 'تم تجهيز جزء من الطلب'],
    ['SupplyRequest', 'Fulfilled', 'تم تجهيز الطلب بالكامل'],
    ['SupplyRequest', 'Cancelled', 'ملغي'],
    ['Supply', 'Dispatched', 'تم الشحن / في الطريق'],
    ['Supply', 'ConfirmedWithDiscrepancy', 'تم الاستلام مع وجود فروقات'],
    ['Supply', 'RejectedAtDelivery', 'مرفوض عند التسليم'],
    ['StockCount', 'InProgress', 'جاري العد'],
    ['StockCount', 'Approved', 'معتمد وتمت التسوية'],
    ['Discrepancy', 'Open', 'مفتوح'],
    ['Discrepancy', 'Resolved', 'تمت المعالجة'],
  ] as const)('renders %s.%s as "%s"', (entity, status, expectedLabel) => {
    const { getByText } = render(<StatusBadge entity={entity} status={status} />);
    expect(getByText(expectedLabel)).not.toBeNull();
  });

  it('never renders the raw enum for a mapped status', () => {
    const { queryByText } = render(<StatusBadge entity="ReceivingOrder" status="Verified" />);
    expect(queryByText('Verified')).toBeNull();
  });

  it('falls back to the raw enum for an unmapped status rather than rendering blank', () => {
    const { getByText } = render(<StatusBadge entity="ReceivingOrder" status="SomeFutureStatus" />);
    expect(getByText('SomeFutureStatus')).not.toBeNull();
  });
});
