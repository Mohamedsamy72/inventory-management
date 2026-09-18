'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatMobileNumber } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import type { RoleName } from '@/lib/auth/types';

interface UserSummary {
  id: string;
  fullName: string;
  mobileNumber: string;
  role: RoleName | null;
  isActive: boolean;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface UserScope {
  warehouseIds: string[];
  restaurantIds: string[];
}

interface PermissionCatalogueItem {
  code: string;
  description: string;
  module: string;
  isGrantable: boolean;
}

const ROLE_LABELS: Record<RoleName, string> = {
  Owner: 'مالك',
  Admin: 'مسؤول',
  WarehouseStaff: 'موظف مخزن',
  RestaurantSupervisor: 'مشرف فرع',
  User: 'مستخدم عام',
};

const SCOPED_ROLES: RoleName[] = ['WarehouseStaff', 'RestaurantSupervisor'];

/** docs/09 task 4.14/4.8 - role, scope, and permission management for one user. Every mutation
 * here relies entirely on the server's own privilege-escalation guards (no self-modification,
 * only an Owner may assign Owner, a non-Owner cannot grant beyond their own effective set) -
 * this screen never tries to replicate that logic client-side, only surfaces whatever error the
 * server returns. */
export default function UserDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();

  const [user, setUser] = useState<UserSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouses, setWarehouses] = useState<NamedOption[]>([]);
  const [restaurants, setRestaurants] = useState<NamedOption[]>([]);
  const [permissionCatalogue, setPermissionCatalogue] = useState<PermissionCatalogueItem[]>([]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<UserSummary>(`/api/v1/users/${params.id}`);
      setUser(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل بيانات المستخدم');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
    apiClient.get<NamedOption[]>('/api/v1/warehouses/names').then(setWarehouses).catch(() => undefined);
    apiClient.get<NamedOption[]>('/api/v1/restaurants/names').then(setRestaurants).catch(() => undefined);
    apiClient.get<PermissionCatalogueItem[]>('/api/v1/permissions').then(setPermissionCatalogue).catch(() => undefined);
  }, [params.id]);

  const [role, setRole] = useState<RoleName>('User');
  const [isSavingRole, setIsSavingRole] = useState(false);
  const [roleError, setRoleError] = useState<string | null>(null);

  const [scope, setScope] = useState<UserScope>({ warehouseIds: [], restaurantIds: [] });
  const [isLoadingScope, setIsLoadingScope] = useState(false);
  const [isSavingScope, setIsSavingScope] = useState(false);
  const [scopeError, setScopeError] = useState<string | null>(null);

  const [grantedCodes, setGrantedCodes] = useState<Set<string>>(new Set());
  const [isSavingPermissions, setIsSavingPermissions] = useState(false);
  const [permissionsError, setPermissionsError] = useState<string | null>(null);

  const [isTogglingActive, setIsTogglingActive] = useState(false);
  const [activeError, setActiveError] = useState<string | null>(null);

  useEffect(() => {
    if (user?.role) {
      setRole(user.role);
    }
  }, [user?.role]);

  useEffect(() => {
    if (!user || !SCOPED_ROLES.includes(user.role as RoleName)) {
      return;
    }
    setIsLoadingScope(true);
    apiClient
      .get<UserScope>(`/api/v1/users/${params.id}/scope`)
      .then(setScope)
      .catch(() => undefined)
      .finally(() => setIsLoadingScope(false));
  }, [user, params.id]);

  if (profile && !profile.permissionCodes.includes('users:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canManage = profile?.permissionCodes.includes('users:manage') ?? false;
  const canScope = profile?.permissionCodes.includes('users:scope') ?? false;
  const isSelf = profile?.userId === params.id;

  async function handleRoleChange() {
    setRoleError(null);
    setIsSavingRole(true);
    try {
      await apiClient.put(`/api/v1/users/${params.id}/role`, { role });
      await load();
    } catch (caught) {
      setRoleError(caught instanceof ApiError ? caught.messageAr : 'تعذر تغيير الدور');
    } finally {
      setIsSavingRole(false);
    }
  }

  function toggleWarehouse(id: string, checked: boolean) {
    setScope((current) => ({
      ...current,
      warehouseIds: checked ? [...current.warehouseIds, id] : current.warehouseIds.filter((w) => w !== id),
    }));
  }

  function toggleRestaurant(id: string, checked: boolean) {
    setScope((current) => ({
      ...current,
      restaurantIds: checked ? [...current.restaurantIds, id] : current.restaurantIds.filter((r) => r !== id),
    }));
  }

  async function handleScopeSave() {
    setScopeError(null);
    setIsSavingScope(true);
    try {
      await apiClient.put(`/api/v1/users/${params.id}/scope`, scope);
    } catch (caught) {
      setScopeError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ نطاق الصلاحية');
    } finally {
      setIsSavingScope(false);
    }
  }

  function togglePermission(code: string, checked: boolean) {
    setGrantedCodes((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(code);
      } else {
        next.delete(code);
      }
      return next;
    });
  }

  async function handlePermissionsSave() {
    setPermissionsError(null);
    setIsSavingPermissions(true);
    try {
      const permissions = permissionCatalogue
        .filter((p) => p.isGrantable)
        .map((p) => ({ code: p.code, isGranted: grantedCodes.has(p.code) }));
      await apiClient.put(`/api/v1/users/${params.id}/permissions`, { permissions });
    } catch (caught) {
      setPermissionsError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ الصلاحيات');
    } finally {
      setIsSavingPermissions(false);
    }
  }

  async function handleToggleActive() {
    setActiveError(null);
    setIsTogglingActive(true);
    try {
      await apiClient.post(`/api/v1/users/${params.id}/${user!.isActive ? 'deactivate' : 'reactivate'}`);
      await load();
    } catch (caught) {
      setActiveError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحديث حالة الحساب');
    } finally {
      setIsTogglingActive(false);
    }
  }

  if (isLoading) {
    return <LoadingSkeleton className="h-64 w-full" />;
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (!user) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground">{user.fullName}</h1>
          <p className="text-sm text-muted-foreground" dir="ltr">{formatMobileNumber(user.mobileNumber)}</p>
        </div>
        <Badge variant={user.isActive ? 'default' : 'secondary'}>{user.isActive ? 'نشط' : 'معطل'}</Badge>
      </div>

      {isSelf ? (
        <div className="rounded-lg border border-border bg-muted/50 p-3 text-sm text-muted-foreground">
          لا يمكنك تعديل دورك أو نطاقك أو تعطيل حسابك الخاص.
        </div>
      ) : null}

      {canManage ? (
        <Card>
          <CardHeader>
            <h2 className="font-heading text-base font-medium text-foreground">الدور</h2>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {roleError ? <ErrorBanner message={roleError} /> : null}
            <div className="flex flex-wrap items-end gap-2">
              <div className="flex flex-1 flex-col gap-1.5">
                <Label htmlFor="role-select">الدور الحالي</Label>
                <Select value={role} onValueChange={(value) => value && setRole(value as RoleName)} disabled={isSelf}>
                  <SelectTrigger id="role-select" className="w-full">
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
              <Button onClick={handleRoleChange} loading={isSavingRole} disabled={isSelf || role === user.role}>
                حفظ الدور
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {canScope && user.role && SCOPED_ROLES.includes(user.role) ? (
        <Card>
          <CardHeader>
            <h2 className="font-heading text-base font-medium text-foreground">نطاق الصلاحية</h2>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {scopeError ? <ErrorBanner message={scopeError} /> : null}
            {isLoadingScope ? <LoadingSkeleton className="h-20 w-full" /> : null}
            {!isLoadingScope && user.role === 'WarehouseStaff' ? (
              <div className="flex flex-col gap-2">
                <p className="text-sm font-medium text-foreground">المخازن المصرح بها</p>
                {warehouses.map((warehouse) => (
                  <label key={warehouse.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={scope.warehouseIds.includes(warehouse.id)}
                      onCheckedChange={(checked) => toggleWarehouse(warehouse.id, checked === true)}
                    />
                    {warehouse.nameArabic}
                  </label>
                ))}
              </div>
            ) : null}
            {!isLoadingScope && user.role === 'RestaurantSupervisor' ? (
              <div className="flex flex-col gap-2">
                <p className="text-sm font-medium text-foreground">الفروع المصرح بها</p>
                {restaurants.map((restaurant) => (
                  <label key={restaurant.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={scope.restaurantIds.includes(restaurant.id)}
                      onCheckedChange={(checked) => toggleRestaurant(restaurant.id, checked === true)}
                    />
                    {restaurant.nameArabic}
                  </label>
                ))}
              </div>
            ) : null}
            <Button onClick={handleScopeSave} loading={isSavingScope} disabled={isSelf} className="self-start">
              حفظ النطاق
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {canManage && permissionCatalogue.length > 0 ? (
        <Card>
          <CardHeader>
            <h2 className="font-heading text-base font-medium text-foreground">صلاحيات إضافية</h2>
            <p className="text-xs text-muted-foreground">
              الصلاحيات الأربع المحجوزة للمالك (التكاليف والتقييم وسجل التدقيق) غير قابلة للمنح لأي دور آخر.
            </p>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {permissionsError ? <ErrorBanner message={permissionsError} /> : null}
            <div className="grid grid-cols-1 gap-1.5 md:grid-cols-2">
              {permissionCatalogue
                .filter((p) => p.isGrantable)
                .map((permission) => (
                  <label key={permission.code} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={grantedCodes.has(permission.code)}
                      onCheckedChange={(checked) => togglePermission(permission.code, checked === true)}
                    />
                    {permission.description}
                  </label>
                ))}
            </div>
            <Button onClick={handlePermissionsSave} loading={isSavingPermissions} className="self-start">
              حفظ الصلاحيات
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {activeError ? <ErrorBanner message={activeError} /> : null}

      <div className="flex items-center gap-2">
        {canManage ? (
          <Button variant={user.isActive ? 'destructive' : 'default'} loading={isTogglingActive} disabled={isSelf} onClick={handleToggleActive}>
            {user.isActive ? 'تعطيل الحساب' : 'إعادة تفعيل الحساب'}
          </Button>
        ) : null}
        <Button variant="ghost" onClick={() => router.push('/users')}>
          رجوع لقائمة المستخدمين
        </Button>
      </div>
    </div>
  );
}
