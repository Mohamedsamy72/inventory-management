import {
  LayoutDashboard,
  Package,
  Tags,
  Building2,
  Truck,
  Inbox,
  ClipboardList,
  Send,
  TriangleAlert,
  UtensilsCrossed,
  ShieldCheck,
  Users,
  Settings,
  UserCog,
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
/**
 * The base "User" role has no fixed menu (docs/03 §1): its sidebar is built from the permissions it
 * was explicitly granted. Only a navigation convenience - every endpoint still enforces its own policy.
 */
const PERMISSION_NAV: { codes: string[]; item: AppShellNavItem }[] = [
  { codes: ['items:view'], item: { href: '/items', label: 'الأصناف', icon: Package } },
  { codes: ['categories:manage', 'units:manage'], item: { href: '/master-data', label: 'الأقسام والوحدات', icon: Tags } },
  { codes: ['warehouses:manage', 'restaurants:manage'], item: { href: '/locations', label: 'المخازن والفروع', icon: Building2 } },
  { codes: ['suppliers:manage'], item: { href: '/suppliers', label: 'الموردون', icon: Truck } },
  { codes: ['receiving:view'], item: { href: '/receiving', label: 'الداخل الى المخزن', icon: Inbox } },
  { codes: ['supply_requests:view'], item: { href: '/supply-requests', label: 'طلبات البضاعه', icon: ClipboardList } },
  { codes: ['supplies:view'], item: { href: '/supplies', label: 'الصادر الى المطعم', icon: Send } },
  { codes: ['discrepancies:view'], item: { href: '/discrepancies', label: 'سجل الفروقات', icon: TriangleAlert } },
  { codes: ['consumption:view'], item: { href: '/consumption', label: 'سجلات الاستهلاك', icon: UtensilsCrossed } },
  { codes: ['users:view'], item: { href: '/users', label: 'ادارة المستخدمين', icon: Users } },
  { codes: ['settings:manage'], item: { href: '/settings', label: 'الإعدادات', icon: Settings } },
];

export function getNavItemsForRole(role: RoleName | null, permissionCodes: string[] = []): AppShellNavItem[] {
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
        // '/stock-counts' temporarily hidden from the nav on request - route untouched.
        { href: '/discrepancies', label: 'سجل الفروقات', icon: TriangleAlert },
        { href: '/consumption', label: 'سجلات الاستهلاك', icon: UtensilsCrossed },
        { href: '/audit', label: 'الحركات', icon: ShieldCheck },
        { href: '/users', label: 'ادارة المستخدمين', icon: Users },
        { href: '/settings', label: 'إعدادات المنشأة', icon: Settings },
        { href: '/account', label: 'حسابي', icon: UserCog },
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
        // '/stock-counts' temporarily hidden from the nav on request - route untouched.
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
        { href: '/discrepancies', label: 'سجل الفروقات', icon: TriangleAlert },
      ];
    case 'RestaurantSupervisor':
      return [
        { href: '/dashboard', label: 'مهام الفرع', icon: LayoutDashboard },
        { href: '/supply-requests', label: 'طلبات البضاعه', icon: ClipboardList },
        { href: '/supplies', label: 'تأكيد استلام البضاعه', icon: Send },
        { href: '/discrepancies', label: 'فروقات الاستلام', icon: TriangleAlert },
      ];
    default:
      return PERMISSION_NAV.filter((entry) => entry.codes.some((code) => permissionCodes.includes(code))).map((entry) => entry.item);
  }
}
