'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
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

export interface CreateSupplyRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * The "طلب بضاعة جديد" creation form, shared by the Restaurant Supervisor dashboard CTA and the
 * requests list so the CTA opens creation DIRECTLY (no intermediate navigation to the list).
 * Creates the Draft (restaurant + warehouse, both validated server-side), then continues to the
 * request editor where items/quantities are added and the request-review confirmation appears
 * before submission. Change 1 (reversed ADR-028): the warehouse is chosen per request; every
 * active company warehouse is offered since Restaurant Supervisor has no warehouse scope.
 */
export function CreateSupplyRequestDialog({ open, onOpenChange }: CreateSupplyRequestDialogProps) {
  const router = useRouter();
  const { profile } = useSession();
  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [restaurantId, setRestaurantId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!open) {
      return;
    }
    setRestaurantId('');
    setWarehouseId('');
    setFormError(null);
    // "/names" endpoints: Restaurant Supervisor holds no restaurants:/warehouses:manage.
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => setRestaurants([]));
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => setWarehouses([]));
  }, [open]);

  const scopedRestaurants = restaurants.filter((restaurant) => profile?.restaurantScopeIds.includes(restaurant.id));

  useEffect(() => {
    // Must react to the async fetch finishing, not only to the dialog opening.
    if (open && !restaurantId && scopedRestaurants.length === 1) {
      setRestaurantId(scopedRestaurants[0]!.id);
    }
  }, [open, restaurantId, scopedRestaurants]);

  async function handleCreate() {
    setFormError(null);
    setIsSubmitting(true);
    try {
      const created = await apiClient.post<{ id: string }>('/api/v1/supply-requests', { restaurantId, warehouseId });
      onOpenChange(false);
      router.push(`/supply-requests/${created.id}`);
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر إنشاء الطلب');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <div className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>طلب بضاعة جديد</DialogTitle>
            <DialogDescription>اختر المخزن الذي تريد طلب البضاعة منه، ثم أضف الأصناف والكميات المطلوبة.</DialogDescription>
          </DialogHeader>

          {formError ? <ErrorBanner message={formError} /> : null}

          {scopedRestaurants.length > 1 ? (
            <Select value={restaurantId} onValueChange={(value) => value && setRestaurantId(value)} required>
              <SelectTrigger className="w-full" aria-label="الفرع">
                <SelectValue placeholder="اختر الفرع" />
              </SelectTrigger>
              <SelectContent>
                {scopedRestaurants.map((restaurant) => (
                  <SelectItem key={restaurant.id} value={restaurant.id}>
                    {restaurant.nameArabic}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          ) : null}

          <Select value={warehouseId} onValueChange={(value) => value && setWarehouseId(value)} required>
            <SelectTrigger className="w-full" aria-label="المخزن">
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

          <DialogFooter>
            <Button loading={isSubmitting} disabled={!restaurantId || !warehouseId} onClick={handleCreate}>
              إنشاء
            </Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
