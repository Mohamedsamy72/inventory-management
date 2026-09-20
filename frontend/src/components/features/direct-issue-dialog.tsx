'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { apiClient, ApiError } from '@/lib/api-client';
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

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface ItemOption extends NamedOption {
  baseUnitId: string;
}

interface IssueLine {
  itemId: string;
  unitId: string;
  quantity: string;
}

interface ConversionSummary {
  fromUnitId: string;
  isActive: boolean;
}

export interface DirectIssueDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  warehouses: NamedOption[];
  restaurants: NamedOption[];
  onIssued: (supplyId: string) => void;
}

/**
 * Change 4 - "أمر صرف": Owner/Admin issue stock straight to a restaurant. One POST to
 * `/api/v1/supplies/direct-issue` (a single atomic backend transaction) - deliberately NOT a
 * browser-side chain of request/fulfil/dispatch/confirm calls. Each line's unit is the
 * item's base unit or a DEFINED conversion unit; the server re-derives everything else (company, base quantity, stock check)
 * and rejects insufficient stock with the standard Arabic error, shown verbatim below. The
 * idempotency key is generated once per dialog session so a double-click cannot deduct twice.
 */
export function DirectIssueDialog({ open, onOpenChange, warehouses, restaurants, onIssued }: DirectIssueDialogProps) {
  const [items, setItems] = useState<ItemOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);
  const [warehouseId, setWarehouseId] = useState('');
  const [restaurantId, setRestaurantId] = useState('');
  const [lines, setLines] = useState<IssueLine[]>([{ itemId: '', unitId: '', quantity: '' }]);
  const [unitOptions, setUnitOptions] = useState<Record<string, NamedOption[]>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());

  useEffect(() => {
    if (!open) {
      return;
    }
    setWarehouseId('');
    setRestaurantId('');
    setLines([{ itemId: '', unitId: '', quantity: '' }]);
    setUnitOptions({});
    setFormError(null);
    setIdempotencyKey(crypto.randomUUID());
    apiClient.get<{ items: ItemOption[] }>('/api/v1/items?limit=200').then((page) => setItems(page.items)).catch(() => setItems([]));
    apiClient.get<NamedOption[]>('/api/v1/units/names').then(setUnits).catch(() => setUnits([]));
  }, [open]);

  // Selectable units = the item's base unit + every ACTIVE defined conversion (same rule as the
  // receiving editor). No free-form factor exists anywhere: the server resolves the conversion
  // from the stored definitions and rejects an undefined one (CONVERSION_NOT_DEFINED).
  async function handleItemChange(index: number, itemId: string) {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item) {
      return;
    }
    setLines((current) => current.map((line, i) => (i === index ? { ...line, itemId, unitId: item.baseUnitId } : line)));
    let fromUnitIds: string[] = [];
    try {
      const conversions = await apiClient.get<ConversionSummary[]>(`/api/v1/items/${itemId}/conversions`);
      fromUnitIds = conversions.filter((conversion) => conversion.isActive).map((conversion) => conversion.fromUnitId);
    } catch {
      fromUnitIds = [];
    }
    const options = [item.baseUnitId, ...fromUnitIds]
      .map((id) => units.find((unit) => unit.id === id))
      .filter((unit): unit is NamedOption => Boolean(unit));
    setUnitOptions((current) => ({ ...current, [itemId]: options }));
  }

  function updateLine(index: number, patch: Partial<IssueLine>) {
    setLines((current) => current.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  const isValid =
    Boolean(warehouseId) &&
    Boolean(restaurantId) &&
    lines.length > 0 &&
    lines.every((line) => line.itemId && line.unitId && Number(line.quantity) > 0);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    event.stopPropagation();
    setFormError(null);
    setIsSubmitting(true);

    try {
      const payload = {
        warehouseId,
        restaurantId,
        lines: lines.map((line) => ({
          itemId: line.itemId,
          unitId: line.unitId,
          quantity: Number(line.quantity),
        })),
      };
      const result = await apiClient.post<{ supply: { id: string } }>('/api/v1/supplies/direct-issue', payload, {
        headers: { 'X-Idempotency-Key': idempotencyKey },
      });
      onOpenChange(false);
      onIssued(result.supply.id);
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر تنفيذ أمر الصرف');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>أمر صرف</DialogTitle>
            <DialogDescription>
              صرف مباشر من المخزن إلى الفرع. يتم خصم الكميات من رصيد المخزن فور التنفيذ ولا يمكن التراجع عن العملية.
            </DialogDescription>
          </DialogHeader>

          {formError ? <ErrorBanner message={formError} /> : null}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="issue-warehouse">
              المخزن <span aria-hidden="true" className="text-destructive">*</span>
            </Label>
            <Select value={warehouseId} onValueChange={(value) => value && setWarehouseId(value)} required>
              <SelectTrigger id="issue-warehouse" className="w-full">
                <SelectValue placeholder="اختر المخزن" />
              </SelectTrigger>
              <SelectContent>
                {warehouses.map((warehouse) => (
                  <SelectItem key={warehouse.id} value={warehouse.id}>
                    {warehouse.nameArabic}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="issue-restaurant">
              الفرع <span aria-hidden="true" className="text-destructive">*</span>
            </Label>
            <Select value={restaurantId} onValueChange={(value) => value && setRestaurantId(value)} required>
              <SelectTrigger id="issue-restaurant" className="w-full">
                <SelectValue placeholder="اختر الفرع" />
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

          <div className="flex flex-col gap-2">
            <Label>الأصناف والكميات</Label>
            {lines.map((line, index) => (
              <div key={index} className="flex items-end gap-2">
                <div className="min-w-0 flex-1">
                  <Select value={line.itemId} onValueChange={(value) => value && void handleItemChange(index, value)}>
                    <SelectTrigger className="w-full" aria-label={`الصنف ${index + 1}`}>
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
                <Select value={line.unitId} onValueChange={(value) => value && updateLine(index, { unitId: value })} disabled={!line.itemId}>
                  <SelectTrigger className="w-32" aria-label={`الوحدة ${index + 1}`}>
                    <SelectValue placeholder="الوحدة" />
                  </SelectTrigger>
                  <SelectContent>
                    {(unitOptions[line.itemId] ?? []).map((unit) => (
                      <SelectItem key={unit.id} value={unit.id}>
                        {unit.nameArabic}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Input
                  type="number"
                  min="0.0001"
                  step="any"
                  dir="ltr"
                  className="w-28"
                  aria-label={`الكمية ${index + 1}`}
                  placeholder="الكمية"
                  value={line.quantity}
                  onChange={(event) => updateLine(index, { quantity: event.target.value })}
                />
                {lines.length > 1 ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    aria-label="حذف السطر"
                    onClick={() => setLines((current) => current.filter((_, i) => i !== index))}
                  >
                    <Trash2 />
                  </Button>
                ) : null}
              </div>
            ))}
            <Button type="button" variant="outline" onClick={() => setLines((current) => [...current, { itemId: '', unitId: '', quantity: '' }])} className="self-start">
              <Plus />
              إضافة صنف
            </Button>
          </div>

          <DialogFooter>
            <Button type="submit" loading={isSubmitting} disabled={!isValid}>
              تنفيذ أمر الصرف
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
