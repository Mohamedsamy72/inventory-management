'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { DataTable } from '@/components/ui/data-table';
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
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { StatusBadge } from '@/components/domain/status-badge';

interface SupplyRequestSummary {
  id: string;
  documentNumber: string;
  restaurantId: string;
  warehouseId: string;
  status: 'Draft' | 'Submitted' | 'PartiallyFulfilled' | 'Fulfilled' | 'Cancelled';
  requestedAt: string;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/** Task F5 (docs/09 §Phase F5, guide §8.2/§8.3 "`/supply-requests`") - shared between
 * Restaurant Supervisor (creates/submits/cancels, restaurant-scoped) and Warehouse Staff
 * (views/fulfils, warehouse-scoped); both see the SAME list, filtered server-side by
 * their own scope, with role-appropriate actions shown per guide §8.6. */
export default function SupplyRequestsPage() {
  const router = useRouter();
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<SupplyRequestSummary>('/api/v1/supply-requests');

  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);

  useEffect(() => {
    // "/names" (docs/03 §"Warehouses & Branches" - ❌ for Restaurant Supervisor), not the
    // manage-gated list: this page's own "طلب جديد" dialog is exactly where a scoped
    // Supervisor (who holds supply_requests:create but no restaurants:manage) needs to
    // resolve their own restaurant's name.
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => setRestaurants([]));
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [restaurantId, setRestaurantId] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (profile && !profile.permissionCodes.includes('supply_requests:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('supply_requests:create') ?? false;
  const scopedRestaurants = restaurants.filter((restaurant) => profile?.restaurantScopeIds.includes(restaurant.id));

  function restaurantName(id: string): string {
    return restaurants.find((restaurant) => restaurant.id === id)?.nameArabic ?? '—';
  }

  useEffect(() => {
    // The single-scope auto-select must react to `restaurants` finishing its async fetch, not
    // just the moment the dialog was opened - opening the dialog before that fetch resolves
    // previously left `restaurantId` stuck at '' forever (no dropdown either, since it only
    // renders for >1 option), permanently disabling "إنشاء".
    if (dialogOpen && !restaurantId && scopedRestaurants.length === 1) {
      setRestaurantId(scopedRestaurants[0]!.id);
    }
  }, [dialogOpen, restaurantId, scopedRestaurants]);

  function openCreate() {
    setRestaurantId('');
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleCreate() {
    setFormError(null);
    setIsSubmitting(true);
    try {
      const created = await apiClient.post<{ id: string }>('/api/v1/supply-requests', { restaurantId });
      setDialogOpen(false);
      router.push(`/supply-requests/${created.id}`);
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر إنشاء الطلب');
    } finally {
      setIsSubmitting(false);
    }
  }

  const columns: ColumnDef<SupplyRequestSummary, unknown>[] = [
    {
      id: 'status',
      header: 'الحالة',
      cell: ({ row }: CellContext<SupplyRequestSummary, unknown>) => <StatusBadge entity="SupplyRequest" status={row.original.status} />,
    },
    {
      id: 'restaurant',
      header: 'الفرع',
      cell: ({ row }: CellContext<SupplyRequestSummary, unknown>) => restaurantName(row.original.restaurantId),
    },
    {
      id: 'documentNumber',
      header: 'رقم الطلب',
      cell: ({ row }: CellContext<SupplyRequestSummary, unknown>) => (
        <button
          type="button"
          onClick={() => router.push(`/supply-requests/${row.original.id}`)}
          className="font-medium text-accent hover:underline"
          dir="ltr"
        >
          {row.original.documentNumber}
        </button>
      ),
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">طلبات البضاعة</h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            طلب جديد
          </Button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد طلبات مطابقة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <button
            type="button"
            onClick={() => router.push(`/supply-requests/${row.id}`)}
            className="flex w-full items-center justify-between gap-2 text-start"
          >
            <div>
              <p className="font-medium text-foreground" dir="ltr">{row.documentNumber}</p>
              <p className="text-xs text-muted-foreground">{restaurantName(row.restaurantId)}</p>
            </div>
            <StatusBadge entity="SupplyRequest" status={row.status} />
          </button>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <div className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>طلب بضاعة جديد</DialogTitle>
              <DialogDescription>
                لا يوجد اختيار للمخزن - سيتم تحديد المخزن المغذي تلقائياً حسب إعدادات الفرع.
              </DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            {scopedRestaurants.length > 1 ? (
              <Select value={restaurantId} onValueChange={(value) => value && setRestaurantId(value)} required>
                <SelectTrigger className="w-full">
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

            <DialogFooter>
              <Button loading={isSubmitting} disabled={!restaurantId} onClick={handleCreate}>
                إنشاء
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
