'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { createPortal } from 'react-dom';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus, Printer } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatQuantity, formatDate } from '@/lib/formatters';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConsumptionPrintReport, type ConsumptionReport } from '@/components/features/consumption-print-report';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';

type FilterMode = 'last24h' | 'date' | 'range';

interface AppliedFilter {
  mode: FilterMode;
  from: string;
  to: string;
  restaurantId: string;
}

interface ConsumptionRecord {
  id: string;
  itemName?: string | null;
  unitName?: string | null;
  restaurantName?: string | null;
  recordedByName?: string | null;
  recordedAtLocal?: string | null;
  restaurantId: string;
  itemId: string;
  quantity: number;
  unitId: string;
  consumptionDate: string;
  notes: string | null;
  createdAt: string;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface ItemOption extends NamedOption {
  baseUnitId: string;
}

/** 'yyyy-MM-dd' -> 'dd/MM/yyyy' by string, never through Date (no timezone shift). */
function isoToDisplay(iso: string): string {
  const [year, month, day] = iso.split('-');
  return `${day}/${month}/${year}`;
}

/** REQ-07 (docs/26, docs/04 §12) - a purely statistical consumption log with zero stock
 * effect. Previously the backend (`/api/v1/consumption`) had no frontend screen at all,
 * despite `nav-items.ts` linking Owner to it - the same "real backend, no route" gap
 * closed for /users and /stock-counts in the same reconciliation pass. */
export default function ConsumptionPage() {
  const { profile } = useSession();
  // The filter form (draft) is separate from the APPLIED filter that actually drives the server query, so nothing is
  // fetched while the user is still choosing dates. The default is the rolling last 24 hours, applied server-side.
  const [mode, setMode] = useState<FilterMode>('last24h');
  const [dateValue, setDateValue] = useState('');
  const [fromValue, setFromValue] = useState('');
  const [toValue, setToValue] = useState('');
  const [restaurantFilter, setRestaurantFilter] = useState('');
  const [filterError, setFilterError] = useState<string | null>(null);
  const [applied, setApplied] = useState<AppliedFilter>({ mode: 'last24h', from: '', to: '', restaurantId: '' });

  const query: Record<string, string | undefined> = {
    from: applied.mode === 'last24h' ? undefined : applied.from,
    to: applied.mode === 'last24h' ? undefined : applied.to,
    restaurantId: applied.restaurantId || undefined,
  };

  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<ConsumptionRecord>('/api/v1/consumption', query);

  const [printReport, setPrintReport] = useState<ConsumptionReport | null>(null);
  const [isPreparingPrint, setIsPreparingPrint] = useState(false);
  const [printError, setPrintError] = useState<string | null>(null);

  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [itemOptions, setItemOptions] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);

