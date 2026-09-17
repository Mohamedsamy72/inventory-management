import { cn } from '@/lib/utils';

/**
 * Task F1.8. A single shimmering block - callers compose multiple instances for
 * table-row or card-grid skeletons rather than this component knowing about layout.
 * Respects `prefers-reduced-motion` via the `animate-pulse` utility, which Tailwind
 * itself already gates on that media query.
 */
export function LoadingSkeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)} aria-hidden="true" />;
}

/** A skeleton row set matching a table's column count - the common F3+ list-loading case. */
export function TableLoadingSkeleton({ rows = 5, columns = 4 }: { rows?: number; columns?: number }) {
  return (
    <div className="space-y-2" role="status" aria-label="جارٍ التحميل">
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div key={rowIndex} className="flex gap-4">
          {Array.from({ length: columns }).map((_, columnIndex) => (
            <LoadingSkeleton key={columnIndex} className="h-8 flex-1" />
          ))}
        </div>
      ))}
    </div>
  );
}
