'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { apiClient, ApiError } from '@/lib/api-client';
import Link from 'next/link';

/** Task F2 - mobile + password login (docs/08). */
export default function LoginPage() {
  const router = useRouter();
  const [mobileNumber, setMobileNumber] = useState('');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await apiClient.post('/api/v1/auth/login', { mobileNumber, password }, { skipAuthRedirect: true });
      router.push('/dashboard');
    } catch (caught) {
      // Login's own 401 (wrong credentials) must not trigger api-client's generic
      // redirect-to-/login behaviour - that's a window.location.assign hard navigation
      // that would discard this component's state before setError below ever rendered.
      // 403 (ACCOUNT_INACTIVE) is not remapped by api-client at all and lands here normally.
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تسجيل الدخول، يرجى المحاولة لاحقاً');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        {/* The page's one semantic h1 (heading-hierarchy) - CardTitle renders a plain
            div, which is correct for a card that ISN'T the page's main heading, but
            login's title is exactly that. */}
        <h1 className="text-center font-heading text-xl font-medium">نظام إدارة المخازن</h1>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {error ? <ErrorBanner message={error} /> : null}

          <div className="flex flex-col gap-1.5">
            <label htmlFor="mobileNumber" className="text-sm font-medium text-foreground">
              رقم الجوال
            </label>
            <Input
              id="mobileNumber"
              type="tel"
              dir="ltr"
              autoComplete="username"
              required
              value={mobileNumber}
              onChange={(event) => setMobileNumber(event.target.value)}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="password" className="text-sm font-medium text-foreground">
              كلمة المرور
            </label>
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>

          <Button type="submit" loading={isSubmitting} className="mt-2 w-full">
            تسجيل الدخول
          </Button>

          <Link href="/forgot-password" className="text-center text-sm text-accent hover:underline">
            نسيت كلمة المرور؟
          </Link>
        </form>
      </CardContent>
    </Card>
  );
}
