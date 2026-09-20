'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { apiClient, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
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

export interface RestaurantWarehousesDialogProps {
  restaurant: { id: string; nameArabic: string } | null;
  onClose: () => void;
}

/**
 * Owner/Admin (`restaurants:manage`) manage which warehouses a restaurant may request goods
 * from. The backend is the authority: a supply request can only target a warehouse allowed here
 * (plus Active, same company); this screen just edits the real `restaurant_warehouses` mapping.
 */
export function RestaurantWarehousesDialog({ restaurant, onClose }: RestaurantWarehousesDialogProps) {
  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!restaurant) {
      return;
    }
    setError(null);
    Promise.all([
      apiClient.get<NamedOption[]>('/api/v1/warehouses/names'),
      apiClient.get<NamedOption[]>(`/api/v1/restaurants/${restaurant.id}/warehouses`),
    ])
      .then(([all, allowed]) => {
        setWarehouses(all);
        setSelected(new Set(allowed.map((warehouse) => warehouse.id)));
      })
      .catch((caught) => setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل المخازن'));
  }, [restaurant]);

  function toggle(id: string, checked: boolean) {
    setSelected((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    if (!restaurant) {
      return;
    }
    setError(null);
    setIsSaving(true);
    try {
      await apiClient.put(`/api/v1/restaurants/${restaurant.id}/warehouses`, { warehouseIds: [...selected] });
      onClose();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ المخازن المسموح بها');
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Dialog open={restaurant !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form onSubmit={handleSave} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>المخازن المسموح بها - {restaurant?.nameArabic}</DialogTitle>
            <DialogDescription>
              يستطيع مشرف الفرع طلب البضاعة من المخازن المحددة هنا فقط. الفرع بدون مخازن مسموح بها لا يستطيع إنشاء طلبات.
            </DialogDescription>
          </DialogHeader>

          {error ? <ErrorBanner message={error} /> : null}

          <div className="flex max-h-64 flex-col gap-2 overflow-auto">
            {warehouses.map((warehouse) => (
              <label key={warehouse.id} className="flex items-center gap-2 text-sm">
                <Checkbox checked={selected.has(warehouse.id)} onCheckedChange={(checked) => toggle(warehouse.id, checked === true)} />
                {warehouse.nameArabic}
              </label>
            ))}
          </div>

          <DialogFooter>
            <Button type="submit" loading={isSaving}>
              حفظ
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
