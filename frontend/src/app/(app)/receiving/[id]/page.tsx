'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Plus, Trash2 } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatCurrency, formatDate, formatQuantity } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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

interface ReceivingOrderLine {
  id: string;
  itemId: string;
  expectedQuantity: number;
  actualQuantity: number | null;
  unitId: string;
  unitCost: number | null;
  totalCost: number | null;
  reconciled: boolean;
}

interface ReceivingOrder {
  id: string;
  documentNumber: string;
  warehouseId: string;
  supplierId: string | null;
  status: 'Draft' | 'Submitted' | 'Verified' | 'Reversed';
  businessDate: string;
  reversedBy: string | null;
  reversalReason: string | null;
  lines: ReceivingOrderLine[];
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

/** Task F4 - the receiving draft editor / submit / verify / reverse workflow. */
export default function ReceivingOrderPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();
  const isOwner = profile?.role === 'Owner';

  const [order, setOrder] = useState<ReceivingOrder | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [items, setItems] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<ReceivingOrder>(`/api/v1/receiving-orders/${params.id}`);
      setOrder(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل أمر التوريد');
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

  const [lineDialogOpen, setLineDialogOpen] = useState(false);
  const [lineItemId, setLineItemId] = useState('');
  const [lineUnitId, setLineUnitId] = useState('');
  const [lineUnits, setLineUnits] = useState<NamedOption[]>([]);
  const [lineQuantity, setLineQuantity] = useState('');
  const [lineCost, setLineCost] = useState('');
  const [lineNotes, setLineNotes] = useState('');
  const [lineError, setLineError] = useState<string | null>(null);
  const [isSavingLine, setIsSavingLine] = useState(false);

  const [actualQuantities, setActualQuantities] = useState<Record<string, string>>({});
  const [verifyError, setVerifyError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [verifyKey] = useState(() => crypto.randomUUID());

  const [isSubmittingOrder, setIsSubmittingOrder] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitKey] = useState(() => crypto.randomUUID());

  const [reverseDialogOpen, setReverseDialogOpen] = useState(false);
  const [reverseReason, setReverseReason] = useState('');
  const [reverseError, setReverseError] = useState<string | null>(null);
  const [isReversing, setIsReversing] = useState(false);
  const [reverseKey] = useState(() => crypto.randomUUID());

  if (profile && !profile.permissionCodes.includes('receiving:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('receiving:create') ?? false;
  const canSubmit = profile?.permissionCodes.includes('receiving:submit') ?? false;
  const canVerify = profile?.permissionCodes.includes('receiving:verify') ?? false;
  const canReverse = profile?.permissionCodes.includes('receiving:reverse') ?? false;

  function itemName(id: string): string {
    return items.find((item) => item.id === id)?.nameArabic ?? '—';
  }

  function unitName(id: string): string {
    return units.find((unit) => unit.id === id)?.nameArabic ?? '—';
  }

  function warehouseName(id: string): string {
    return warehouses.find((warehouse) => warehouse.id === id)?.nameArabic ?? '—';
  }

  function openAddLine() {
    setLineItemId('');
    setLineUnitId('');
    setLineUnits([]);
    setLineQuantity('');
    setLineCost('');
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
      await apiClient.post(`/api/v1/receiving-orders/${order!.id}/lines`, {
        itemId: lineItemId,
        unitId: lineUnitId,
        expectedQuantity: Number(lineQuantity),
        unitCost: Number(lineCost),
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
    await apiClient.delete(`/api/v1/receiving-orders/${order!.id}/lines/${lineId}`);
    await load();
  }

  async function handleSubmitOrder() {
    setSubmitError(null);
    setIsSubmittingOrder(true);
    try {
      await apiClient.post(`/api/v1/receiving-orders/${order!.id}/submit`, undefined, {
        headers: { 'X-Idempotency-Key': submitKey },
      });
      await load();
    } catch (caught) {
      setSubmitError(caught instanceof ApiError ? caught.messageAr : 'تعذر ترحيل الأمر');
    } finally {
      setIsSubmittingOrder(false);
    }
  }

  async function handleVerify(event: FormEvent) {
    event.preventDefault();
    setVerifyError(null);
    setIsVerifying(true);

    try {
      const lines = order!.lines.map((line) => ({
        lineId: line.id,
        actualQuantity: Number(actualQuantities[line.id] ?? line.expectedQuantity),
      }));
      await apiClient.post(
        `/api/v1/receiving-orders/${order!.id}/verify`,
        { lines },
        { headers: { 'X-Idempotency-Key': verifyKey } },
      );
      await load();
    } catch (caught) {
      setVerifyError(caught instanceof ApiError ? caught.messageAr : 'تعذر تسجيل المطابقة');
    } finally {
      setIsVerifying(false);
    }
  }

  async function handleReverse(event: FormEvent) {
    event.preventDefault();
    setReverseError(null);
    setIsReversing(true);

    try {
      await apiClient.post(
        `/api/v1/receiving-orders/${order!.id}/reverse`,
        { reason: reverseReason },
        { headers: { 'X-Idempotency-Key': reverseKey } },
      );
      setReverseDialogOpen(false);
      await load();
    } catch (caught) {
      setReverseError(caught instanceof ApiError ? caught.messageAr : 'تعذر عكس الأمر');
    } finally {
      setIsReversing(false);
    }
  }

  if (isLoading) {
    return <LoadingSkeleton className="h-64 w-full" />;
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (!order) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground" dir="ltr">
            {order.documentNumber}
          </h1>
          <p className="text-sm text-muted-foreground">
            {warehouseName(order.warehouseId)} · {formatDate(order.businessDate)}
          </p>
        </div>
        <StatusBadge entity="ReceivingOrder" status={order.status} />
      </div>

      {order.status === 'Reversed' && order.reversalReason ? (
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          سبب العكس: {order.reversalReason}
        </div>
      ) : null}

      <div className="overflow-auto rounded-lg border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              {order.status === 'Draft' && canCreate ? <TableHead>إجراء</TableHead> : null}
              {isOwner ? <TableHead>الإجمالي</TableHead> : null}
              {isOwner ? <TableHead>تكلفة الوحدة</TableHead> : null}
              {order.status !== 'Draft' ? <TableHead>الفرق</TableHead> : null}
              {order.status !== 'Draft' ? <TableHead>الفعلي</TableHead> : null}
              <TableHead>المتوقع</TableHead>
              <TableHead>الوحدة</TableHead>
              <TableHead>الصنف</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {order.lines.map((line) => {
              const variance = line.actualQuantity !== null ? line.actualQuantity - line.expectedQuantity : null;
              return (
                <TableRow key={line.id}>
                  {order.status === 'Draft' && canCreate ? (
                    <TableCell>
                      <Button variant="ghost" size="icon-sm" onClick={() => handleRemoveLine(line.id)} aria-label="حذف">
                        <Trash2 />
                      </Button>
                    </TableCell>
                  ) : null}
                  {isOwner ? <TableCell>{line.totalCost !== null ? formatCurrency(line.totalCost) : '—'}</TableCell> : null}
                  {isOwner ? <TableCell>{line.unitCost !== null ? formatCurrency(line.unitCost) : '—'}</TableCell> : null}
                  {order.status !== 'Draft' ? (
                    <TableCell className={variance && variance !== 0 ? 'text-destructive' : undefined}>
                      {variance !== null ? formatQuantity(variance, '') : '—'}
                    </TableCell>
                  ) : null}
                  {order.status !== 'Draft' ? (
                    <TableCell>
                      {order.status === 'Submitted' && canVerify ? (
                        <Input
                          type="number"
                          min="0"
                          step="any"
                          dir="ltr"
                          className="w-24"
                          aria-label={`الكمية الفعلية - ${itemName(line.itemId)}`}
                          value={actualQuantities[line.id] ?? String(line.expectedQuantity)}
                          onChange={(event) => setActualQuantities((current) => ({ ...current, [line.id]: event.target.value }))}
                        />
                      ) : (
                        (line.actualQuantity ?? '—')
                      )}
                    </TableCell>
                  ) : null}
                  <TableCell>{formatQuantity(line.expectedQuantity, unitName(line.unitId))}</TableCell>
                  <TableCell>{unitName(line.unitId)}</TableCell>
                  <TableCell>{itemName(line.itemId)}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      {order.status === 'Draft' && canCreate ? (
        <Button variant="outline" onClick={openAddLine} className="self-start">
          <Plus />
          إضافة صنف
        </Button>
      ) : null}

      {submitError ? <ErrorBanner message={submitError} /> : null}
      {verifyError ? <ErrorBanner message={verifyError} /> : null}

      <div className="flex items-center gap-2">
        {order.status === 'Draft' && canSubmit && order.lines.length > 0 ? (
          <Button loading={isSubmittingOrder} onClick={handleSubmitOrder}>
            ترحيل الأمر إلى المخزن
          </Button>
        ) : null}
        {order.status === 'Submitted' && canVerify ? (
          <Button loading={isVerifying} onClick={handleVerify}>
            تسجيل المطابقة
          </Button>
        ) : null}
        {(order.status === 'Submitted' || order.status === 'Verified') && canReverse ? (
          <Button variant="destructive" onClick={() => setReverseDialogOpen(true)}>
            عكس الأمر
          </Button>
        ) : null}
        <Button variant="ghost" onClick={() => router.push('/receiving')}>
          رجوع لقائمة الأوامر
        </Button>
      </div>

      <Dialog open={lineDialogOpen} onOpenChange={setLineDialogOpen}>
        <DialogContent>
          <form onSubmit={handleAddLine} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>إضافة صنف</DialogTitle>
              <DialogDescription>حدد الصنف والوحدة والكمية المتوقعة.</DialogDescription>
            </DialogHeader>

            {lineError ? <ErrorBanner message={lineError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="line-item">
                الصنف <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={lineItemId} onValueChange={(value) => value && handleItemChange(value)} required>
                <SelectTrigger id="line-item" className="w-full">
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
              <Label htmlFor="line-unit">
                الوحدة <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={lineUnitId} onValueChange={(value) => value && setLineUnitId(value)} required>
                <SelectTrigger id="line-unit" className="w-full">
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
              <Label htmlFor="line-quantity">
                الكمية المتوقعة <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="line-quantity"
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
              <Label htmlFor="line-cost">
                تكلفة الوحدة <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="line-cost"
                type="number"
                min="0"
                step="any"
                dir="ltr"
                required
                value={lineCost}
                onChange={(event) => setLineCost(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="line-notes">ملاحظات</Label>
              <Input id="line-notes" value={lineNotes} onChange={(event) => setLineNotes(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSavingLine} disabled={!lineItemId || !lineUnitId}>
                إضافة
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={reverseDialogOpen} onOpenChange={setReverseDialogOpen}>
        <DialogContent>
          <form onSubmit={handleReverse} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>عكس أمر التوريد</DialogTitle>
              <DialogDescription>سيتم عكس كل حركات المخزون المسجلة لهذا الأمر. هذا الإجراء لا يمكن التراجع عنه.</DialogDescription>
            </DialogHeader>

            {reverseError ? <ErrorBanner message={reverseError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="reverse-reason">
                السبب <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="reverse-reason" required value={reverseReason} onChange={(event) => setReverseReason(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" variant="destructive" loading={isReversing}>
                تأكيد العكس
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
