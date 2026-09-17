'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Plus, Trash2, TriangleAlert } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatQuantity } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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

interface SupplyRequestLine {
  id: string;
  itemId: string;
  requestedQuantity: number;
  fulfilledQuantity: number;
  unitId: string;
  notes: string | null;
}

interface SupplyRequest {
  id: string;
  documentNumber: string;
  restaurantId: string;
  warehouseId: string;
  status: 'Draft' | 'Submitted' | 'PartiallyFulfilled' | 'Fulfilled' | 'Cancelled';
  lines: SupplyRequestLine[];
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface ItemOption extends NamedOption {
  baseUnitId: string;
}

interface ConversionSummary {
  fromUnitId: string;
  isActive: boolean;
}

interface WarehouseStockLine {
  itemId: string;
  available: number;
}

/** Task F5 - "the single most important screen in the product" (guide §8.3): the multi-item
 * request editor (Draft, Restaurant Supervisor) and the fulfilment panel (Submitted/
 * PartiallyFulfilled, Warehouse Staff) live on the same page, since both act on the same
 * document and never overlap in time (a Supervisor cannot edit once Submitted; Warehouse
 * Staff cannot fulfil a Draft). No warehouse selector anywhere (ADR-028) - `request.warehouseId`
 * is read-only, server-resolved, and only ever displayed.
 */
export default function SupplyRequestPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();

