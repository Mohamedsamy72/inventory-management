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

export interface SimpleEntityField {
  key: string;
  label: string;
  required?: boolean;
  /** Immutable after creation (e.g. a warehouse/restaurant code) - shown read-only on edit. */
  createOnly?: boolean;
}

interface BaseSummary {
  id: string;
  nameArabic: string;
  isActive: boolean;
}

export interface SimpleMasterDataScreenProps {
  title: string;
  basePath: string;
  permissionCode: string;
  /** All fields including `nameArabic` itself, in display/form order. */
  fields: SimpleEntityField[];
  emptyMessage: string;
}

/**
 * Task F3. Shared list+dialog CRUD screen for the master-data entities whose shape is
 * genuinely identical: `nameArabic` + a handful of optional text fields + an
 * active/inactive toggle via deactivate/reactivate (never a hard delete - docs/09 §5).
 * Restaurants (needs a warehouse selector) and Items (category/unit/supplier selects,
 * generated code, quick-add, conversions) have real structural differences and are their
 * own screens, not instances of this one.
 */
export function SimpleMasterDataScreen<TSummary extends BaseSummary>({
  title,
  basePath,
  permissionCode,
  fields,
  emptyMessage,
}: SimpleMasterDataScreenProps) {
  const { profile } = useSession();
  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } =
    useKeysetList<TSummary>(basePath);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<TSummary | null>(null);
  const [formValues, setFormValues] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);

  if (profile && !profile.permissionCodes.includes(permissionCode)) {
    return <ForbiddenState reason="forbidden" />;
  }

  function openCreate() {
    setEditing(null);
    setFormValues(Object.fromEntries(fields.map((field) => [field.key, ''])));
    setFormError(null);
    setDialogOpen(true);
  }

  function openEdit(row: TSummary) {
    setEditing(row);
    setFormValues(Object.fromEntries(fields.map((field) => [field.key, String((row as Record<string, unknown>)[field.key] ?? '')])));
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    const body = Object.fromEntries(
      fields
        .filter((field) => !(editing && field.createOnly))
        .map((field) => [field.key, formValues[field.key]?.trim() === '' ? null : formValues[field.key]]),
    );

    try {
      if (editing) {
        await apiClient.put(`${basePath}/${editing.id}`, body);
      } else {
        await apiClient.post(basePath, body);
      }
      setDialogOpen(false);
      refetch();
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ البيانات');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function toggleActive(row: TSummary) {
    setTogglingId(row.id);
    try {
      await apiClient.post(`${basePath}/${row.id}/${row.isActive ? 'deactivate' : 'reactivate'}`);
      refetch();
    } finally {
      setTogglingId(null);
    }
  }

  const columns: ColumnDef<TSummary, unknown>[] = [
    ...fields
      .filter((field) => field.key !== 'nameArabic')
      .slice(0, 2)
      .map<ColumnDef<TSummary, unknown>>((field) => ({
        id: field.key,
        header: field.label,
        cell: ({ row }: CellContext<TSummary, unknown>) => (row.original as Record<string, unknown>)[field.key]?.toString() || '—',
      })),
    {
      id: 'nameArabic',
      header: 'الاسم',
      cell: ({ row }: CellContext<TSummary, unknown>) => row.original.nameArabic,
    },
    {
      id: 'status',
      header: 'الحالة',
      cell: ({ row }: CellContext<TSummary, unknown>) => (
        <Badge variant={row.original.isActive ? 'default' : 'secondary'}>{row.original.isActive ? 'نشط' : 'غير نشط'}</Badge>
      ),
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }: CellContext<TSummary, unknown>) => (
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
  ].reverse();
  // .reverse() so nameArabic (the primary identifying column) reads first under dir="rtl"
  // (docs/31 §4.3 point 4: the first logical column sits at the right edge).

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="font-heading text-xl font-medium text-foreground">{title}</h1>
        <Button onClick={openCreate}>
          <Plus />
          إضافة جديد
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage={emptyMessage}
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex items-center justify-between gap-2">
            <div>
              <p className="font-medium text-foreground">{row.nameArabic}</p>
              <Badge variant={row.isActive ? 'default' : 'secondary'} className="mt-1">
                {row.isActive ? 'نشط' : 'غير نشط'}
              </Badge>
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
              <DialogTitle>{editing ? `تعديل ${title}` : `إضافة ${title}`}</DialogTitle>
              <DialogDescription>أدخل البيانات ثم اضغط حفظ.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            {fields.map((field) => (
              <div key={field.key} className="flex flex-col gap-1.5">
                <Label htmlFor={field.key}>
                  {field.label}
                  {field.required ? (
                    <span aria-hidden="true" className="text-destructive">
                      {' '}
                      *
                    </span>
                  ) : null}
                </Label>
                <Input
                  id={field.key}
                  required={field.required}
                  disabled={Boolean(editing) && field.createOnly}
                  value={formValues[field.key] ?? ''}
                  onChange={(event) => setFormValues((values) => ({ ...values, [field.key]: event.target.value }))}
                />
              </div>
            ))}

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
