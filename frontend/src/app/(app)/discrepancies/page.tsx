'use client';

import { useEffect, useState, type FormEvent } from 'react';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatQuantity, formatDateTime } from '@/lib/formatters';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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

interface Discrepancy {
  id: string;
  documentNumber: string;
  type: 'ReceivingVariance' | 'SupplyReceiptVariance' | 'StockCountVariance' | 'StockUnavailableAtConfirmation';
  warehouseId: string | null;
  restaurantId: string | null;
  itemId: string | null;
  expectedQuantity: number;
  actualQuantity: number;
  variance: number;
  status: 'Open' | 'Investigating' | 'Resolved';
  reason: string | null;
  createdAt: string;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/** docs/09 §9 - the four variance sources surfaced in one screen (guide §8.2 "`/discrepancies`"),
 * scoped server-side per docs/03 (Warehouse Staff: their warehouses, every type; Restaurant
 * Supervisor: their restaurants, receipt variances only; Owner/Admin: everything). */
const TYPE_LABELS: Record<Discrepancy['type'], string> = {
  ReceivingVariance: 'فرق أمر استلام',
  SupplyReceiptVariance: 'فرق استلام من المخزن',
  StockCountVariance: 'فرق جرد فعلي',
  StockUnavailableAtConfirmation: 'نقص مخزون عند التأكيد',
};

export default function DiscrepanciesPage() {
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<Discrepancy>('/api/v1/discrepancies');

  const [itemOptions, setItemOptions] = useState<NamedOption[]>([]);

  useEffect(() => {
    apiClient.get<{ items: NamedOption[] }>('/api/v1/items?limit=200').then((page) => setItemOptions(page.items)).catch(() => undefined);
  }, []);

  const [resolveDialogOpen, setResolveDialogOpen] = useState(false);
  const [resolvingId, setResolvingId] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [resolveError, setResolveError] = useState<string | null>(null);
  const [isResolving, setIsResolving] = useState(false);

  if (profile && !profile.permissionCodes.includes('discrepancies:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canResolve = profile?.permissionCodes.includes('discrepancies:resolve') ?? false;

  function itemName(id: string | null): string {
    if (!id) return '—';
    return itemOptions.find((item) => item.id === id)?.nameArabic ?? '—';
  }

  function openResolve(id: string) {
    setResolvingId(id);
    setReason('');
    setResolveError(null);
    setResolveDialogOpen(true);
  }

  async function handleResolve(event: FormEvent) {
    event.preventDefault();
    setResolveError(null);
    setIsResolving(true);

    try {
      await apiClient.post(`/api/v1/discrepancies/${resolvingId}/resolve`, { reason });
      setResolveDialogOpen(false);
      refetch();
    } catch (caught) {
      setResolveError(caught instanceof ApiError ? caught.messageAr : 'تعذر تسجيل المعالجة');
    } finally {
      setIsResolving(false);
    }
  }

  const columns: ColumnDef<Discrepancy, unknown>[] = [
    {
      id: 'actions',
      header: '',
      cell: ({ row }: CellContext<Discrepancy, unknown>) =>
        canResolve && row.original.status !== 'Resolved' ? (
          <Button variant="outline" size="sm" onClick={() => openResolve(row.original.id)}>
            معالجة
          </Button>
        ) : null,
    },
    { id: 'status', header: 'الحالة', cell: ({ row }: CellContext<Discrepancy, unknown>) => <StatusBadge entity="Discrepancy" status={row.original.status} /> },
    {
      id: 'variance',
      header: 'الفرق',
      cell: ({ row }: CellContext<Discrepancy, unknown>) => (
        <span className={row.original.variance !== 0 ? 'text-destructive' : undefined}>{formatQuantity(row.original.variance, '')}</span>
      ),
    },
    { id: 'item', header: 'الصنف', cell: ({ row }: CellContext<Discrepancy, unknown>) => itemName(row.original.itemId) },
    { id: 'type', header: 'النوع', cell: ({ row }: CellContext<Discrepancy, unknown>) => TYPE_LABELS[row.original.type] },
    {
      id: 'documentNumber',
      header: 'المستند',
      cell: ({ row }: CellContext<Discrepancy, unknown>) => <span dir="ltr">{row.original.documentNumber}</span>,
    },
    {
      id: 'createdAt',
      header: 'التاريخ',
      cell: ({ row }: CellContext<Discrepancy, unknown>) => formatDateTime(row.original.createdAt),
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-heading text-xl font-medium text-foreground">سجل الفروقات</h1>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد فروقات مسجلة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <p className="font-medium text-foreground">{itemName(row.itemId)}</p>
              <StatusBadge entity="Discrepancy" status={row.status} />
            </div>
            <p className="text-xs text-muted-foreground">
              {TYPE_LABELS[row.type]} · {formatDateTime(row.createdAt)}
            </p>
            <p className={row.variance !== 0 ? 'text-sm text-destructive' : 'text-sm'}>الفرق: {formatQuantity(row.variance, '')}</p>
            {canResolve && row.status !== 'Resolved' ? (
              <Button variant="outline" size="sm" onClick={() => openResolve(row.id)} className="self-start">
                معالجة
              </Button>
            ) : null}
          </div>
        )}
      />

      <Dialog open={resolveDialogOpen} onOpenChange={setResolveDialogOpen}>
        <DialogContent>
          <form onSubmit={handleResolve} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>معالجة الفرق</DialogTitle>
              <DialogDescription>أدخل سبب الفرق وإجراء المعالجة المتخذ.</DialogDescription>
            </DialogHeader>

            {resolveError ? <ErrorBanner message={resolveError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="discrepancy-reason">
                السبب <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="discrepancy-reason" required value={reason} onChange={(event) => setReason(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isResolving}>
                تأكيد المعالجة
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
