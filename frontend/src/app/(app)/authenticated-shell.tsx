'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { LogOut } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { getNavItemsForRole } from '@/lib/auth/nav-items';
import { RoleBadge, ScopeDisplay } from '@/components/domain/role-badge';
import { AppShell } from '@/components/shell/app-shell';
import { Button } from '@/components/ui/button';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { apiClient } from '@/lib/api-client';

/**
 * Task F2. The client-side half of the auth guard: while the session is loading,
 * render a full-page skeleton rather than a flash of the shell with empty nav; once
 * loaded, a missing profile means the `/account/me` call 401'd, which `api-client`
 * has already redirected away from - render nothing while that navigation completes
 * rather than a flash of unauthenticated shell content.
 */
export function AuthenticatedShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { profile, isLoading, error, refetch } = useSession();

  useEffect(() => {
    if (!isLoading && !profile && !error) {
      router.replace('/login');
    }
  }, [isLoading, profile, error, router]);

  if (isLoading) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-8">
        <LoadingSkeleton className="h-40 w-full max-w-md" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-8">
        <ErrorBanner message={error} onRetry={refetch} className="max-w-md" />
      </div>
    );
  }

  if (!profile) {
    return null;
  }

  async function handleLogout() {
    await apiClient.post('/api/v1/auth/logout');
    router.push('/login');
  }

  return (
    <AppShell
      navItems={getNavItemsForRole(profile.role)}
      headerActions={
        <>
          <ScopeDisplay warehouseCount={profile.warehouseScopeIds.length} restaurantCount={profile.restaurantScopeIds.length} />
          <RoleBadge role={profile.role} />
          <span className="text-sm font-medium text-foreground">{profile.fullName}</span>
          <Button variant="ghost" size="icon" onClick={handleLogout} aria-label="تسجيل الخروج">
            <LogOut />
          </Button>
        </>
      }
    >
      {children}
    </AppShell>
  );
}
