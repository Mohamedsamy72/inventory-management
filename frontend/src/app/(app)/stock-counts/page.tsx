'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatDateTime } from '@/lib/formatters';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
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

interface StockCountSummary {
  id: string;
  documentNumber: string;
  warehouseId: string;
  status: 'Draft' | 'InProgress' | 'PendingApproval' | 'Approved' | 'Rejected';
  isBlindCount: boolean;
  openedAt: string;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/** docs/09 Phase 13 - physical stock counts. Previously the backend (`/api/v1/stock-counts`)
 * had no frontend screen at all, despite `nav-items.ts` linking Owner/Admin to it - the same
 * "real backend, no route" gap closed for /users and /consumption in the same pass. */
export default function StockCountsPage() {
  const router = useRouter();
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<StockCountSummary>('/api/v1/stock-counts');

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);

  useEffect(() => {
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [warehouseId, setWarehouseId] = useState('');
  const [isBlindCount, setIsBlindCount] = useState(true);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (profile && !profile.permissionCodes.includes('stock_counts:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('stock_counts:create') ?? false;

  function warehouseName(id: string): string {
    return warehouses.find((w) => w.id === id)?.nameArabic ?? '—';
  }

  function openCreate() {
    setWarehouseId('');
    setIsBlindCount(true);
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      const created = await apiClient.post<StockCountSummary>('/api/v1/stock-counts', { warehouseId, isBlindCount });
      setDialogOpen(false);
      router.push(`/stock-counts/${created.id}`);
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر بدء الجرد');
    } finally {
      setIsSubmitting(false);
    }
  }

  const columns: ColumnDef<StockCountSummary, unknown>[] = [
    { id: 'status', header: 'الحالة', cell: ({ row }: CellContext<StockCountSummary, unknown>) => <StatusBadge entity="StockCount" status={row.original.status} /> },
    { id: 'blind', header: 'النوع', cell: ({ row }: CellContext<StockCountSummary, unknown>) => (row.original.isBlindCount ? 'جرد بدون كميات معروضة' : 'جرد عادي') },
    { id: 'warehouse', header: 'المخزن', cell: ({ row }: CellContext<StockCountSummary, unknown>) => warehouseName(row.original.warehouseId) },
    {
      id: 'documentNumber',
      header: 'المستند',
      cell: ({ row }: CellContext<StockCountSummary, unknown>) => (
        <button type="button" onClick={() => router.push(`/stock-counts/${row.original.id}`)} className="font-medium text-accent hover:underline" dir="ltr">
          {row.original.documentNumber}
        </button>
      ),
    },
    { id: 'openedAt', header: 'تاريخ البدء', cell: ({ row }: CellContext<StockCountSummary, unknown>) => formatDateTime(row.original.openedAt) },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">الجرد الفعلي</h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            بدء جرد جديد
          </Button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد عمليات جرد"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <button
            type="button"
            onClick={() => router.push(`/stock-counts/${row.id}`)}
            className="flex w-full items-center justify-between gap-2 text-start"
          >
            <div>
              <p className="font-medium text-foreground" dir="ltr">{row.documentNumber}</p>
              <p className="text-xs text-muted-foreground">{warehouseName(row.warehouseId)} · {formatDateTime(row.openedAt)}</p>
            </div>
            <StatusBadge entity="StockCount" status={row.status} />
          </button>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>بدء جرد جديد</DialogTitle>
              <DialogDescription>سيتم أخذ لقطة من أرصدة كل الأصناف في المخزن المحدد.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="count-warehouse">
                المخزن <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={warehouseId} onValueChange={(value) => value && setWarehouseId(value)} required>
                <SelectTrigger id="count-warehouse" className="w-full">
                  <SelectValue placeholder="اختر مخزناً" />
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

            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={isBlindCount} onCheckedChange={(checked) => setIsBlindCount(checked === true)} />
              جرد بدون عرض الكميات الدفترية أثناء العد
            </label>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting} disabled={!warehouseId}>
                بدء الجرد
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
