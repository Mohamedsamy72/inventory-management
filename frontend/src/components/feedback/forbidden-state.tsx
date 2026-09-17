import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';

export type ForbiddenReason = 'forbidden' | 'out-of-scope';

const MESSAGES: Record<ForbiddenReason, string> = {
  // docs/frontend-ui-ux-implementation-guide.md section 8.6 - authenticated but the
  // permission itself is missing.
  forbidden: 'غير مصرح لك بالوصول إلى هذه الصفحة',
  // Authenticated, holds the permission, but the specific record is outside this
  // user's warehouse/restaurant scope (docs/09 section 2.1's IDOR-prevention distinction
  // from a plain 404).
  'out-of-scope': 'هذا السجل خارج نطاق صلاحياتك',
};

export interface ForbiddenStateProps {
  reason: ForbiddenReason;
  onBack?: () => void;
}

/**
 * Task F1.8. One of the three distinguishable unauthorized states (guide section 8.6) -
 * the other two (401 -> redirect to /login, and an unregistered route for a role with
 * no access at all -> the framework's own not-found page) are not rendered UI states at
 * all, so they have no component here. Never a blank screen, never a silent redirect for
 * THIS case specifically - the user is told why, in Arabic, with a way back.
 */
export function ForbiddenState({ reason, onBack }: ForbiddenStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border p-12 text-center">
      <ShieldAlert className="size-10 text-muted-foreground" aria-hidden="true" />
      <p className="font-medium text-foreground">{MESSAGES[reason]}</p>
      {onBack ? (
        <Button variant="outline" onClick={onBack} className="mt-2">
          رجوع
        </Button>
      ) : null}
    </div>
  );
}
