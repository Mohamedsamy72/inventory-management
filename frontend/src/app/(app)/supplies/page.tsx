'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { Plus } from 'lucide-react';
import { apiClient } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { DirectIssueDialog } from '@/components/features/direct-issue-dialog';
import { DataTable } from '@/components/ui/data-table';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { StatusBadge } from '@/components/domain/status-badge';

interface SupplySummary {
  id: string;
  documentNumber: string;
  warehouseId: string;
  restaurantId: string;
  status: 'Prepared' | 'Dispatched' | 'Confirmed' | 'ConfirmedWithDiscrepancy' | 'RejectedAtDelivery' | 'Cancelled';
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/** Task F5 (guide §8.2 "`/supplies`") - shared between Warehouse Staff (dispatch queue) and
 * Restaurant Supervisor (confirmation queue); both list the same resource, scoped server-side. */
export default function SuppliesPage() {
  const router = useRouter();
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<SupplySummary>('/api/v1/supplies');

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);

  const [issueOpen, setIssueOpen] = useState(false);

  useEffect(() => {
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
  }, []);

  if (profile && !profile.permissionCodes.includes('supplies:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  // Convenience only - the backend independently enforces supplies:direct_issue (Owner/Admin).
  const canDirectIssue = profile?.permissionCodes.includes('supplies:direct_issue') ?? false;

  function warehouseName(id: string): string {
    return warehouses.find((warehouse) => warehouse.id === id)?.nameArabic ?? '—';
  }

  function restaurantName(id: string): string {
    return restaurants.find((restaurant) => restaurant.id === id)?.nameArabic ?? '—';
  }

  const columns: ColumnDef<SupplySummary, unknown>[] = [
    {
      id: 'status',
      header: 'الحالة',
      cell: ({ row }: CellContext<SupplySummary, unknown>) => <StatusBadge entity="Supply" status={row.original.status} />,
    },
    {
      id: 'restaurant',
      header: 'الفرع',
      cell: ({ row }: CellContext<SupplySummary, unknown>) => restaurantName(row.original.restaurantId),
    },
    {
      id: 'warehouse',
      header: 'المخزن',
      cell: ({ row }: CellContext<SupplySummary, unknown>) => warehouseName(row.original.warehouseId),
    },
    {
      id: 'documentNumber',
      header: 'رقم المستند',
      cell: ({ row }: CellContext<SupplySummary, unknown>) => (
        <button type="button" onClick={() => router.push(`/supplies/${row.original.id}`)} className="font-medium text-accent hover:underline" dir="ltr">
          {row.original.documentNumber}
        </button>
      ),
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">التوريدات الصادرة للفروع</h1>
        {canDirectIssue ? (
          <Button onClick={() => setIssueOpen(true)}>
            <Plus />
            أمر صرف
          </Button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد توريدات مطابقة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <button
            type="button"
            onClick={() => router.push(`/supplies/${row.id}`)}
            className="flex w-full items-center justify-between gap-2 text-start"
          >
            <div>
              <p className="font-medium text-foreground" dir="ltr">{row.documentNumber}</p>
              <p className="text-xs text-muted-foreground">{restaurantName(row.restaurantId)} · {warehouseName(row.warehouseId)}</p>
            </div>
            <StatusBadge entity="Supply" status={row.status} />
          </button>
        )}
      />

      <DirectIssueDialog
        open={issueOpen}
        onOpenChange={setIssueOpen}
        warehouses={warehouses}
        restaurants={restaurants}
        onIssued={(id) => router.push(`/supplies/${id}`)}
      />
    </div>
  );
}
