import {
  LayoutDashboard,
  Package,
  Tags,
  Building2,
  Truck,
  Inbox,
  ClipboardList,
  Send,
  ClipboardCheck,
  TriangleAlert,
  UtensilsCrossed,
  ShieldCheck,
  Users,
  Settings,
} from 'lucide-react';
import type { AppShellNavItem } from '@/components/shell/app-shell';
import type { RoleName } from './types';

/**
 * docs/12 section 1 - the literal per-role menu content. Rendered by role directly
 * (not derived from `permissionCodes`) because docs/12 specifies these as fixed,
 * curated menus per role, not a generic permission-to-menu-item mapping; the server
 * remains the authority regardless of what this list shows (guide section 8.6 -
 * "Navigation and controls are hidden by permission for clarity, never for security").
 *
 * `/reports` is never included anywhere, for any role - the route is not registered at
 * all (ADR-029, docs/12 section 3), so it has no nav entry to omit conditionally.
 */
export function getNavItemsForRole(role: RoleName | null): AppShellNavItem[] {
  switch (role) {
    case 'Owner':
      return [
        { href: '/dashboard', label: 'لوحة التحكم الرئيسية', icon: LayoutDashboard },
        { href: '/items', label: 'الأصناف', icon: Package },
        { href: '/master-data', label: 'الأقسام والوحدات', icon: Tags },
        { href: '/locations', label: 'المخازن والفروع', icon: Building2 },
        { href: '/suppliers', label: 'الموردون', icon: Truck },
        { href: '/receiving', label: 'الداخل الى المخزن', icon: Inbox },
        { href: '/supply-requests', label: 'طلبات البضاعه', icon: ClipboardList },
        { href: '/supplies', label: 'الصادر الى المطعم', icon: Send },
        { href: '/stock-counts', label: 'الجرد الفعلي', icon: ClipboardCheck },
        { href: '/discrepancies', label: 'سجل الفروقات', icon: TriangleAlert },
        { href: '/consumption', label: 'سجلات الاستهلاك', icon: UtensilsCrossed },
        { href: '/audit', label: 'سجل التدقيق', icon: ShieldCheck },
        { href: '/users', label: 'ادارة المستخدمين', icon: Users },
        { href: '/settings', label: 'إعدادات المنشأة', icon: Settings },
      ];
    case 'Admin':
      return [
        { href: '/dashboard', label: 'لوحة التحكم التشغيلية', icon: LayoutDashboard },
        { href: '/items', label: 'الأصناف', icon: Package },
        { href: '/master-data', label: 'الأقسام والوحدات', icon: Tags },
        { href: '/locations', label: 'المخازن والفروع', icon: Building2 },
        { href: '/suppliers', label: 'الموردون', icon: Truck },
        { href: '/receiving', label: 'الداخل الى المخزن', icon: Inbox },
        { href: '/supply-requests', label: 'طلبات البضاعه', icon: ClipboardList },
        { href: '/supplies', label: 'الصادر الى المطعم', icon: Send },
        { href: '/stock-counts', label: 'الجرد الفعلي', icon: ClipboardCheck },
        { href: '/discrepancies', label: 'سجل الفروقات', icon: TriangleAlert },
        { href: '/users', label: 'ادارة المستخدمين', icon: Users },
        { href: '/settings', label: 'الإعدادات التشغيلية', icon: Settings },
      ];
    case 'WarehouseStaff':
      return [
        { href: '/dashboard', label: 'مهام المخزن', icon: LayoutDashboard },
        { href: '/receiving', label: 'الداخل الى المخزن', icon: Inbox },
        { href: '/supply-requests', label: 'الطلبيات الواردة من المطعم', icon: ClipboardList },
        { href: '/supplies', label: 'الصادر الى المطعم', icon: Send },
      ];
    case 'RestaurantSupervisor':
      return [
        { href: '/dashboard', label: 'مهام الفرع', icon: LayoutDashboard },
        { href: '/supply-requests', label: 'طلبات البضاعه', icon: ClipboardList },
        { href: '/supplies', label: 'تأكيد استلام البضاعه', icon: Send },
        { href: '/discrepancies', label: 'فروقات الاستلام', icon: TriangleAlert },
      ];
    default:
      return [];
  }
}