  const [request, setRequest] = useState<SupplyRequest | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [items, setItems] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);
  const [stock, setStock] = useState<WarehouseStockLine[]>([]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<SupplyRequest>(`/api/v1/supply-requests/${params.id}`);
      setRequest(result);
      if (result.warehouseId) {
        apiClient
          .get<WarehouseStockLine[]>(`/api/v1/warehouses/${result.warehouseId}/stock`)
          .then(setStock)
          .catch(() => setStock([]));
      }
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل الطلب');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
    apiClient.get<{ items: ItemOption[] }>('/api/v1/items?limit=200').then((page) => setItems(page.items)).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => undefined);
  }, [params.id]);

  const [lineDialogOpen, setLineDialogOpen] = useState(false);
  const [lineItemId, setLineItemId] = useState('');
  const [lineUnitId, setLineUnitId] = useState('');
  const [lineUnits, setLineUnits] = useState<NamedOption[]>([]);
  const [lineQuantity, setLineQuantity] = useState('');
  const [lineNotes, setLineNotes] = useState('');
  const [lineError, setLineError] = useState<string | null>(null);
  const [isSavingLine, setIsSavingLine] = useState(false);

  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmittingRequest, setIsSubmittingRequest] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);

  const [fulfillQuantities, setFulfillQuantities] = useState<Record<string, string>>({});
  const [fulfillError, setFulfillError] = useState<string | null>(null);
  const [isFulfilling, setIsFulfilling] = useState(false);
  const [shortItemIds, setShortItemIds] = useState<Set<string>>(new Set());

  if (profile && !profile.permissionCodes.includes('supply_requests:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('supply_requests:create') ?? false;
  const canFulfill = profile?.permissionCodes.includes('supply_requests:fulfill') ?? false;

  function itemName(id: string): string {
    return items.find((item) => item.id === id)?.nameArabic ?? '—';
  }

  function unitName(id: string): string {
    return units.find((unit) => unit.id === id)?.nameArabic ?? '—';
  }

  function availableFor(itemId: string): number {
    return stock.find((line) => line.itemId === itemId)?.available ?? 0;
  }

  function openAddLine() {
    setLineItemId('');
    setLineUnitId('');
    setLineUnits([]);
    setLineQuantity('');
    setLineNotes('');
    setLineError(null);
    setLineDialogOpen(true);
  }

  async function handleItemChange(itemId: string) {
    setLineItemId(itemId);
    setLineUnitId('');
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item) {
      setLineUnits([]);
      return;
    }
    try {
      const conversions = await apiClient.get<ConversionSummary[]>(`/api/v1/items/${itemId}/conversions`);
      const fromUnitIds = conversions.filter((conversion) => conversion.isActive).map((conversion) => conversion.fromUnitId);
      const options = [item.baseUnitId, ...fromUnitIds].map((id) => units.find((unit) => unit.id === id)).filter((unit): unit is NamedOption => Boolean(unit));
      setLineUnits(options);
    } catch {
      const baseUnit = units.find((unit) => unit.id === item.baseUnitId);
      setLineUnits(baseUnit ? [baseUnit] : []);
    }
  }

  async function handleAddLine(event: FormEvent) {
    event.preventDefault();
    event.stopPropagation();
    setLineError(null);
    setIsSavingLine(true);

    try {
      // The backend merges into an existing line for the same (itemId, unitId) pair rather
      // than rejecting a duplicate (CR-023) - this call is safe to make even if the item is
      // already on the request; the refetch below shows the merged quantity.
      await apiClient.post(`/api/v1/supply-requests/${request!.id}/items`, {
        itemId: lineItemId,
        unitId: lineUnitId,
        requestedQuantity: Number(lineQuantity),
        notes: lineNotes || null,
      });
      setLineDialogOpen(false);
      await load();
    } catch (caught) {
      setLineError(caught instanceof ApiError ? caught.messageAr : 'تعذر إضافة الصنف');
    } finally {
      setIsSavingLine(false);
    }
  }

  async function handleRemoveLine(lineId: string) {
    await apiClient.delete(`/api/v1/supply-requests/${request!.id}/items/${lineId}`);
    await load();
  }

  async function handleSubmitRequest() {
    setSubmitError(null);
    setIsSubmittingRequest(true);
    try {
      await apiClient.post(`/api/v1/supply-requests/${request!.id}/submit`);
      await load();
    } catch (caught) {
      setSubmitError(caught instanceof ApiError ? caught.messageAr : 'تعذر إرسال الطلب');
    } finally {
      setIsSubmittingRequest(false);
    }
  }

  async function handleCancel() {
    setIsCancelling(true);
    try {
      await apiClient.post(`/api/v1/supply-requests/${request!.id}/cancel`);
      await load();
    } finally {
      setIsCancelling(false);
    }
  }

  function remainingFor(line: SupplyRequestLine): number {
    return line.requestedQuantity - line.fulfilledQuantity;
  }

  async function handleFulfill(event: FormEvent) {
    event.preventDefault();
    setFulfillError(null);
    setIsFulfilling(true);

    try {
      const lines = request!.lines
        .filter((line) => remainingFor(line) > 0)
        .map((line) => ({
          supplyRequestItemId: line.id,
          fulfilledQuantity: Number(fulfillQuantities[line.id] ?? remainingFor(line)),
        }));
      const result = await apiClient.post<{ supply: { id: string }; insufficientStockItemIds: string[] }>(
        `/api/v1/supply-requests/${request!.id}/fulfill`,
        { lines },
      );
      if (result.insufficientStockItemIds.length > 0) {
        setShortItemIds(new Set(result.insufficientStockItemIds));
      }
      router.push(`/supplies/${result.supply.id}`);
    } catch (caught) {
      setFulfillError(caught instanceof ApiError ? caught.messageAr : 'تعذر تجهيز الطلب');
    } finally {
      setIsFulfilling(false);
    }
  }

  if (isLoading) {
    return <LoadingSkeleton className="h-64 w-full" />;
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (!request) {
    return null;
  }

  const isDraft = request.status === 'Draft';
  const canFulfillNow = canFulfill && (request.status === 'Submitted' || request.status === 'PartiallyFulfilled');
  const remainingLines = request.lines.filter((line) => remainingFor(line) > 0);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground" dir="ltr">
            {request.documentNumber}
          </h1>
          <p className="text-sm text-muted-foreground">
            {restaurants.find((restaurant) => restaurant.id === request.restaurantId)?.nameArabic ?? '—'}
            {' · '}
            المخزن المغذي: {warehouses.find((warehouse) => warehouse.id === request.warehouseId)?.nameArabic ?? '—'}
          </p>
        </div>
        <StatusBadge entity="SupplyRequest" status={request.status} />
      </div>

      {!isDraft && request.status !== 'Cancelled' ? (
        <div className="rounded-lg border border-border bg-muted/50 p-3 text-sm text-muted-foreground">
          لا يمكن تعديل الأصناف بعد إرسال الطلب.
        </div>
      ) : null}

      <div className="overflow-auto rounded-lg border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              {isDraft && canCreate ? <TableHead>إجراء</TableHead> : null}
              {!isDraft ? <TableHead>الكمية المجهزة</TableHead> : null}
              <TableHead>الكمية المطلوبة</TableHead>
              <TableHead>الوحدة</TableHead>
              <TableHead>الصنف</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {request.lines.map((line) => (
              <TableRow key={line.id}>
                {isDraft && canCreate ? (
                  <TableCell>
                    <Button variant="ghost" size="icon-sm" onClick={() => handleRemoveLine(line.id)} aria-label="حذف">
                      <Trash2 />
                    </Button>
                  </TableCell>
                ) : null}
                {!isDraft ? <TableCell>{formatQuantity(line.fulfilledQuantity, unitName(line.unitId))}</TableCell> : null}
                <TableCell>{formatQuantity(line.requestedQuantity, unitName(line.unitId))}</TableCell>
                <TableCell>{unitName(line.unitId)}</TableCell>
                <TableCell>{itemName(line.itemId)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {isDraft && canCreate ? (
        <Button variant="outline" onClick={openAddLine} className="self-start">
          <Plus />
          إضافة صنف
        </Button>
      ) : null}

      {submitError ? <ErrorBanner message={submitError} /> : null}

      <div className="flex items-center gap-2">
        {isDraft && canCreate ? (
          <Button loading={isSubmittingRequest} disabled={request.lines.length === 0} onClick={handleSubmitRequest}>
            إرسال الطلب الى المخزن
          </Button>
        ) : null}
        {isDraft && request.lines.length === 0 ? (
          <p className="text-sm text-muted-foreground">أضف صنفاً واحداً على الأقل قبل الإرسال.</p>
        ) : null}
        {canCreate && request.status !== 'Fulfilled' && request.status !== 'Cancelled' ? (
          <Button variant="destructive" loading={isCancelling} onClick={handleCancel}>
            إلغاء الطلب
          </Button>
        ) : null}
        <Button variant="ghost" onClick={() => router.push('/supply-requests')}>
          رجوع لقائمة الطلبات
        </Button>
      </div>

      {canFulfillNow && remainingLines.length > 0 ? (
        <form onSubmit={handleFulfill} className="flex flex-col gap-3 rounded-lg border border-border p-4">
          <h2 className="font-heading text-base font-medium text-foreground">تجهيز الطلب</h2>

          {fulfillError ? <ErrorBanner message={fulfillError} /> : null}

          {remainingLines.map((line) => {
            const remaining = remainingFor(line);
            const value = fulfillQuantities[line.id] ?? String(remaining);
            const available = availableFor(line.itemId);
            const exceedsAvailable = Number(value) > available;
            return (
              <div key={line.id} className="flex flex-wrap items-end gap-3 border-b border-border pb-3 last:border-0 last:pb-0">
                <div className="min-w-40 flex-1">
                  <p className="text-sm font-medium text-foreground">{itemName(line.itemId)}</p>
                  <p className="text-xs text-muted-foreground">
                    المطلوب: {formatQuantity(remaining, unitName(line.unitId))} · المتاح بالمخزن: {formatQuantity(available, unitName(line.unitId))}
                  </p>
                  {exceedsAvailable || shortItemIds.has(line.itemId) ? (
                    <p className="mt-1 flex items-center gap-1 text-xs font-medium text-badge-amber-fg">
                      <TriangleAlert className="size-3.5" aria-hidden="true" />
                      الكمية المطلوبة تجهيزها أكبر من المتاح حالياً بالمخزن
                    </p>
                  ) : null}
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor={`fulfill-${line.id}`}>الكمية المجهزة</Label>
                  <Input
                    id={`fulfill-${line.id}`}
                    type="number"
                    min="0"
                    max={remaining}
                    step="any"
                    dir="ltr"
                    className="w-28"
                    value={value}
                    onChange={(event) => setFulfillQuantities((current) => ({ ...current, [line.id]: event.target.value }))}
                  />
                </div>
              </div>
            );
          })}

          <Button type="submit" loading={isFulfilling} className="self-start">
            تجهيز وإرسال للشحن
          </Button>
        </form>
      ) : null}

      <Dialog open={lineDialogOpen} onOpenChange={setLineDialogOpen}>
        <DialogContent>
          <form onSubmit={handleAddLine} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>إضافة صنف</DialogTitle>
              <DialogDescription>اختيار صنف موجود بالفعل في الطلب بنفس الوحدة يضيف الكمية للسطر الحالي.</DialogDescription>
            </DialogHeader>

            {lineError ? <ErrorBanner message={lineError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="req-line-item">
                الصنف <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={lineItemId} onValueChange={(value) => value && handleItemChange(value)} required>
                <SelectTrigger id="req-line-item" className="w-full">
                  <SelectValue placeholder="اختر صنفاً" />
                </SelectTrigger>
                <SelectContent>
                  {items.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="req-line-unit">
                الوحدة <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={lineUnitId} onValueChange={(value) => value && setLineUnitId(value)} required>
                <SelectTrigger id="req-line-unit" className="w-full">
                  <SelectValue placeholder="اختر وحدة" />
                </SelectTrigger>
                <SelectContent>
                  {lineUnits.map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="req-line-quantity">
                الكمية <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="req-line-quantity"
                type="number"
                min="0.0001"
                step="any"
                dir="ltr"
                required
                value={lineQuantity}
                onChange={(event) => setLineQuantity(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="req-line-notes">ملاحظات</Label>
              <Input id="req-line-notes" value={lineNotes} onChange={(event) => setLineNotes(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSavingLine} disabled={!lineItemId || !lineUnitId}>
                إضافة
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
