'use client';

import { useState, type FormEvent } from 'react';
import { apiClient, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';

export interface ManagedUser {
  id: string;
  fullName: string;
  mobileNumber: string;
  role: string | null;
}

/**
 * Name (any user manager) and mobile number (Owner only - it is the login identifier) of a
 * NON-Owner account. An Owner's own details are never edited here (the backend refuses it): the
 * Owner changes their own mobile through the OTP flow on /account.
 */
export function UserProfileCard({ user, canEditMobile, onSaved }: { user: ManagedUser; canEditMobile: boolean; onSaved: () => void }) {
  const [fullName, setFullName] = useState(user.fullName);
  const [mobileNumber, setMobileNumber] = useState(user.mobileNumber);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSaved(false);
    setIsSaving(true);
    try {
      await apiClient.put(`/api/v1/users/${user.id}`, { fullName, mobileNumber });
      setSaved(true);
      onSaved();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ بيانات الحساب');
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="font-heading text-base font-medium text-foreground">بيانات الحساب</h2>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSave} className="flex flex-col gap-3">
          {error ? <ErrorBanner message={error} /> : null}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="profile-name">الاسم الكامل</Label>
            <Input id="profile-name" required value={fullName} onChange={(event) => setFullName(event.target.value)} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="profile-mobile">رقم الجوال</Label>
            <Input
              id="profile-mobile"
              type="tel"
              dir="ltr"
              required
              disabled={!canEditMobile}
              value={mobileNumber}
              onChange={(event) => setMobileNumber(event.target.value)}
            />
            {!canEditMobile ? <p className="text-xs text-muted-foreground">تغيير رقم الجوال متاح للمالك فقط.</p> : null}
          </div>
          {saved ? <p className="text-sm text-badge-green-fg">تم حفظ بيانات الحساب.</p> : null}
          <Button type="submit" loading={isSaving} className="self-start">
            حفظ
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

/**
 * Owner-only administrative password reset for a non-Owner user: the Owner does not need the old
 * password; the backend enforces the Identity policy, revokes the target's sessions and audits the
 * event without any secret. The typed password is cleared after every attempt and never displayed.
 */
export function UserPasswordCard({ userId }: { userId: string }) {
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setDone(false);
    if (password !== confirm) {
      setError('كلمتا المرور غير متطابقتين');
      return;
    }
    setIsSaving(true);
    try {
      await apiClient.post(`/api/v1/users/${userId}/password`, { newPassword: password });
      setDone(true);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تغيير كلمة المرور');
    } finally {
      setPassword('');
      setConfirm('');
      setIsSaving(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="font-heading text-base font-medium text-foreground">تغيير كلمة المرور</h2>
        <p className="text-xs text-muted-foreground">
          لا تحتاج إلى كلمة المرور الحالية. سيتم إنهاء جلسات المستخدم الحالية فوراً.
        </p>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-3">
          {error ? <ErrorBanner message={error} /> : null}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="admin-new-password">كلمة المرور الجديدة</Label>
            <Input id="admin-new-password" type="password" dir="ltr" autoComplete="new-password" required value={password} onChange={(event) => setPassword(event.target.value)} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="admin-confirm-password">تأكيد كلمة المرور</Label>
            <Input id="admin-confirm-password" type="password" dir="ltr" autoComplete="new-password" required value={confirm} onChange={(event) => setConfirm(event.target.value)} />
          </div>
          {done ? <p className="text-sm text-badge-green-fg">تم تغيير كلمة المرور.</p> : null}
          <Button type="submit" loading={isSaving} disabled={!password || !confirm} className="self-start">
            تغيير كلمة المرور
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
