'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { EmptyState } from '@/components/feedback/empty-state';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatCurrency } from '@/lib/formatters';
import { useSession } from '@/lib/auth/session-context';

interface NamedOption {
  id: string;
  nameArabic: string;
}

interface WarehouseStockLine {
  itemId: string;
  balance: number;
  averageUnitCost: number | null;
}

interface WarehouseSummaryCard {
  id: string;
  nameArabic: string;
  totalItems: number;
  totalValue: number;
}

/**
 * Stock overview for the dashboard: one card per warehouse with its
 * total item count and total inventory value, clicking through to the existing item-level
 * balance/cost table (`/locations/{id}/stock`, Task F4). Only ever mounted from the Owner
 * dashboard branch - never Admin's - because `costs:view`/`valuation:view` are Owner-exclusive
 * (ADR-012, "Admin must not access financial/cost data"). No new backend endpoint was needed:
 * this aggregates the existing `/warehouses/{id}/stock` endpoint client-side per warehouse, and
 * that endpoint already masks `averageUnitCost` to null server-side for anyone without
 * `costs:view` (`IFinancialProjection`) - this component does not perform that masking itself.
 */
export function WarehouseInventoryCards() {
  const router = useRouter();
  const { profile } = useSession();
  // Quantities are shown to anyone who may view stock (receiving:view). The money value is shown only to
  // an account that holds costs:view - the server already returns null costs to everyone else.
  const showValue = profile?.permissionCodes.includes('costs:view') ?? false;
  const [cards, setCards] = useState<WarehouseSummaryCard[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    setCards(null);
    try {
      const warehouses = await apiClient.get<NamedOption[]>('/api/v1/warehouses/names');
      const summaries = await Promise.all(
        warehouses.map(async (warehouse) => {
          const lines = await apiClient.get<WarehouseStockLine[]>(`/api/v1/warehouses/${warehouse.id}/stock`);
          const inStock = lines.filter((line) => line.balance > 0);
          const totalValue = inStock.reduce((sum, line) => sum + line.balance * (line.averageUnitCost ?? 0), 0);
          return { id: warehouse.id, nameArabic: warehouse.nameArabic, totalItems: inStock.length, totalValue };
        }),
      );
      setCards(summaries);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل بيانات المخازن');
    }
  }

  useEffect(() => {
    void load();
  }, []);

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  if (cards === null) {
    return <LoadingSkeleton className="h-24 w-full" />;
  }

  if (cards.length === 0) {
    return <EmptyState message="لا توجد مخازن مسجلة" />;
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {cards.map((card) => (
        <Card
          key={card.id}
          role="button"
          tabIndex={0}
          onClick={() => router.push(`/locations/${card.id}/stock`)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              router.push(`/locations/${card.id}/stock`);
            }
          }}
          className="cursor-pointer transition-colors hover:bg-muted/50"
        >
          <CardHeader>
            <h3 className="font-heading text-base font-medium text-foreground">{card.nameArabic}</h3>
          </CardHeader>
          <CardContent className="flex flex-col gap-1">
            <p className="text-sm text-muted-foreground">
              إجمالي الأصناف: <span className="font-medium text-foreground">{card.totalItems}</span>
            </p>
            {showValue ? (
              <p className="text-sm text-muted-foreground">
                القيمة الإجمالية: <span className="font-medium text-foreground">{formatCurrency(card.totalValue)}</span>
              </p>
            ) : null}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
