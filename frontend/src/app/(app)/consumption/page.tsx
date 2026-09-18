'use client';

import { useEffect, useState, type FormEvent } from 'react';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
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
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';

interface ConsumptionRecord {
  id: string;
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

/** REQ-07 (docs/26, docs/04 §12) - a purely statistical consumption log with zero stock
 * effect. Previously the backend (`/api/v1/consumption`) had no frontend screen at all,
 * despite `nav-items.ts` linking Owner to it - the same "real backend, no route" gap
 * closed for /users and /stock-counts in the same reconciliation pass. */
export default function ConsumptionPage() {
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<ConsumptionRecord>('/api/v1/consumption');

  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [itemOptions, setItemOptions] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);

  useEffect(() => {
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
    apiClient.get<{ items: ItemOption[] }>('/api/v1/items?limit=200').then((page) => setItemOptions(page.items)).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => undefined);
  }, []);

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

  const columns: ColumnDef<ConsumptionRecord, unknown>[] = [
    { id: 'quantity', header: 'الكمية', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => formatQuantity(row.original.quantity, unitName(row.original.unitId)) },
    { id: 'item', header: 'الصنف', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => itemName(row.original.itemId) },
    { id: 'restaurant', header: 'الفرع', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => restaurantName(row.original.restaurantId) },
    { id: 'date', header: 'التاريخ', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => formatDate(row.original.consumptionDate) },
    { id: 'notes', header: 'ملاحظات', cell: ({ row }: CellContext<ConsumptionRecord, unknown>) => row.original.notes ?? '—' },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">سجلات الاستهلاك</h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            تسجيل استهلاك
          </Button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد سجلات استهلاك"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex flex-col gap-1">
            <p className="font-medium text-foreground">{itemName(row.itemId)}</p>
            <p className="text-xs text-muted-foreground">{restaurantName(row.restaurantId)} · {formatDate(row.consumptionDate)}</p>
            <p className="text-sm">{formatQuantity(row.quantity, unitName(row.unitId))}</p>
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
    </div>
  );
}
