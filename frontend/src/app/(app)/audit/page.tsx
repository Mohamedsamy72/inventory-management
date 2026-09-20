'use client';

import { useState } from 'react';
import { notFound } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { useSession } from '@/lib/auth/session-context';
import { useKeysetList } from '@/lib/use-keyset-list';
import { formatDateTime } from '@/lib/formatters';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import type { RoleName } from '@/lib/auth/types';

interface AuditRecord {
  id: string;
  actorRole: RoleName;
  action: string;
  entityType: string;
  entityId: string;
  descriptionArabic: string;
  oldValuesJson: string | null;
  newValuesJson: string | null;
  result: string;
  correlationId: string;
  createdAt: string;
}

const ROLE_LABELS: Record<string, string> = {
  Owner: 'مالك',
  Admin: 'مسؤول',
  WarehouseStaff: 'موظف مخزن',
  RestaurantSupervisor: 'مشرف فرع',
  User: 'مستخدم عام',
};

function prettyJson(raw: string | null): string {
  if (!raw) {
    return '—';
  }
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

/**
 * Change 6 - "الحركات" (the audit log). Previously a "coming in a later phase" placeholder even
 * though `GET /api/v1/audit` (Phase 14, `audit:view`, Owner-only per ADR-012) was fully built -
 * the same "real backend, no real screen" gap as /users and /stock-counts. Reads the real
 * endpoint only; the backend never writes secrets into audit rows and is the sole authority on
 * who may read them. Non-Owner roles get the framework 404 (guide §8.6) so the route does not
 * reveal that an audit feature exists.
 */
export default function AuditPage() {
  const { profile, isLoading: isProfileLoading } = useSession();

  const [entityType, setEntityType] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [selected, setSelected] = useState<AuditRecord | null>(null);

  const { items, isLoading, error, hasNextPage, hasPreviousPage, nextPage, previousPage, refetch } = useKeysetList<AuditRecord>(
    '/api/v1/audit',
    {
      entityType: entityType.trim() || undefined,
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
    },
  );

  if (isProfileLoading) {
    return <LoadingSkeleton className="h-40 w-full" />;
  }

  if (profile && profile.role !== 'Owner') {
    notFound();
  }

  const columns: ColumnDef<AuditRecord, unknown>[] = [
    {
      id: 'result',
      header: 'النتيجة',
      cell: ({ row }: CellContext<AuditRecord, unknown>) => (
        <Badge variant={row.original.result === 'Success' ? 'default' : 'secondary'}>
          {row.original.result === 'Success' ? 'نجحت' : 'مرفوضة'}
        </Badge>
      ),
    },
    { id: 'entity', header: 'النوع', cell: ({ row }: CellContext<AuditRecord, unknown>) => <span dir="ltr">{row.original.entityType}</span> },
    { id: 'role', header: 'المنفذ', cell: ({ row }: CellContext<AuditRecord, unknown>) => ROLE_LABELS[row.original.actorRole] ?? row.original.actorRole },
    {
      id: 'description',
      header: 'الوصف',
      cell: ({ row }: CellContext<AuditRecord, unknown>) => (
        <button type="button" onClick={() => setSelected(row.original)} className="text-start font-medium text-accent hover:underline">
          {row.original.descriptionArabic}
        </button>
      ),
    },
    { id: 'createdAt', header: 'الوقت', cell: ({ row }: CellContext<AuditRecord, unknown>) => formatDateTime(row.original.createdAt) },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-heading text-xl font-medium text-foreground">الحركات</h1>

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-entity">نوع السجل</Label>
          <Input id="audit-entity" dir="ltr" placeholder="Supply" value={entityType} onChange={(event) => setEntityType(event.target.value)} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-from">من تاريخ</Label>
          <Input id="audit-from" type="date" dir="ltr" value={from} onChange={(event) => setFrom(event.target.value)} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="audit-to">إلى تاريخ</Label>
          <Input id="audit-to" type="date" dir="ltr" value={to} onChange={(event) => setTo(event.target.value)} />
        </div>
        <Button
          variant="ghost"
          onClick={() => {
            setEntityType('');
            setFrom('');
            setTo('');
          }}
        >
          مسح الفلاتر
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد حركات مسجلة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <button type="button" onClick={() => setSelected(row)} className="flex w-full flex-col gap-1 text-start">
            <p className="font-medium text-foreground">{row.descriptionArabic}</p>
            <p className="text-xs text-muted-foreground">
              {ROLE_LABELS[row.actorRole] ?? row.actorRole} · {formatDateTime(row.createdAt)}
            </p>
          </button>
        )}
      />

      <Dialog open={selected !== null} onOpenChange={(open) => !open && setSelected(null)}>
        <DialogContent>
          {selected ? (
            <div className="flex flex-col gap-3">
              <DialogHeader>
                <DialogTitle>{selected.descriptionArabic}</DialogTitle>
                <DialogDescription>
                  {selected.action} · {formatDateTime(selected.createdAt)}
                </DialogDescription>
              </DialogHeader>
              <div className="flex flex-col gap-1">
                <p className="text-sm font-medium">القيم السابقة</p>
                <pre dir="ltr" className="max-h-40 overflow-auto rounded-md bg-muted p-2 text-xs">{prettyJson(selected.oldValuesJson)}</pre>
              </div>
              <div className="flex flex-col gap-1">
                <p className="text-sm font-medium">القيم الجديدة</p>
                <pre dir="ltr" className="max-h-40 overflow-auto rounded-md bg-muted p-2 text-xs">{prettyJson(selected.newValuesJson)}</pre>
              </div>
              <p className="text-xs text-muted-foreground" dir="ltr">
                correlation: {selected.correlationId}
              </p>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}
