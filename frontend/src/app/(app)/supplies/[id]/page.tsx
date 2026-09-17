'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatQuantity } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { StatusBadge } from '@/components/domain/status-badge';

interface SupplyLine {
  id: string;
  itemId: string;
  dispatchedQuantity: number;
  receivedQuantity: number | null;
  unitId: string;
}

interface Supply {
  id: string;
  documentNumber: string;
  warehouseId: string;
  restaurantId: string;
  status: 'Prepared' | 'Dispatched' | 'Confirmed' | 'ConfirmedWithDiscrepancy' | 'RejectedAtDelivery' | 'Cancelled';
  lines: SupplyLine[];
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/**
 * Task F5 (guide §8.4). Dispatch is a plain status-transition action - the dispatched
 * quantities are exactly what fulfilment already set, so there is no dispatch-time form.
 * Confirmation clamps received quantity to `0..dispatched`, shows a pre-submit summary of
 * every variance, and submits once with a stable idempotency key. There is no per-line
 * "reason" field here even though the guide's prose mentions one: the actual
 * `ConfirmSupplyLineCommand` contract (docs/09 task 11.2, verified against the real backend)
 * carries no such field - a variance instead surfaces automatically as a
 * `SupplyReceiptVariance` discrepancy (Phase 12), where a reason is captured at RESOLVE time
 * via `/discrepancies/{id}/resolve`, not at confirm time. Inventing an unsent reason field here
 * would be exactly the UI theater docs/12 §2.3 forbids.
 */
export default function SupplyPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();

