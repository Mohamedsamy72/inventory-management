'use client';

import { useState, type FormEvent } from 'react';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus, Pencil, Power, PowerOff } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { apiClient, ApiError } from '@/lib/api-client';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
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
  address: string | null;
  description: string | null;
  isActive: boolean;
}

/**
 * Task F3 (guide §8.2 "`/locations`").
 *
 * Change 1 (product decision reversing ADR-028): a restaurant is pure master data - name,
 * code, address, description - with NO warehouse field at all. It is no longer pinned to one
 * "serving warehouse"; a restaurant can receive from more than one warehouse, chosen per supply
 * request instead (see `/supply-requests`' create dialog).
 */
export function RestaurantsScreen() {
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<RestaurantSummary>('/api/v1/restaurants');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RestaurantSummary | null>(null);
  const [nameArabic, setNameArabic] = useState('');
  const [code, setCode] = useState('');
  const [address, setAddress] = useState('');
  const [description, setDescription] = useState('');
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
    setFormError(null);
    setDialogOpen(true);
  }

  function openEdit(row: RestaurantSummary) {
    setEditing(row);
    setNameArabic(row.nameArabic);
    setCode(row.code);
    setAddress(row.address ?? '');
    setDescription(row.description ?? '');
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
      } else {
        await apiClient.post('/api/v1/restaurants', {
          nameArabic,
          code,
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
              <Label htmlFor="restaurant-address">العنوان</Label>
              <Input id="restaurant-address" value={address} onChange={(event) => setAddress(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="restaurant-description">الوصف</Label>
              <Input id="restaurant-description" value={description} onChange={(event) => setDescription(event.target.value)} />
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting}>
                حفظ
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
