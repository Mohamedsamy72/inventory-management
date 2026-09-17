'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { apiClient, ApiError } from '@/lib/api-client';

type Step = 'request-otp' | 'verify-otp' | 'reset-password';

/**
 * Task F2 - the forgot-password/OTP/reset flow, as one page with internal step state
 * rather than three routes: the reset token from step 2 only has meaning as input to
 * step 3 within the same visit, so there is nothing to deep-link to mid-flow anyway.
 */
export default function ForgotPasswordPage() {
  const router = useRouter();
  const [step, setStep] = useState<Step>('request-otp');
  const [mobileNumber, setMobileNumber] = useState('');
  const [code, setCode] = useState('');
  const [resetToken, setResetToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function requestOtp(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      // Task 3.9: the server returns 200 whether or not the number is registered
      // (enumeration prevention) - the UI has nothing more specific to say either.
      await apiClient.post('/api/v1/auth/forgot-password/otp', { mobileNumber });
      setStep('verify-otp');
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر إرسال رمز التحقق');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function verifyOtp(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await apiClient.post<{ resetToken: string }>('/api/v1/auth/forgot-password/verify', {
        mobileNumber,
        code,
      });
      setResetToken(result.resetToken);
      setStep('reset-password');
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'رمز التحقق غير صحيح');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function resetPassword(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await apiClient.post('/api/v1/auth/forgot-password/reset', { mobileNumber, resetToken, newPassword });
      router.push('/login');
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تعيين كلمة المرور الجديدة');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <h1 className="text-center font-heading text-xl font-medium">استعادة كلمة المرور</h1>
      </CardHeader>
      <CardContent>
        {error ? <ErrorBanner message={error} className="mb-4" /> : null}

        {step === 'request-otp' && (
          <form onSubmit={requestOtp} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="mobileNumber" className="text-sm font-medium text-foreground">
                رقم الجوال
              </label>
              <Input
                id="mobileNumber"
                type="tel"
                dir="ltr"
                required
                value={mobileNumber}
                onChange={(event) => setMobileNumber(event.target.value)}
              />
            </div>
            <Button type="submit" loading={isSubmitting} className="w-full">
              إرسال رمز التحقق
            </Button>
          </form>
        )}

        {step === 'verify-otp' && (
          <form onSubmit={verifyOtp} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="code" className="text-sm font-medium text-foreground">
                رمز التحقق
              </label>
              <Input id="code" dir="ltr" required value={code} onChange={(event) => setCode(event.target.value)} />
            </div>
            <Button type="submit" loading={isSubmitting} className="w-full">
              تأكيد الرمز
            </Button>
          </form>
        )}

        {step === 'reset-password' && (
          <form onSubmit={resetPassword} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="newPassword" className="text-sm font-medium text-foreground">
                كلمة المرور الجديدة
              </label>
              <Input
                id="newPassword"
                type="password"
                autoComplete="new-password"
                required
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
              />
            </div>
            <Button type="submit" loading={isSubmitting} className="w-full">
              تعيين كلمة المرور
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
