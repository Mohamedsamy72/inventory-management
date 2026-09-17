'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatDate } from '@/lib/formatters';
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
import { StatusBadge } from '@/components/domain/status-badge';

interface ReceivingOrderSummary {
  id: string;
  documentNumber: string;
  warehouseId: string;
  supplierId: string | null;
  status: 'Draft' | 'Submitted' | 'Verified' | 'Reversed';
  businessDate: string;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

const NONE = '__none__';

/** Task F4 (docs/09 §Phase F4, guide §8.2 "`/receiving`"). */
export default function ReceivingPage() {
  const router = useRouter();
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<ReceivingOrderSummary>('/api/v1/receiving-orders');

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [suppliers, setSuppliers] = useState<NamedOption[]>([]);

  useEffect(() => {
    apiClient
      .get<{ items: NamedOption[] }>('/api/v1/warehouses?limit=100')
      .then((page) => setWarehouses(page.items))
      .catch(() => setWarehouses([]));
    apiClient
      .get<{ items: NamedOption[] }>('/api/v1/suppliers?limit=100')
      .then((page) => setSuppliers(page.items))
      .catch(() => setSuppliers([]));
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [warehouseId, setWarehouseId] = useState('');
  const [supplierId, setSupplierId] = useState(NONE);
  const [businessDate, setBusinessDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (profile && !profile.permissionCodes.includes('receiving:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('receiving:create') ?? false;

  function warehouseName(id: string): string {
    return warehouses.find((warehouse) => warehouse.id === id)?.nameArabic ?? '—';
  }

  function openCreate() {
    setWarehouseId('');
    setSupplierId(NONE);
    setBusinessDate(new Date().toISOString().slice(0, 10));
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      const created = await apiClient.post<{ id: string }>('/api/v1/receiving-orders', {
        warehouseId,
        supplierId: supplierId === NONE ? null : supplierId,
        businessDate,
      });
      setDialogOpen(false);
      router.push(`/receiving/${created.id}`);
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر إنشاء أمر التوريد');
    } finally {
      setIsSubmitting(false);
    }
  }

  const columns: ColumnDef<ReceivingOrderSummary, unknown>[] = [
    {
      id: 'status',
      header: 'الحالة',
      cell: ({ row }: CellContext<ReceivingOrderSummary, unknown>) => <StatusBadge entity="ReceivingOrder" status={row.original.status} />,
    },
    {
      id: 'date',
      header: 'التاريخ',
      cell: ({ row }: CellContext<ReceivingOrderSummary, unknown>) => formatDate(row.original.businessDate),
    },
    {
      id: 'warehouse',
      header: 'المخزن',
      cell: ({ row }: CellContext<ReceivingOrderSummary, unknown>) => warehouseName(row.original.warehouseId),
    },
    {
      id: 'documentNumber',
      header: 'رقم المستند',
      cell: ({ row }: CellContext<ReceivingOrderSummary, unknown>) => <span dir="ltr">{row.original.documentNumber}</span>,
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">أوامر التوريد</h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            أمر توريد جديد
          </Button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد أوامر توريد مطابقة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <button
            type="button"
            onClick={() => router.push(`/receiving/${row.id}`)}
            className="flex w-full items-center justify-between gap-2 text-start"
          >
            <div>
              <p className="font-medium text-foreground" dir="ltr">{row.documentNumber}</p>
              <p className="text-xs text-muted-foreground">{warehouseName(row.warehouseId)} · {formatDate(row.businessDate)}</p>
            </div>
            <StatusBadge entity="ReceivingOrder" status={row.status} />
          </button>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>أمر توريد جديد</DialogTitle>
              <DialogDescription>أدخل بيانات الأمر ثم أضف الأصناف بعد الإنشاء.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="receiving-warehouse">
                المخزن <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={warehouseId} onValueChange={(value) => value && setWarehouseId(value)} required>
                <SelectTrigger id="receiving-warehouse" className="w-full">
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
              <Label htmlFor="receiving-supplier">المورد</Label>
              <Select value={supplierId} onValueChange={(value) => value && setSupplierId(value)}>
                <SelectTrigger id="receiving-supplier" className="w-full">
                  <SelectValue placeholder="بدون" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>بدون</SelectItem>
                  {suppliers.map((supplier) => (
                    <SelectItem key={supplier.id} value={supplier.id}>
                      {supplier.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="receiving-date">
                التاريخ <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input
                id="receiving-date"
                type="date"
                required
                dir="ltr"
                value={businessDate}
                onChange={(event) => setBusinessDate(event.target.value)}
              />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting} disabled={!warehouseId}>
                إنشاء
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
