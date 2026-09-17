import type { LucideIcon } from 'lucide-react';
import { Inbox } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface EmptyStateProps {
  /** e.g. "لا توجد سجلات مطابقة" (docs/31 4.6). */
  message: string;
  description?: string;
  icon?: LucideIcon;
  action?: { label: string; onClick: () => void };
}

/**
 * Task F1.8. Zero fake data (docs/12 2.3) starts here: an empty result set is always
 * this component, never a hardcoded placeholder row or a sample chart with invented
 * numbers.
 */
export function EmptyState({ message, description, icon: Icon = Inbox, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed border-border p-12 text-center">
      <Icon className="size-10 text-muted-foreground" aria-hidden="true" />
      <p className="font-medium text-foreground">{message}</p>
      {description ? <p className="text-sm text-muted-foreground">{description}</p> : null}
      {action ? (
        <Button variant="outline" onClick={action.onClick} className="mt-2">
          {action.label}
        </Button>
      ) : null}
    </div>
  );
}