  useEffect(() => {
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
    apiClient.get<{ items: ItemOption[] }>('/api/v1/items?limit=200').then((page) => setItemOptions(page.items)).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => undefined);
  }, []);

  // Once the report for the applied filter is rendered into the print portal, open the browser print dialog.
  useEffect(() => {
    if (!printReport) return;
    const handle = window.setTimeout(() => window.print(), 50);
    const clear = () => setPrintReport(null);
    window.addEventListener('afterprint', clear);
    return () => {
      window.clearTimeout(handle);
      window.removeEventListener('afterprint', clear);
    };
  }, [printReport]);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [restaurantId, setRestaurantId] = useState('');
  const [itemId, setItemId] = useState('');
  const [unitId, setUnitId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [consumptionDate, setConsumptionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (profile && !profile.permissionCodes.includes('consumption:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('consumption:create') ?? false;

  function restaurantName(id: string): string {
    return restaurants.find((r) => r.id === id)?.nameArabic ?? '—';
  }

  function itemName(id: string): string {
    return itemOptions.find((i) => i.id === id)?.nameArabic ?? '—';
  }

  function unitName(id: string): string {
    return units.find((u) => u.id === id)?.nameArabic ?? '—';
  }

  function openCreate() {
    setRestaurantId('');
    setItemId('');
    setUnitId('');
    setQuantity('');
    setConsumptionDate(new Date().toISOString().slice(0, 10));
    setNotes('');
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      await apiClient.post('/api/v1/consumption', {
        restaurantId,
        itemId,
        unitId,
        quantity: Number(quantity),
        consumptionDate,
        notes: notes || null,
      });
      setDialogOpen(false);
      refetch();
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر تسجيل الاستهلاك');
    } finally {
      setIsSubmitting(false);
    }
  }

  function applyFilter() {
    setFilterError(null);
    if (mode === 'date') {
      if (!dateValue) {
        setFilterError('اختر التاريخ');
        return;
      }
      setApplied({ mode, from: dateValue, to: dateValue, restaurantId: restaurantFilter });
    } else if (mode === 'range') {
      if (!fromValue || !toValue) {
        setFilterError('حدد تاريخ البداية وتاريخ النهاية');
        return;
      }
      if (toValue < fromValue) {
        setFilterError('يجب ألا يسبق تاريخ النهاية تاريخ البداية');
        return;
      }
      setApplied({ mode, from: fromValue, to: toValue, restaurantId: restaurantFilter });
    } else {
      setApplied({ mode, from: '', to: '', restaurantId: restaurantFilter });
    }
  }

  function changeMode(next: FilterMode) {
    setMode(next);
    setFilterError(null);
    if (next === 'last24h') {
      // Switching back to the default applies it immediately - no extra click needed.
      setApplied({ mode: 'last24h', from: '', to: '', restaurantId: restaurantFilter });
    }
  }

  async function handlePrint() {
    setPrintError(null);
    setIsPreparingPrint(true);
    try {
      const params = new URLSearchParams();
      if (applied.mode !== 'last24h') {
        params.set('from', applied.from);
        params.set('to', applied.to);
      }
      if (applied.restaurantId) params.set('restaurantId', applied.restaurantId);
      const suffix = params.toString();
      // The report is fetched from the server for EXACTLY the applied filter - the print never re-filters locally.
      setPrintReport(await apiClient.get<ConsumptionReport>(`/api/v1/consumption/report${suffix ? `?${suffix}` : ''}`));
    } catch (caught) {
      setPrintError(caught instanceof ApiError ? caught.messageAr : 'تعذر تجهيز التقرير للطباعة');
    } finally {
      setIsPreparingPrint(false);
    }
  }

  const activeLabel =
    applied.mode === 'last24h'
      ? 'آخر 24 ساعة'
      : applied.from === applied.to
        ? `التاريخ: ${isoToDisplay(applied.from)}`
        : `من ${isoToDisplay(applied.from)} إلى ${isoToDisplay(applied.to)}`;

  const columns: ColumnDef<ConsumptionRecord, unknown>[] = [
    { id: 'recordedAt', header: 'التاريخ والوقت', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.recordedAtLocal ?? formatDate(row.original.consumptionDate) },
    { id: 'item', header: 'الصنف', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.itemName ?? itemName(row.original.itemId) },
    { id: 'quantity', header: 'الكمية المستهلكة', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => formatQuantity(row.original.quantity, row.original.unitName ?? unitName(row.original.unitId)) },
    { id: 'restaurant', header: 'الفرع', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.restaurantName ?? restaurantName(row.original.restaurantId) },
    { id: 'user', header: 'المستخدم', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.recordedByName ?? '—' },
    { id: 'notes', header: 'ملاحظات', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.notes ?? '—' },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">سجلات الاستهلاك</h1>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => void handlePrint()} loading={isPreparingPrint}>
            <Printer />
            طباعة التقرير
          </Button>
          {canCreate ? (
            <Button onClick={openCreate}>
              <Plus />
              تسجيل استهلاك
            </Button>
          ) : null}
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-card p-3" data-testid="consumption-filters">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="consumption-filter-mode">الفترة</Label>
          <Select value={mode} onValueChange={(value) => value && changeMode(value as FilterMode)}>
            <SelectTrigger id="consumption-filter-mode" className="w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="last24h">آخر 24 ساعة</SelectItem>
              <SelectItem value="date">تاريخ محدد</SelectItem>
              <SelectItem value="range">فترة زمنية</SelectItem>
            </SelectContent>
          </Select>
        </div>
        {mode === 'date' ? (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="consumption-filter-date">التاريخ</Label>
            <Input id="consumption-filter-date" type="date" dir="ltr" value={dateValue} onChange={(event) => setDateValue(event.target.value)} />
          </div>
        ) : null}
        {mode === 'range' ? (
          <>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-filter-from">من تاريخ</Label>
              <Input id="consumption-filter-from" type="date" dir="ltr" value={fromValue} onChange={(event) => setFromValue(event.target.value)} />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-filter-to">إلى تاريخ</Label>
              <Input id="consumption-filter-to" type="date" dir="ltr" value={toValue} onChange={(event) => setToValue(event.target.value)} />
            </div>
          </>
        ) : null}
        {restaurants.length > 1 ? (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="consumption-filter-restaurant">الفرع</Label>
            <Select value={restaurantFilter || 'all'} onValueChange={(value) => setRestaurantFilter(!value || value === 'all' ? '' : value)}>
              <SelectTrigger id="consumption-filter-restaurant" className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">كل الفروع</SelectItem>
                {restaurants.map((restaurant) => (
                  <SelectItem key={restaurant.id} value={restaurant.id}>
                    {restaurant.nameArabic}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        ) : null}
        <Button onClick={applyFilter}>تطبيق</Button>
      </div>
      {filterError ? <ErrorBanner message={filterError} /> : null}
      {printError ? <ErrorBanner message={printError} /> : null}
      <p className="text-sm text-muted-foreground" data-testid="consumption-active-filter">
        الفترة المعروضة: <span className="font-medium text-foreground">{activeLabel}</span>
      </p>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد سجلات استهلاك في هذه الفترة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex flex-col gap-1">
            <p className="font-medium text-foreground">{row.itemName ?? itemName(row.itemId)}</p>
            <p className="text-xs text-muted-foreground">{row.restaurantName ?? restaurantName(row.restaurantId)} · {row.recordedAtLocal ?? formatDate(row.consumptionDate)}</p>
            <p className="text-sm">{formatQuantity(row.quantity, row.unitName ?? unitName(row.unitId))}</p>
          </div>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>تسجيل استهلاك</DialogTitle>
              <DialogDescription>سجل إحصائي بحت، لا يؤثر على رصيد المخزون.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-restaurant">
                الفرع <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={restaurantId} onValueChange={(value) => value && setRestaurantId(value)} required>
                <SelectTrigger id="consumption-restaurant" className="w-full">
                  <SelectValue placeholder="اختر فرعاً" />
                </SelectTrigger>
                <SelectContent>
                  {restaurants.map((restaurant) => (
                    <SelectItem key={restaurant.id} value={restaurant.id}>
                      {restaurant.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-item">
                الصنف <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={itemId} onValueChange={(value) => value && setItemId(value)} required>
                <SelectTrigger id="consumption-item" className="w-full">
                  <SelectValue placeholder="اختر صنفاً" />
                </SelectTrigger>
                <SelectContent>
                  {itemOptions.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-unit">
                الوحدة <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={unitId} onValueChange={(value) => value && setUnitId(value)} required>
                <SelectTrigger id="consumption-unit" className="w-full">
                  <SelectValue placeholder="اختر وحدة" />
                </SelectTrigger>
                <SelectContent>
                  {units.map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-quantity">
                الكمية <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="consumption-quantity"
                type="number"
                min="0.0001"
                step="any"
                dir="ltr"
                required
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-date">
                التاريخ <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="consumption-date"
                type="date"
                dir="ltr"
                required
                value={consumptionDate}
                onChange={(event) => setConsumptionDate(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="consumption-notes">ملاحظات</Label>
              <Input id="consumption-notes" value={notes} onChange={(event) => setNotes(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting} disabled={!restaurantId || !itemId || !unitId}>
                تسجيل
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
      {printReport && typeof document !== 'undefined' ? createPortal(<ConsumptionPrintReport report={printReport} />, document.body) : null}
    </div>
  );
}
