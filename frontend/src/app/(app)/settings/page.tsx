'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatDateTime } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';

interface CompanySettings {
  companyName: string;
  companyCode: string;
  timezoneId: string;
  currencyCode: string;
  updatedAt: string;
}

const TIMEZONES: { id: string; label: string }[] = [
  { id: 'Africa/Cairo', label: 'القاهرة (Africa/Cairo)' },
  { id: 'Asia/Riyadh', label: 'الرياض (Asia/Riyadh)' },
  { id: 'Asia/Dubai', label: 'دبي (Asia/Dubai)' },
  { id: 'Asia/Kuwait', label: 'الكويت (Asia/Kuwait)' },
  { id: 'Europe/London', label: 'لندن (Europe/London)' },
];

/**
 * Change 5. Previously `/settings` was linked from the nav for Owner/Admin but had no page (404)
 * and no backend endpoint at all. The only tenant settings the plan actually defines are
 * `company_settings` - timezone (drives document-numbering periods, ADR-024) and currency - so
 * that is exactly what this shows; nothing else is invented. Company name/code and currency are
 * read-only (no setter exists by design). The backend (`settings:manage`, Owner/Admin only) is the
 * authority - this screen is a convenience layer over `GET/PUT /api/v1/settings`.
 */
export default function SettingsPage() {
  const { profile } = useSession();
  const [settings, setSettings] = useState<CompanySettings | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [timezoneId, setTimezoneId] = useState('');
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  async function load() {
    setLoadError(null);
    setSettings(null);
    try {
      const result = await apiClient.get<CompanySettings>('/api/v1/settings');
      setSettings(result);
      setTimezoneId(result.timezoneId);
    } catch (caught) {
      setLoadError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل الإعدادات');
    }
  }

  useEffect(() => {
    void load();
  }, []);

  if (profile && !profile.permissionCodes.includes('settings:manage')) {
    return <ForbiddenState reason="forbidden" />;
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setSaveError(null);
    setSaved(false);
    setIsSaving(true);
    try {
      const result = await apiClient.put<CompanySettings>('/api/v1/settings', { timezoneId });
      setSettings(result);
      setSaved(true);
    } catch (caught) {
      setSaveError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ الإعدادات');
    } finally {
      setIsSaving(false);
    }
  }

  const options = settings && !TIMEZONES.some((zone) => zone.id === settings.timezoneId)
    ? [{ id: settings.timezoneId, label: settings.timezoneId }, ...TIMEZONES]
    : TIMEZONES;

  return (
    <div className="flex max-w-xl flex-col gap-4">
      <h1 className="font-heading text-xl font-medium text-foreground">
        {profile?.role === 'Owner' ? 'إعدادات المنشأة' : 'الإعدادات التشغيلية'}
      </h1>

      {loadError ? <ErrorBanner message={loadError} onRetry={load} /> : null}
      {!loadError && settings === null ? <LoadingSkeleton className="h-48 w-full" /> : null}

      {settings ? (
        <form onSubmit={handleSave} className="flex flex-col gap-4 rounded-lg border border-border p-4">
          <dl className="grid grid-cols-2 gap-2 text-sm">
            <dt className="text-muted-foreground">المنشأة</dt>
            <dd className="text-foreground">{settings.companyName}</dd>
            <dt className="text-muted-foreground">رمز المنشأة</dt>
            <dd className="text-foreground" dir="ltr">{settings.companyCode}</dd>
            <dt className="text-muted-foreground">العملة</dt>
            <dd className="text-foreground" dir="ltr">{settings.currencyCode}</dd>
            <dt className="text-muted-foreground">آخر تحديث</dt>
            <dd className="text-foreground">{formatDateTime(settings.updatedAt)}</dd>
          </dl>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="settings-timezone">المنطقة الزمنية</Label>
            <Select value={timezoneId} onValueChange={(value) => value && setTimezoneId(value)}>
              <SelectTrigger id="settings-timezone" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {options.map((zone) => (
                  <SelectItem key={zone.id} value={zone.id}>
                    {zone.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              تُستخدم لتحديد الفترة الشهرية في ترقيم المستندات. لا تؤثر على المستندات الصادرة سابقاً.
            </p>
          </div>

          {saveError ? <ErrorBanner message={saveError} /> : null}
          {saved ? <p className="text-sm text-badge-green-fg">تم حفظ الإعدادات.</p> : null}

          <Button type="submit" loading={isSaving} disabled={timezoneId === settings.timezoneId} className="self-start">
            حفظ
          </Button>
        </form>
      ) : null}
    </div>
  );
}
