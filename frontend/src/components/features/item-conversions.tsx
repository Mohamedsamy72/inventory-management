'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { Trash2 } from 'lucide-react';
import { apiClient, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { EmptyState } from '@/components/feedback/empty-state';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';

interface ConversionSummary {
  id: string;
  itemId: string;
  fromUnitId: string;
  toBaseUnitId: string;
  conversionFactor: number;
  isActive: boolean;
}

interface UnitOption {
  id: string;
  nameArabic: string;
}

export interface ItemConversionsProps {
  itemId: string;
  baseUnitId: string;
  units: UnitOption[];
}

/**
 * Task 6.1/F3. Nested inside the item edit dialog - a conversion has no meaning outside
 * the item it belongs to (`ItemUnitConversionsEndpoints`'s own comment). The target unit
 * is always the item's current base unit, resolved server-side (ADR-023) - the form never
 * shows or sends a "to" unit.
 */
export function ItemConversions({ itemId, baseUnitId, units }: ItemConversionsProps) {
  const [conversions, setConversions] = useState<ConversionSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fromUnitId, setFromUnitId] = useState('');
  const [factor, setFactor] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function unitName(id: string): string {
    return units.find((unit) => unit.id === id)?.nameArabic ?? '—';
  }

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await apiClient.get<ConversionSummary[]>(`/api/v1/items/${itemId}/conversions`);
      setConversions(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل وحدات التحويل');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [itemId]);

  async function handleAdd(event: FormEvent) {
    event.preventDefault();
    // Same React-portal event-bubbling issue as QuickAddModal (see its handleSubmit comment) -
    // this form is nested inside the item edit form and would otherwise also submit it.
    event.stopPropagation();
    setFormError(null);
    setIsSubmitting(true);

    try {
      await apiClient.post(`/api/v1/items/${itemId}/conversions`, {
        fromUnitId,
        conversionFactor: Number(factor),
      });
      setFromUnitId('');
      setFactor('');
      await load();
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.messageAr : 'تعذر إضافة وحدة التحويل');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeactivate(id: string) {
    await apiClient.post(`/api/v1/items/${itemId}/conversions/${id}/deactivate`);
    await load();
  }

  const availableUnits = units.filter(
    (unit) => unit.id !== baseUnitId && !conversions.some((conversion) => conversion.isActive && conversion.fromUnitId === unit.id),
  );

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border p-3">
      <p className="text-sm font-medium text-foreground">وحدات التحويل</p>

      {error ? <ErrorBanner message={error} onRetry={load} /> : null}
      {isLoading ? <LoadingSkeleton className="h-16 w-full" /> : null}

      {!isLoading && !error ? (
        conversions.filter((conversion) => conversion.isActive).length === 0 ? (
          <EmptyState message="لا توجد وحدات تحويل" description={`الوحدة الأساسية: ${unitName(baseUnitId)}`} />
        ) : (
          <ul className="flex flex-col gap-1.5">
            {conversions
              .filter((conversion) => conversion.isActive)
              .map((conversion) => (
                <li key={conversion.id} className="flex items-center justify-between rounded-md bg-muted px-3 py-1.5 text-sm">
                  <span>
                    1 {unitName(conversion.fromUnitId)} = {conversion.conversionFactor} {unitName(baseUnitId)}
                  </span>
                  <Button variant="ghost" size="icon-sm" onClick={() => handleDeactivate(conversion.id)} aria-label="حذف">
                    <Trash2 />
                  </Button>
                </li>
              ))}
          </ul>
        )
      ) : null}

      {formError ? <ErrorBanner message={formError} /> : null}

      {availableUnits.length > 0 ? (
        <form onSubmit={handleAdd} className="flex items-end gap-2">
          <div className="flex flex-1 flex-col gap-1.5">
            <Label htmlFor="conversion-unit">الوحدة</Label>
            <Select value={fromUnitId} onValueChange={setFromUnitId} required>
              <SelectTrigger id="conversion-unit" className="w-full">
                <SelectValue placeholder="اختر وحدة" />
              </SelectTrigger>
              <SelectContent>
                {availableUnits.map((unit) => (
                  <SelectItem key={unit.id} value={unit.id}>
                    {unit.nameArabic}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex w-28 flex-col gap-1.5">
            <Label htmlFor="conversion-factor">المعامل</Label>
            <Input
              id="conversion-factor"
              type="number"
              min="0.0001"
              step="any"
              dir="ltr"
              required
              value={factor}
              onChange={(event) => setFactor(event.target.value)}
            />
          </div>
          <Button type="submit" loading={isSubmitting} disabled={!fromUnitId}>
            إضافة
          </Button>
        </form>
      ) : null}
    </div>
  );
}