  const [supply, setSupply] = useState<Supply | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [items, setItems] = useState<NamedOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<Supply>(`/api/v1/supplies/${params.id}`);
      setSupply(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل التوريد');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
    apiClient.get<{ items: NamedOption[] }>('/api/v1/items?limit=200').then((page) => setItems(page.items)).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => undefined);
  }, [params.id]);

  const [isDispatching, setIsDispatching] = useState(false);
  const [dispatchError, setDispatchError] = useState<string | null>(null);
  const [isCancelling, setIsCancelling] = useState(false);

  const [receivedQuantities, setReceivedQuantities] = useState<Record<string, string>>({});
  const [summaryOpen, setSummaryOpen] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [isConfirming, setIsConfirming] = useState(false);
  const [confirmKey] = useState(() => crypto.randomUUID());

  if (profile && !profile.permissionCodes.includes('supplies:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canDispatch = profile?.permissionCodes.includes('supplies:dispatch') ?? false;
  const canConfirm = profile?.permissionCodes.includes('supplies:confirm') ?? false;
  const canCancel = profile?.permissionCodes.includes('supply_requests:fulfill') ?? false;

  function itemName(id: string): string {
    return items.find((item) => item.id === id)?.nameArabic ?? '—';
  }

  function unitName(id: string): string {
    return units.find((unit) => unit.id === id)?.nameArabic ?? '—';
  }

  async function handleDispatch() {
    setDispatchError(null);
    setIsDispatching(true);
    try {
      await apiClient.post(`/api/v1/supplies/${supply!.id}/dispatch`);
      await load();
    } catch (caught) {
      setDispatchError(caught instanceof ApiError ? caught.messageAr : 'تعذر شحن التوريد');
    } finally {
      setIsDispatching(false);
    }
  }

  async function handleCancel() {
    setIsCancelling(true);
    try {
      await apiClient.post(`/api/v1/supplies/${supply!.id}/cancel`);
      await load();
    } finally {
      setIsCancelling(false);
    }
  }

  function receivedFor(line: SupplyLine): number {
    const raw = receivedQuantities[line.id];
    const value = raw === undefined ? line.dispatchedQuantity : Number(raw);
    return Math.min(Math.max(value, 0), line.dispatchedQuantity);
  }

  async function handleConfirm() {
    setConfirmError(null);
    setIsConfirming(true);
    try {
      const lines = supply!.lines.map((line) => ({ supplyItemId: line.id, receivedQuantity: receivedFor(line) }));
      await apiClient.post(
        `/api/v1/supplies/${supply!.id}/confirm`,
        { lines },
        { headers: { 'X-Idempotency-Key': confirmKey } },
      );
      setSummaryOpen(false);
      await load();
    } catch (caught) {
      setConfirmError(caught instanceof ApiError ? caught.messageAr : 'تعذر تأكيد الاستلام');
    } finally {
      setIsConfirming(false);
    }
  }

  if (isLoading) {
    return <LoadingSkeleton className="h-64 w-full" />;
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (!supply) {
    return null;
  }

  const varianceLines = supply.lines.filter((line) => receivedFor(line) !== line.dispatchedQuantity);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground" dir="ltr">
            {supply.documentNumber}
          </h1>
          <p className="text-sm text-muted-foreground">
            {restaurants.find((restaurant) => restaurant.id === supply.restaurantId)?.nameArabic ?? '—'}
            {' · '}
            {warehouses.find((warehouse) => warehouse.id === supply.warehouseId)?.nameArabic ?? '—'}
          </p>
        </div>
        <StatusBadge entity="Supply" status={supply.status} />
      </div>

      <div className="overflow-auto rounded-lg border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              {supply.status === 'Dispatched' && canConfirm ? <TableHead>الكمية المستلمة</TableHead> : null}
              {supply.status !== 'Prepared' ? <TableHead>المستلم</TableHead> : null}
              <TableHead>المشحون</TableHead>
              <TableHead>الوحدة</TableHead>
              <TableHead>الصنف</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {supply.lines.map((line) => (
              <TableRow key={line.id}>
                {supply.status === 'Dispatched' && canConfirm ? (
                  <TableCell>
                    <Input
                      type="number"
                      min="0"
                      max={line.dispatchedQuantity}
                      step="any"
                      dir="ltr"
                      className="w-24"
                      aria-label={`الكمية المستلمة - ${itemName(line.itemId)}`}
                      value={receivedQuantities[line.id] ?? String(line.dispatchedQuantity)}
                      onChange={(event) => setReceivedQuantities((current) => ({ ...current, [line.id]: event.target.value }))}
                    />
                  </TableCell>
                ) : null}
                {supply.status !== 'Prepared' ? <TableCell>{line.receivedQuantity ?? '—'}</TableCell> : null}
                <TableCell>{formatQuantity(line.dispatchedQuantity, unitName(line.unitId))}</TableCell>
                <TableCell>{unitName(line.unitId)}</TableCell>
                <TableCell>{itemName(line.itemId)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {dispatchError ? <ErrorBanner message={dispatchError} /> : null}

      <div className="flex items-center gap-2">
        {supply.status === 'Prepared' && canDispatch ? (
          <Button loading={isDispatching} onClick={handleDispatch}>
            شحن التوريد للفرع
          </Button>
        ) : null}
        {supply.status === 'Prepared' && canCancel ? (
          <Button variant="destructive" loading={isCancelling} onClick={handleCancel}>
            إلغاء التوريد
          </Button>
        ) : null}
        {supply.status === 'Dispatched' && canConfirm ? (
          <Button onClick={() => setSummaryOpen(true)}>تأكيد الاستلام</Button>
        ) : null}
        <Button variant="ghost" onClick={() => router.push('/supplies')}>
          رجوع لقائمة التوريدات
        </Button>
      </div>

      <Dialog open={summaryOpen} onOpenChange={setSummaryOpen}>
        <DialogContent>
          <div className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>تأكيد استلام التوريد</DialogTitle>
              <DialogDescription>راجع الفروقات قبل التأكيد - هذا الإجراء لا يمكن التراجع عنه.</DialogDescription>
            </DialogHeader>

            {confirmError ? <ErrorBanner message={confirmError} /> : null}

            {varianceLines.length > 0 ? (
              <div className="flex flex-col gap-1.5 rounded-lg border border-badge-amber-bg bg-badge-amber-bg/30 p-3 text-sm">
                <p className="font-medium text-foreground">يوجد فروقات في الكميات المستلمة:</p>
                {varianceLines.map((line) => (
                  <p key={line.id}>
                    {itemName(line.itemId)}: المشحون {formatQuantity(line.dispatchedQuantity, unitName(line.unitId))} ، المستلم{' '}
                    {formatQuantity(receivedFor(line), unitName(line.unitId))}
                  </p>
                ))}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">جميع الكميات مطابقة للمشحون - لا توجد فروقات.</p>
            )}

            <DialogFooter>
              <Button loading={isConfirming} onClick={handleConfirm}>
                تأكيد الاستلام نهائياً
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
