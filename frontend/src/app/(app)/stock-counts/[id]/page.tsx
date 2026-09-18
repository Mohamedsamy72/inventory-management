'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatDateTime, formatQuantity } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { StatusBadge } from '@/components/domain/status-badge';

interface StockCountLine {
  id: string;
  itemId: string;
  systemQuantity: number | null;
  physicalQuantity: number | null;
  variance: number | null;
  baseUnitId: string;
  notes: string | null;
}

interface StockCount {
  id: string;
  documentNumber: string;
  warehouseId: string;
  status: 'Draft' | 'InProgress' | 'PendingApproval' | 'Approved' | 'Rejected';
  isBlindCount: boolean;
  openedAt: string;
  approvedAt: string | null;
  lines: StockCountLine[];
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface ItemOption extends NamedOption {
  baseUnitId: string;
}

/** docs/09 Phase 13 detail/record/approve screen. Blind counts (task 13.4) never receive
 * `systemQuantity`/`variance` from the server while `InProgress` - rendered as "—", never
 * derived or guessed client-side, matching the backend's own leak-prevention design. */
export default function StockCountPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();

  const [count, setCount] = useState<StockCount | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [items, setItems] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<StockCount>(`/api/v1/stock-counts/${params.id}`);
      setCount(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل الجرد');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
    apiClient.get<{ items: ItemOption[] }>('/api/v1/items?limit=200').then((page) => setItems(page.items)).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => undefined);
  }, [params.id]);

  const [physicalQuantities, setPhysicalQuantities] = useState<Record<string, string>>({});
  const [recordError, setRecordError] = useState<string | null>(null);
  const [isRecording, setIsRecording] = useState(false);

  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitKey] = useState(() => crypto.randomUUID());

  const [approveError, setApproveError] = useState<string | null>(null);
  const [isApproving, setIsApproving] = useState(false);
  const [approveKey] = useState(() => crypto.randomUUID());

  const [rejectError, setRejectError] = useState<string | null>(null);
  const [isRejecting, setIsRejecting] = useState(false);

  if (profile && !profile.permissionCodes.includes('stock_counts:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCount = profile?.permissionCodes.includes('stock_counts:count') ?? false;
  const canApprove = profile?.permissionCodes.includes('stock_counts:approve') ?? false;

  function warehouseName(id: string): string {
    return warehouses.find((w) => w.id === id)?.nameArabic ?? '—';
  }

  function itemName(id: string): string {
    return items.find((i) => i.id === id)?.nameArabic ?? '—';
  }

  function unitName(id: string): string {
    return units.find((u) => u.id === id)?.nameArabic ?? '—';
  }

  async function handleRecord() {
    if (!count) return;
    setRecordError(null);
    setIsRecording(true);
    try {
      const lines = Object.entries(physicalQuantities)
        .filter(([, value]) => value !== '')
        .map(([lineId, value]) => ({ lineId, physicalQuantity: Number(value) }));
      await apiClient.post(`/api/v1/stock-counts/${count.id}/record`, { lines });
      setPhysicalQuantities({});
      await load();
    } catch (caught) {
      setRecordError(caught instanceof ApiError ? caught.messageAr : 'تعذر تسجيل الكميات');
    } finally {
      setIsRecording(false);
    }
  }

  async function handleSubmit() {
    if (!count) return;
    setSubmitError(null);
    setIsSubmitting(true);
    try {
      await apiClient.post(`/api/v1/stock-counts/${count.id}/submit`, undefined, {
        headers: { 'X-Idempotency-Key': submitKey },
      });
      await load();
    } catch (caught) {
      setSubmitError(caught instanceof ApiError ? caught.messageAr : 'تعذر رفع الجرد للاعتماد');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleApprove() {
    if (!count) return;
    setApproveError(null);
    setIsApproving(true);
    try {
      await apiClient.post(`/api/v1/stock-counts/${count.id}/approve`, undefined, {
        headers: { 'X-Idempotency-Key': approveKey },
      });
      await load();
    } catch (caught) {
      setApproveError(caught instanceof ApiError ? caught.messageAr : 'تعذر اعتماد الجرد');
    } finally {
      setIsApproving(false);
    }
  }

  async function handleReject() {
    if (!count) return;
    setRejectError(null);
    setIsRejecting(true);
    try {
      await apiClient.post(`/api/v1/stock-counts/${count.id}/reject`);
      await load();
    } catch (caught) {
      setRejectError(caught instanceof ApiError ? caught.messageAr : 'تعذر رفض الجرد');
    } finally {
      setIsRejecting(false);
    }
  }

  if (isLoading) {
    return <LoadingSkeleton className="h-64 w-full" />;
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (!count) {
    return null;
  }

  const showSystemQuantity = !count.isBlindCount || count.status === 'PendingApproval' || count.status === 'Approved';

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground" dir="ltr">
            {count.documentNumber}
          </h1>
          <p className="text-sm text-muted-foreground">
            {warehouseName(count.warehouseId)} · {formatDateTime(count.openedAt)}
            {count.isBlindCount ? ' · جرد بدون عرض الكميات الدفترية' : ''}
          </p>
        </div>
        <StatusBadge entity="StockCount" status={count.status} />
      </div>

      <div className="overflow-auto rounded-lg border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              {count.status !== 'InProgress' ? <TableHead>الفرق</TableHead> : null}
              <TableHead>الكمية الفعلية</TableHead>
              {showSystemQuantity ? <TableHead>الكمية الدفترية</TableHead> : null}
              <TableHead>الوحدة</TableHead>
              <TableHead>الصنف</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {count.lines.map((line) => (
              <TableRow key={line.id}>
                {count.status !== 'InProgress' ? (
                  <TableCell className={line.variance !== null && line.variance !== 0 ? 'text-destructive' : undefined}>
                    {line.variance !== null ? formatQuantity(line.variance, '') : '—'}
                  </TableCell>
                ) : null}
                <TableCell>
                  {count.status === 'InProgress' && canCount ? (
                    <Input
                      type="number"
                      step="any"
                      dir="ltr"
                      className="w-24"
                      aria-label={`الكمية الفعلية - ${itemName(line.itemId)}`}
                      value={physicalQuantities[line.id] ?? (line.physicalQuantity !== null ? String(line.physicalQuantity) : '')}
                      onChange={(event) => setPhysicalQuantities((current) => ({ ...current, [line.id]: event.target.value }))}
                    />
                  ) : (
                    (line.physicalQuantity ?? '—')
                  )}
                </TableCell>
                {showSystemQuantity ? <TableCell>{line.systemQuantity ?? '—'}</TableCell> : null}
                <TableCell>{unitName(line.baseUnitId)}</TableCell>
                <TableCell>{itemName(line.itemId)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {recordError ? <ErrorBanner message={recordError} /> : null}
      {submitError ? <ErrorBanner message={submitError} /> : null}
      {approveError ? <ErrorBanner message={approveError} /> : null}
      {rejectError ? <ErrorBanner message={rejectError} /> : null}

      <div className="flex flex-wrap items-center gap-2">
        {count.status === 'InProgress' && canCount ? (
          <Button loading={isRecording} onClick={handleRecord} disabled={Object.keys(physicalQuantities).length === 0}>
            حفظ الكميات المسجلة
          </Button>
        ) : null}
        {count.status === 'InProgress' && canCount ? (
          <Button variant="outline" loading={isSubmitting} onClick={handleSubmit}>
            رفع الجرد للاعتماد
          </Button>
        ) : null}
        {count.status === 'PendingApproval' && canApprove ? (
          <Button loading={isApproving} onClick={handleApprove}>
            اعتماد الجرد وتسوية الفروقات
          </Button>
        ) : null}
        {count.status === 'PendingApproval' && canApprove ? (
          <Button variant="destructive" loading={isRejecting} onClick={handleReject}>
            رفض الجرد وإعادة العد
          </Button>
        ) : null}
        <Button variant="ghost" onClick={() => router.push('/stock-counts')}>
          رجوع لقائمة عمليات الجرد
        </Button>
      </div>
    </div>
  );
}
