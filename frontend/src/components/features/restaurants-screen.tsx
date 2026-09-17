'use client';

import { useEffect, useState, type FormEvent } from 'react';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus, Pencil, Power, PowerOff, Warehouse as WarehouseIcon } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
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

interface RestaurantSummary {
  id: string;
  nameArabic: string;
  code: string;
  defaultServingWarehouseId: string;
  address: string | null;
  description: string | null;
  isActive: boolean;
}

interface WarehouseSummary {
  id: string;
  nameArabic: string;
  code: string;
  isActive: boolean;
}

/**
 * Task F3 (guide §8.2 "`/locations`"). Restaurants differ from the other simple
 * master-data entities in a way that genuinely justifies a dedicated screen: creation
 * requires picking a serving warehouse (ADR-028's `defaultServingWarehouseId`), and an
 * existing restaurant's serving warehouse can be changed independently of its other
 * fields via its own dedicated endpoint/action.
 */
export function RestaurantsScreen() {
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<RestaurantSummary>('/api/v1/restaurants');

  const [warehouses, setWarehouses] = useState<WarehouseSummary[]>([]);

  useEffect(() => {
    apiClient
      .get<{ items: WarehouseSummary[] }>('/api/v1/warehouses?limit=100')
      .then((page) => setWarehouses(page.items.filter((warehouse) => warehouse.isActive)))
      .catch(() => setWarehouses([]));
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RestaurantSummary | null>(null);
  const [nameArabic, setNameArabic] = useState('');
  const [code, setCode] = useState('');
  const [address, setAddress] = useState('');
  const [description, setDescription] = useState('');
  const [servingWarehouseId, setServingWarehouseId] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);

  if (profile && !profile.permissionCodes.includes('restaurants:manage')) {
    return <ForbiddenState reason="forbidden" />;
  }

  function openCreate() {
    setEditing(null);
    setNameArabic('');
    setCode('');
    setAddress('');
    setDescription('');
    setServingWarehouseId('');
    setFormError(null);
    setDialogOpen(true);
  }

  function openEdit(row: RestaurantSummary) {
    setEditing(row);
    setNameArabic(row.nameArabic);
    setCode(row.code);
    setAddress(row.address ?? '');
    setDescription(row.description ?? '');
    setServingWarehouseId(row.defaultServingWarehouseId);
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      if (editing) {
        await apiClient.put(`/api/v1/restaurants/${editing.id}`, {
          nameArabic,
          address: address || null,
          description: description || null,
        });
        if (servingWarehouseId !== editing.defaultServingWarehouseId) {
          await apiClient.put(`/api/v1/restaurants/${editing.id}/serving-warehouse`, { warehouseId: servingWarehouseId });
        }
      } else {
        await apiClient.post('/api/v1/restaurants', {
          nameArabic,
          code,
          defaultServingWarehouseId: servingWarehouseId,
          address: address || null,
          description: description || null,
        });
      }
      setDialogOpen(false);
      refetch();
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ البيانات');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function toggleActive(row: RestaurantSummary) {
    setTogglingId(row.id);
    try {
      await apiClient.post(`/api/v1/restaurants/${row.id}/${row.isActive ? 'deactivate' : 'reactivate'}`);
      refetch();
    } finally {
      setTogglingId(null);
    }
  }

  function warehouseName(id: string): string {
    return warehouses.find((warehouse) => warehouse.id === id)?.nameArabic ?? '—';
  }

  const columns: ColumnDef<RestaurantSummary, unknown>[] = [
    {
      id: 'actions',
      header: '',
      cell: ({ row }: CellContext<RestaurantSummary, unknown>) => (
        <div className="flex items-center gap-1">
          <Button variant="ghost" size="icon-sm" onClick={() => openEdit(row.original)} aria-label="تعديل">
            <Pencil />
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            loading={togglingId === row.original.id}
            onClick={() => toggleActive(row.original)}
            aria-label={row.original.isActive ? 'إلغاء التفعيل' : 'إعادة التفعيل'}
          >
            {row.original.isActive ? <PowerOff /> : <Power />}
          </Button>
        </div>
      ),
    },
    { id: 'status', header: 'الحالة', cell: ({ row }: CellContext<RestaurantSummary, unknown>) => <Badge variant={row.original.isActive ? 'default' : 'secondary'}>{row.original.isActive ? 'نشط' : 'غير نشط'}</Badge> },
    { id: 'warehouse', header: 'المخزن المغذي', cell: ({ row }: CellContext<RestaurantSummary, unknown>) => warehouseName(row.original.defaultServingWarehouseId) },
    { id: 'code', header: 'الكود', cell: ({ row }: CellContext<RestaurantSummary, unknown>) => row.original.code },
    { id: 'nameArabic', header: 'الاسم', cell: ({ row }: CellContext<RestaurantSummary, unknown>) => row.original.nameArabic },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="font-heading text-lg font-medium text-foreground">الفروع (المطاعم)</h2>
        <Button onClick={openCreate}>
          <Plus />
          إضافة فرع جديد
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد فروع مطابقة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex items-center justify-between gap-2">
            <div>
              <p className="font-medium text-foreground">{row.nameArabic}</p>
              <p className="text-xs text-muted-foreground">{row.code}</p>
              <div className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
                <WarehouseIcon className="size-3.5" aria-hidden="true" />
                {warehouseName(row.defaultServingWarehouseId)}
              </div>
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon-sm" onClick={() => openEdit(row)} aria-label="تعديل">
                <Pencil />
              </Button>
              <Button
                variant="ghost"
                size="icon-sm"
                loading={togglingId === row.id}
                onClick={() => toggleActive(row)}
                aria-label={row.isActive ? 'إلغاء التفعيل' : 'إعادة التفعيل'}
              >
                {row.isActive ? <PowerOff /> : <Power />}
              </Button>
            </div>
          </div>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>{editing ? 'تعديل الفرع' : 'إضافة فرع جديد'}</DialogTitle>
              <DialogDescription>أدخل البيانات ثم اضغط حفظ.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="restaurant-name">
                الاسم <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="restaurant-name" required value={nameArabic} onChange={(event) => setNameArabic(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="restaurant-code">
                الكود {editing ? null : <span aria-hidden="true" className="text-destructive">*</span>}
              </Label>
              <Input
                id="restaurant-code"
                required={!editing}
                disabled={Boolean(editing)}
                dir="ltr"
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="restaurant-warehouse">
                المخزن المغذي <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={servingWarehouseId} onValueChange={setServingWarehouseId} required>
                <SelectTrigger id="restaurant-warehouse" className="w-full">
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
              <Label htmlFor="restaurant-address">العنوان</Label>
              <Input id="restaurant-address" value={address} onChange={(event) => setAddress(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="restaurant-description">الوصف</Label>
              <Input id="restaurant-description" value={description} onChange={(event) => setDescription(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting} disabled={!servingWarehouseId}>
                حفظ
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
