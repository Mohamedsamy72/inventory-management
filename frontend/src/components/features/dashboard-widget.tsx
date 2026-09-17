'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { EmptyState } from '@/components/feedback/empty-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { apiClient, ApiError } from '@/lib/api-client';

interface KeysetPage<T> {
  items: T[];
  nextCursor: string | null;
}

export interface DashboardWidgetProps<T> {
  title: string;
  fetchPath: string;
  /** Client-side status filter - the list endpoints backing these widgets have no server-side
   * status filter, so this widget fetches a bounded page (200, matching this session's other
   * "/names"-style bounded lookups) and filters/counts from what actually came back. The count
   * shown is therefore always a real count of real returned rows (docs/12 §2.3), capped at 200
   * rather than a claimed exact total beyond that. */
  filter?: (item: T) => boolean;
  renderItem: (item: T) => string;
  emptyMessage: string;
  viewAllHref: string;
}

/**
 * Task F6 (docs/12 §2). Every number here is `filtered.length` from a real fetch - never a
 * hardcoded 0, never an estimate. A failed fetch shows an error banner with retry; an empty
 * result shows the Arabic empty state, never a blank card.
 */
export function DashboardWidget<T extends { id: string }>({
  title,
  fetchPath,
  filter,
  renderItem,
  emptyMessage,
  viewAllHref,
}: DashboardWidgetProps<T>) {
  const router = useRouter();
  const [items, setItems] = useState<T[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    setItems(null);
    try {
      const page = await apiClient.get<KeysetPage<T>>(`${fetchPath}${fetchPath.includes('?') ? '&' : '?'}limit=200`);
      setItems(filter ? page.items.filter(filter) : page.items);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل البيانات');
    }
  }

  useEffect(() => {
    void load();
  }, [fetchPath]);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <h2 className="font-heading text-base font-medium text-foreground">{title}</h2>
        {items ? <Badge variant={items.length > 0 ? 'default' : 'secondary'}>{items.length}</Badge> : null}
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {error ? <ErrorBanner message={error} onRetry={load} /> : null}
        {!error && items === null ? <LoadingSkeleton className="h-24 w-full" /> : null}
        {!error && items !== null && items.length === 0 ? <EmptyState message={emptyMessage} /> : null}
        {!error && items && items.length > 0 ? (
          <ul className="flex flex-col gap-1.5">
            {items.slice(0, 5).map((item) => (
              <li
                key={item.id}
                className="cursor-pointer rounded-md bg-muted px-3 py-1.5 text-sm hover:bg-muted/70"
                onClick={() => router.push(viewAllHref)}
              >
                {renderItem(item)}
              </li>
            ))}
          </ul>
        ) : null}
        {items && items.length > 0 ? (
          <Link href={viewAllHref} className="text-sm font-medium text-accent hover:underline">
            عرض الكل
          </Link>
        ) : null}
      </CardContent>
    </Card>
  );
}
