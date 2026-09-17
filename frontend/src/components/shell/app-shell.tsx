'use client';

import type { LucideIcon } from 'lucide-react';
import Link from 'next/link';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar';

export interface AppShellNavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  isActive?: boolean;
}

export interface AppShellBreadcrumb {
  label: string;
  href?: string;
}

export interface AppShellProps {
  navItems: AppShellNavItem[];
  breadcrumbs?: AppShellBreadcrumb[];
  /** Role badge, scope summary, user menu - filled in by Phase F2. */
  headerActions?: React.ReactNode;
  children: React.ReactNode;
}

/**
 * Task F1.9. The RTL app shell (docs/frontend-ui-ux-implementation-guide.md section 2):
 * a full-width top header, a right-hand sidebar (`side="right"` - docs/31 section 4.3:
 * "The sidebar sits on the right; the main canvas on the left", not something `dir="rtl"`
 * produces automatically for shadcn's physically-positioned sidebar primitive), and the
 * operational canvas. On narrow viewports the sidebar collapses into a drawer via
 * shadcn's own built-in mobile `Sheet` behaviour - no separate mobile-drawer component
 * was needed.
 *
 * Navigation content and header actions (role badge, user menu) are Phase F2's job -
 * this component only establishes the structural chrome, per Checkpoint 2's own listed
 * boundary between F1 and F2.
 */
export function AppShell({ navItems, breadcrumbs = [], headerActions, children }: AppShellProps) {
  return (
    <SidebarProvider>
      <Sidebar side="right" collapsible="icon">
        <SidebarHeader>
          <Link href="/" className="flex items-center gap-2 px-2 py-1.5 font-semibold text-sidebar-foreground">
            نظام إدارة المخازن
          </Link>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                {navItems.map((item) => (
                  <SidebarMenuItem key={item.href}>
                    <SidebarMenuButton asChild isActive={item.isActive} tooltip={item.label}>
                      <Link href={item.href}>
                        <item.icon />
                        <span>{item.label}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
      </Sidebar>

      <SidebarInset>
        <header className="flex h-14 shrink-0 items-center gap-3 border-b border-border bg-card px-4">
          <SidebarTrigger />
          {breadcrumbs.length > 0 ? (
            <Breadcrumb>
              <BreadcrumbList>
                {breadcrumbs.map((crumb, index) => {
                  const isLast = index === breadcrumbs.length - 1;
                  return (
                    <BreadcrumbItem key={`${crumb.label}-${index}`}>
                      {isLast || !crumb.href ? (
                        <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                      ) : (
                        <BreadcrumbLink asChild>
                          <Link href={crumb.href}>{crumb.label}</Link>
                        </BreadcrumbLink>
                      )}
                      {!isLast ? <BreadcrumbSeparator /> : null}
                    </BreadcrumbItem>
                  );
                })}
              </BreadcrumbList>
            </Breadcrumb>
          ) : null}
          <div className="ms-auto flex items-center gap-3">{headerActions}</div>
        </header>
        <main className="flex-1 p-6">{children}</main>
      </SidebarInset>
    </SidebarProvider>
  );
}
