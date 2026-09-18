'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import type { CellContext, ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatMobileNumber } from '@/lib/formatters';
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
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { EmptyState } from '@/components/feedback/empty-state';
import type { RoleName } from '@/lib/auth/types';

interface UserSummary {
  id: string;
  fullName: string;
  mobileNumber: string;
  role: RoleName | null;
  isActive: boolean;
}

const ROLE_LABELS: Record<RoleName, string> = {
  Owner: 'مالك',
  Admin: 'مسؤول',
  WarehouseStaff: 'موظف مخزن',
  RestaurantSupervisor: 'مشرف فرع',
  User: 'مستخدم عام',
};

/** docs/09 task 4.14 - user provisioning UI. Previously the backend endpoints existed with no
 * frontend screen at all: `nav-items.ts` linked here for Owner/Admin since Phase F2, but the
 * route itself never existed - a real, previously-undetected gap closed here. */
export default function UsersPage() {
  const router = useRouter();
  const { profile } = useSession();

  const [users, setUsers] = useState<UserSummary[] | null>(null);
  const [listError, setListError] = useState<string | null>(null);

  async function load() {
    setListError(null);
    setUsers(null);
    try {
      const result = await apiClient.get<UserSummary[]>('/api/v1/users');
      setUsers(result);
    } catch (caught) {
      setListError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل المستخدمين');
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [fullName, setFullName] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<RoleName>('User');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (profile && !profile.permissionCodes.includes('users:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes('users:manage') ?? false;

  function openCreate() {
    setFullName('');
    setMobileNumber('');
    setPassword('');
    setRole('User');
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      await apiClient.post('/api/v1/users', { fullName, mobileNumber, password, role });
      setDialogOpen(false);
      load();
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر إنشاء المستخدم');
    } finally {
      setIsSubmitting(false);
    }
  }

  const columns: ColumnDef<UserSummary, unknown>[] = [
    { id: 'status', header: 'الحالة', cell: ({ row }: CellContext<UserSummary, unknown>) => <Badge variant={row.original.isActive ? 'default' : 'secondary'}>{row.original.isActive ? 'نشط' : 'معطل'}</Badge> },
    { id: 'role', header: 'الدور', cell: ({ row }: CellContext<UserSummary, unknown>) => (row.original.role ? ROLE_LABELS[row.original.role] : '—') },
    { id: 'mobile', header: 'رقم الجوال', cell: ({ row }: CellContext<UserSummary, unknown>) => <span dir="ltr">{formatMobileNumber(row.original.mobileNumber)}</span> },
    {
      id: 'name',
      header: 'الاسم',
      cell: ({ row }: CellContext<UserSummary, unknown>) => (
        <button type="button" onClick={() => router.push(`/users/${row.original.id}`)} className="font-medium text-accent hover:underline">
          {row.original.fullName}
        </button>
      ),
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">إدارة المستخدمين</h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            مستخدم جديد
          </Button>
        ) : null}
      </div>

      {listError ? <ErrorBanner message={listError} onRetry={load} /> : null}
      {!listError && users === null ? <LoadingSkeleton className="h-64 w-full" /> : null}
      {!listError && users !== null && users.length === 0 ? <EmptyState message="لا يوجد مستخدمون" /> : null}
      {!listError && users !== null && users.length > 0 ? (
        <DataTable
          columns={columns}
          data={users}
          emptyMessage="لا يوجد مستخدمون"
          mobileCard={(row) => (
            <button
              type="button"
              onClick={() => router.push(`/users/${row.id}`)}
              className="flex w-full items-center justify-between gap-2 text-start"
            >
              <div>
                <p className="font-medium text-foreground">{row.fullName}</p>
                <p className="text-xs text-muted-foreground" dir="ltr">{formatMobileNumber(row.mobileNumber)}</p>
                <p className="text-xs text-muted-foreground">{row.role ? ROLE_LABELS[row.role] : '—'}</p>
              </div>
              <Badge variant={row.isActive ? 'default' : 'secondary'}>{row.isActive ? 'نشط' : 'معطل'}</Badge>
            </button>
          )}
        />
      ) : null}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>مستخدم جديد</DialogTitle>
              <DialogDescription>سيستخدم المستخدم رقم الجوال وكلمة المرور لتسجيل الدخول.</DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="user-name">
                الاسم الكامل <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="user-name" required value={fullName} onChange={(event) => setFullName(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="user-mobile">
                رقم الجوال <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="user-mobile" type="tel" dir="ltr" required value={mobileNumber} onChange={(event) => setMobileNumber(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="user-password">
                كلمة المرور المبدئية <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Input id="user-password" type="password" dir="ltr" required value={password} onChange={(event) => setPassword(event.target.value)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="user-role">
                الدور <span aria-hidden="true" className="text-destructive">*</span>
              </Label>
              <Select value={role} onValueChange={(value) => value && setRole(value as RoleName)} required>
                <SelectTrigger id="user-role" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(ROLE_LABELS) as RoleName[]).map((r) => (
                    <SelectItem key={r} value={r}>
                      {ROLE_LABELS[r]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <DialogFooter>
              <Button type="submit" loading={isSubmitting}>
                إنشاء
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
