'use client';

import { useState } from 'react';
import { Trash2 } from 'lucide-react';
import { apiClient, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ErrorBanner } from '@/components/feedback/error-banner';

interface DeleteRecordButtonProps {
  /** DELETE target, e.g. `/api/v1/categories/{id}`. */
  path: string;
  /** Human name shown in the confirmation. */
  name: string;
  onDeleted: () => void;
}

/**
 * Permanent delete for a master-data record, always behind an explicit confirmation. The server is
 * the authority: a record that any document/ledger row references answers 409 IN_USE (the Arabic
 * message points the user to deactivation instead) and nothing is removed.
 */
export function DeleteRecordButton({ path, name, onDeleted }: DeleteRecordButtonProps) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirm() {
    setBusy(true);
    setError(null);
    try {
      await apiClient.delete(path);
      setOpen(false);
      onDeleted();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر الحذف');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        aria-label={`حذف ${name}`}
      >
        <Trash2 />
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>تأكيد الحذف</DialogTitle>
            <DialogDescription>
              سيتم حذف «{name}» نهائياً. لا يمكن حذف سجل مستخدم في أي حركة؛ في هذه الحالة استخدم إلغاء التفعيل.
            </DialogDescription>
          </DialogHeader>
          {error ? <ErrorBanner message={error} /> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              إلغاء
            </Button>
            <Button variant="destructive" loading={busy} onClick={() => void confirm()}>
              تأكيد الحذف
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
