import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

/**
 * The docs/31 section 4.6 canonical status vocabulary. Server enums are English; the
 * UI renders only the Arabic label - this mapping is authoritative and must not be
 * duplicated per screen. Adding a new entity/status pair means adding a row here,
 * never inventing a label inline at a call site.
 */

export type StatusEntity = 'ReceivingOrder' | 'SupplyRequest' | 'Supply' | 'StockCount' | 'Discrepancy';

type BadgeTone = 'neutral' | 'blue' | 'green' | 'amber' | 'orange' | 'red';

interface StatusMapping {
  label: string;
  tone: BadgeTone;
}

const STATUS_MAP: Record<StatusEntity, Record<string, StatusMapping>> = {
  ReceivingOrder: {
    Draft: { label: 'مسودة', tone: 'neutral' },
    Submitted: { label: 'مرحل للمخزن', tone: 'blue' },
    Verified: { label: 'تمت المطابقة', tone: 'green' },
    Reversed: { label: 'معكوس', tone: 'red' },
  },
  SupplyRequest: {
    Draft: { label: 'مسودة', tone: 'neutral' },
    Submitted: { label: 'مقدم للمستودع', tone: 'blue' },
    PartiallyFulfilled: { label: 'تم تجهيز جزء من الطلب', tone: 'amber' },
    Fulfilled: { label: 'تم تجهيز الطلب بالكامل', tone: 'green' },
    Cancelled: { label: 'ملغي', tone: 'red' },
  },
  Supply: {
    Prepared: { label: 'قيد التجهيز', tone: 'neutral' },
    Dispatched: { label: 'تم الشحن / في الطريق', tone: 'amber' },
    Confirmed: { label: 'تم تأكيد الاستلام', tone: 'green' },
    ConfirmedWithDiscrepancy: { label: 'تم الاستلام مع وجود فروقات', tone: 'orange' },
    RejectedAtDelivery: { label: 'مرفوض عند التسليم', tone: 'red' },
    Cancelled: { label: 'ملغي', tone: 'red' },
  },
  StockCount: {
    Draft: { label: 'مسودة', tone: 'neutral' },
    InProgress: { label: 'جاري العد', tone: 'blue' },
    PendingApproval: { label: 'بانتظار الاعتماد', tone: 'amber' },
    Approved: { label: 'معتمد وتمت التسوية', tone: 'green' },
    Rejected: { label: 'مرفوض — إعادة الجرد', tone: 'red' },
  },
  Discrepancy: {
    Open: { label: 'مفتوح', tone: 'amber' },
    Investigating: { label: 'قيد المراجعة', tone: 'blue' },
    Resolved: { label: 'تمت المعالجة', tone: 'green' },
  },
};

const TONE_CLASSES: Record<BadgeTone, string> = {
  neutral: 'bg-badge-neutral-bg text-badge-neutral-fg',
  blue: 'bg-badge-blue-bg text-badge-blue-fg',
  green: 'bg-badge-green-bg text-badge-green-fg',
  amber: 'bg-badge-amber-bg text-badge-amber-fg',
  orange: 'bg-badge-orange-bg text-badge-orange-fg',
  red: 'bg-badge-red-bg text-badge-red-fg',
};

export interface StatusBadgeProps {
  entity: StatusEntity;
  /** The raw server enum value, e.g. `"PartiallyFulfilled"` - never pre-translated. */
  status: string;
  className?: string;
}

/**
 * Renders a docs/31 4.6 status as an Arabic-labelled, tone-colored badge. Falls back to
 * the raw enum string (never blank) if a status is missing from the map - a loud,
 * visible gap is preferable to a silently blank badge if the server ever adds a status
 * this map has not been updated for yet.
 */
export function StatusBadge({ entity, status, className }: StatusBadgeProps) {
  const mapping = STATUS_MAP[entity][status];

  return (
    <Badge className={cn('border-transparent', mapping ? TONE_CLASSES[mapping.tone] : TONE_CLASSES.neutral, className)}>
      {mapping?.label ?? status}
    </Badge>
  );
}
