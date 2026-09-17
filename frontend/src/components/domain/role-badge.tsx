import { Badge } from '@/components/ui/badge';
import type { RoleName } from '@/lib/auth/types';

const ROLE_LABELS: Record<RoleName, string> = {
  Owner: 'المالك',
  Admin: 'مدير',
  WarehouseStaff: 'موظف مخزن',
  RestaurantSupervisor: 'مشرف فرع',
  User: 'مستخدم',
};

/** Task F2 - the role badge shown in the header. */
export function RoleBadge({ role }: { role: RoleName | null }) {
  if (!role) {
    return null;
  }

  return <Badge className="border-transparent bg-badge-blue-bg text-badge-blue-fg">{ROLE_LABELS[role]}</Badge>;
}

export interface ScopeDisplayProps {
  warehouseCount: number;
  restaurantCount: number;
}

/**
 * A compact scope summary ("N مستودعات · M فروع") - the header-level indication that
 * this user's access is scoped, not a full picker. Roles with unrestricted access
 * (Owner/Admin) render nothing here, since docs/03 defines their scope as the whole
 * tenant, not an enumerable list.
 */
export function ScopeDisplay({ warehouseCount, restaurantCount }: ScopeDisplayProps) {
  if (warehouseCount === 0 && restaurantCount === 0) {
    return null;
  }

  const parts: string[] = [];
  if (warehouseCount > 0) {
    parts.push(`${warehouseCount} ${warehouseCount === 1 ? 'مستودع' : 'مستودعات'}`);
  }
  if (restaurantCount > 0) {
    parts.push(`${restaurantCount} ${restaurantCount === 1 ? 'فرع' : 'فروع'}`);
  }

  return <span className="text-sm text-muted-foreground">{parts.join(' · ')}</span>;
}
