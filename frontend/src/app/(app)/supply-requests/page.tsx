'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient } from '@/lib/api-client';
import { CreateSupplyRequestDialog } from '@/components/features/create-supply-request-dialog';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
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
    // "/names": Restaurant Supervisor holds no restaurants:manage (docs/03).
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => setRestaurants([]));
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);

  if (profile && !profile.permissionCodes.includes('supply_requests:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('supply_requests:create') ?? false;

  function restaurantName(id: string): string {
    return restaurants.find((restaurant) => restaurant.id === id)?.nameArabic ?? '—';
  }

  function openCreate() {
    setDialogOpen(true);
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

      <CreateSupplyRequestDialog open={dialogOpen} onOpenChange={setDialogOpen} />
    </div>
  );
}
