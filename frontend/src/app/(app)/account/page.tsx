'use client';

import { useState, type FormEvent } from 'react';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatMobileNumber } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';

type PasswordStep = 'idle' | 'code' | 'password' | 'done';
type MobileStep = 'idle' | 'code' | 'done';

/**
 * The Owner's OWN security-sensitive operations - deliberately not the administrative
 * "reset another user's password" screen (the backend refuses that mechanism for the Owner's own
 * account). Both flows are OTP-gated: the password change sends the code to the CURRENTLY
 * registered mobile; a new mobile number only becomes the login/recovery number after the code
 * sent to it is verified. Passwords are held only in local state and cleared after each attempt;
 * nothing secret is stored, logged, or shown back.
 */
export default function AccountPage() {
  const { profile } = useSession();

  const [passwordStep, setPasswordStep] = useState<PasswordStep>('idle');
  const [otpCode, setOtpCode] = useState('');
  const [resetToken, setResetToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordBusy, setPasswordBusy] = useState(false);

  const [mobileStep, setMobileStep] = useState<MobileStep>('idle');
  const [newMobile, setNewMobile] = useState('');
  const [mobileCode, setMobileCode] = useState('');
  const [changeToken, setChangeToken] = useState('');
  const [mobileError, setMobileError] = useState<string | null>(null);
  const [mobileBusy, setMobileBusy] = useState(false);

  if (profile && profile.role !== 'Owner') {
    return <ForbiddenState reason="forbidden" />;
  }

  async function run(setBusy: (value: boolean) => void, setError: (value: string | null) => void, action: () => Promise<void>, fallback: string) {
    setError(null);
    setBusy(true);
    try {
      await action();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : fallback);
    } finally {
      setBusy(false);
    }
  }

  const sendPasswordOtp = () =>
    run(setPasswordBusy, setPasswordError, async () => {
      await apiClient.post('/api/v1/account/password/otp', {});
      setPasswordStep('code');
    }, 'تعذر إرسال رمز التحقق');

  const verifyPasswordOtp = (event: FormEvent) => {
    event.preventDefault();
    return run(setPasswordBusy, setPasswordError, async () => {
      const result = await apiClient.post<{ resetToken: string }>('/api/v1/account/password/verify', { code: otpCode });
      setResetToken(result.resetToken);
      setOtpCode('');
      setPasswordStep('password');
    }, 'تعذر التحقق من الرمز');
  };

  const changePassword = (event: FormEvent) => {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      setPasswordError('كلمتا المرور غير متطابقتين');
      return Promise.resolve();
    }
    return run(setPasswordBusy, setPasswordError, async () => {
      try {
        await apiClient.post('/api/v1/account/password/change', { resetToken, newPassword });
        setPasswordStep('done');
        setResetToken('');
      } finally {
        setNewPassword('');
        setConfirmPassword('');
      }
    }, 'تعذر تغيير كلمة المرور');
  };

  const sendMobileOtp = (event: FormEvent) => {
    event.preventDefault();
    return run(setMobileBusy, setMobileError, async () => {
      const result = await apiClient.post<{ changeToken: string }>('/api/v1/account/mobile/otp', { newMobileNumber: newMobile });
      setChangeToken(result.changeToken);
      setMobileStep('code');
    }, 'تعذر إرسال رمز التحقق');
  };

  const confirmMobile = (event: FormEvent) => {
    event.preventDefault();
    return run(setMobileBusy, setMobileError, async () => {
      await apiClient.post('/api/v1/account/mobile/confirm', { changeToken, code: mobileCode });
      setMobileCode('');
      setMobileStep('done');
    }, 'تعذر تأكيد رقم الجوال');
  };

  return (
    <div className="flex max-w-xl flex-col gap-4">
      <h1 className="font-heading text-xl font-medium text-foreground">حسابي</h1>
      {profile ? (
        <p className="text-sm text-muted-foreground">
          {profile.fullName} · <span dir="ltr">{formatMobileNumber(profile.mobileNumber)}</span>
        </p>
      ) : null}

      <Card>
        <CardHeader>
          <h2 className="font-heading text-base font-medium text-foreground">تغيير كلمة المرور</h2>
          <p className="text-xs text-muted-foreground">يتم إرسال رمز تحقق إلى رقم جوالك المسجل قبل السماح بالتغيير.</p>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {passwordError ? <ErrorBanner message={passwordError} /> : null}

          {passwordStep === 'idle' ? (
            <Button onClick={() => void sendPasswordOtp()} loading={passwordBusy} className="self-start">
              إرسال رمز التحقق
            </Button>
          ) : null}

          {passwordStep === 'code' ? (
            <form onSubmit={verifyPasswordOtp} className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="password-otp">رمز التحقق</Label>
                <Input id="password-otp" dir="ltr" inputMode="numeric" autoComplete="one-time-code" required value={otpCode} onChange={(event) => setOtpCode(event.target.value)} />
              </div>
              <Button type="submit" loading={passwordBusy} className="self-start">
                تحقق
              </Button>
            </form>
          ) : null}

          {passwordStep === 'password' ? (
            <form onSubmit={changePassword} className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="own-new-password">كلمة المرور الجديدة</Label>
                <Input id="own-new-password" type="password" dir="ltr" autoComplete="new-password" required value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="own-confirm-password">تأكيد كلمة المرور الجديدة</Label>
                <Input id="own-confirm-password" type="password" dir="ltr" autoComplete="new-password" required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} />
              </div>
              <Button type="submit" loading={passwordBusy} className="self-start">
                تغيير كلمة المرور
              </Button>
            </form>
          ) : null}

          {passwordStep === 'done' ? <p className="text-sm text-badge-green-fg">تم تغيير كلمة المرور بنجاح.</p> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <h2 className="font-heading text-base font-medium text-foreground">تغيير رقم الجوال</h2>
          <p className="text-xs text-muted-foreground">لا يصبح الرقم الجديد فعالاً إلا بعد التحقق من الرمز المرسل إليه.</p>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {mobileError ? <ErrorBanner message={mobileError} /> : null}

          {mobileStep === 'idle' ? (
            <form onSubmit={sendMobileOtp} className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="new-mobile">رقم الجوال الجديد</Label>
                <Input id="new-mobile" type="tel" dir="ltr" required value={newMobile} onChange={(event) => setNewMobile(event.target.value)} />
              </div>
              <Button type="submit" loading={mobileBusy} className="self-start">
                إرسال رمز التحقق
              </Button>
            </form>
          ) : null}

          {mobileStep === 'code' ? (
            <form onSubmit={confirmMobile} className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="mobile-otp">رمز التحقق المرسل إلى الرقم الجديد</Label>
                <Input id="mobile-otp" dir="ltr" inputMode="numeric" autoComplete="one-time-code" required value={mobileCode} onChange={(event) => setMobileCode(event.target.value)} />
              </div>
              <Button type="submit" loading={mobileBusy} className="self-start">
                تأكيد الرقم الجديد
              </Button>
            </form>
          ) : null}

          {mobileStep === 'done' ? <p className="text-sm text-badge-green-fg">تم تغيير رقم الجوال. استخدم الرقم الجديد في تسجيل الدخول.</p> : null}
        </CardContent>
      </Card>
    </div>
  );
}
