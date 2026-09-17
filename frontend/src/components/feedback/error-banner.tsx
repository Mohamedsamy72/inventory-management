import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface ErrorBannerProps {
  /** Already-parsed Arabic message (RFC 7807 `messageAr` - never the raw English error). */
  message: string;
  /**
   * Omit entirely for a non-retryable error (docs/frontend-ui-ux-implementation-guide.md
   * section 9.3 - e.g. `SERVING_WAREHOUSE_UNAVAILABLE`, where retrying cannot succeed and
   * showing a retry button would be a false promise).
   */
  onRetry?: () => void;
  className?: string;
}

/**
 * Task F1.8, guide section 6 state 4: "High-visibility red/amber error banner with
 * parsed Arabic message and `[ إعادة المحاولة ]` CTA button."
 */
export function ErrorBanner({ message, onRetry, className }: ErrorBannerProps) {
  return (
    <div
      role="alert"
      className={cn(
        'flex items-center justify-between gap-4 rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-destructive',
        className,
      )}
    >
      <div className="flex items-center gap-3">
        <AlertTriangle className="size-5 shrink-0" aria-hidden="true" />
        <p className="text-sm font-medium">{message}</p>
      </div>
      {onRetry ? (
        <Button variant="outline" size="sm" onClick={onRetry} className="shrink-0 border-destructive/30 text-destructive hover:bg-destructive/10">
          إعادة المحاولة
        </Button>
      ) : null}
    </div>
  );
}
