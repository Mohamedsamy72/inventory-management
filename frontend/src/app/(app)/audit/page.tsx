'use client';

import { notFound } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';

/**
 * Task F2/docs/frontend-ui-ux-implementation-guide.md section 8.6: "The Owner-only
 * `/audit` route is not registered in the router for other roles, so a direct URL
 * yields the standard not-found page rather than a hint that the feature exists."
 *
 * Next.js has no per-role route-registration primitive at the file-system-routing
 * level, so the equivalent behaviour is achieved here: any role other than Owner
 * calling this URL gets the framework's own not-found page via `notFound()`, not a
 * ForbiddenState (which would itself be the "hint that the feature exists" this rule
 * forbids - a 403 confirms an audit feature is there to be denied; a 404 does not).
 * The real audit content (Phase F6+) replaces this placeholder without changing this
 * guard.
 */
export default function AuditPage() {
  const { profile, isLoading } = useSession();

  if (isLoading) {
    return <LoadingSkeleton className="h-40 w-full" />;
  }

  if (profile && profile.role !== 'Owner') {
    notFound();
  }

  return (
    <div className="rounded-lg border border-dashed border-border p-8 text-center text-muted-foreground">
      سجل التدقيق قادم في مرحلة لاحقة من المشروع.
    </div>
  );
}
