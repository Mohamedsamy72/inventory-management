'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatCurrency, formatQuantity } from '@/lib/formatters';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ForbiddenState } from '@/components/feedback/forbidden-state';
import { EmptyState } from '@/components/feedback/empty-state';
import { TableLoadingSkeleton } from '@/components/feedback/loading-skeleton';

interface WarehouseStockLine {
  itemId: string;
  balance: number;
  inTransit: number;
  available: number;
  averageUnitCost: number | null;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

/** Task F4 (guide §8.2 "`/locations/{id}/stock`"): balance / in-transit / available per item,
 * cost column Owner-only (backend already masks `averageUnitCost` to null for non-Owner via
 * `IFinancialProjection` - this only additionally omits the COLUMN for a cleaner non-Owner view). */
export default function WarehouseStockPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { profile } = useSession();
  const isOwner = profile?.role === 'Owner';

  const [lines, setLines] = useState<WarehouseStockLine[]>([]);
  const [items, setItems] = useState<NamedOption[]>([]);
  const [units, setUnits] = useState<Record<string, string>>({});
  const [warehouseName, setWarehouseName] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const [stock, itemPage, warehouse] = await Promise.all([
        apiClient.get<WarehouseStockLine[]>(`/api/v1/warehouses/${params.id}/stock`),
        apiClient.get<{ items: (NamedOption & { baseUnitId: string })[] }>('/api/v1/items?limit=200'),
        apiClient.get<{ nameArabic: string }>(`/api/v1/warehouses/${params.id}`),
      ]);
      setLines(stock);
      setItems(itemPage.items);
      setWarehouseName(warehouse.nameArabic);

      const unitPage = await apiClient.get<{ items: NamedOption[] }>('/api/v1/units?limit=100');
      const unitById = Object.fromEntries(unitPage.items.map((unit) => [unit.id, unit.nameArabic]));
      const itemUnit = Object.fromEntries(itemPage.items.map((item) => [item.id, unitById[item.baseUnitId] ?? '']));
      setUnits(itemUnit);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل رصيد المخزن');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [params.id]);

  if (profile && !profile.permissionCodes.includes('receiving:view')) {
    return <ForbiddenState reason="forbidden" />;
  }

  function itemName(id: string): string {
    return items.find((item) => item.id === id)?.nameArabic ?? '—';
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={load} />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">رصيد المخزن: {warehouseName}</h1>
        <Button variant="ghost" onClick={() => router.push('/locations')}>
          رجوع للمخازن والفروع
        </Button>
      </div>

      {isLoading ? (
        <TableLoadingSkeleton columns={isOwner ? 5 : 4} />
      ) : lines.length === 0 ? (
        <EmptyState message="لا يوجد رصيد مسجل لهذا المخزن" />
      ) : (
        <div className="overflow-auto rounded-lg border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                {isOwner ? <TableHead>متوسط تكلفة الوحدة</TableHead> : null}
                <TableHead>المتاح</TableHead>
                <TableHead>في الطريق</TableHead>
                <TableHead>الرصيد</TableHead>
                <TableHead>الصنف</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {lines.map((line) => (
                <TableRow key={line.itemId}>
                  {isOwner ? <TableCell>{line.averageUnitCost !== null ? formatCurrency(line.averageUnitCost) : '—'}</TableCell> : null}
                  <TableCell>{formatQuantity(line.available, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{formatQuantity(line.inTransit, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{formatQuantity(line.balance, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{itemName(line.itemId)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
