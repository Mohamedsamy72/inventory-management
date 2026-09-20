'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatCurrency, formatQuantity } from '@/lib/formatters';
import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
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
  const canSetStock = profile?.permissionCodes.includes('stock:direct_set') ?? false;

  const [editing, setEditing] = useState<{ itemId: string; current: number } | null>(null);
  const [newQuantity, setNewQuantity] = useState('');
  const [reason, setReason] = useState('');
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

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
      // Reference data is best-effort: a viewer without items/units access still sees the balances.
      const [stock, itemPage, warehouseNames, unitNames] = await Promise.all([
        apiClient.get<WarehouseStockLine[]>(`/api/v1/warehouses/${params.id}/stock`),
        apiClient.get<{ items: (NamedOption & { baseUnitId: string })[] }>('/api/v1/items?limit=200').catch(() => ({ items: [] as (NamedOption & { baseUnitId: string })[] })),
        apiClient.get<NamedOption[]>('/api/v1/warehouses/names').catch(() => [] as NamedOption[]),
        apiClient.get<NamedOption[]>('/api/v1/units/names').catch(() => [] as NamedOption[]),
      ]);
      setLines(stock);
      setItems(itemPage.items);
      setWarehouseName(warehouseNames.find((w) => w.id === params.id)?.nameArabic ?? '');

      const unitById = Object.fromEntries(unitNames.map((unit) => [unit.id, unit.nameArabic]));
      setUnits(Object.fromEntries(itemPage.items.map((item) => [item.id, unitById[item.baseUnitId] ?? ''])));
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

  // Every item is listed (an item with no balance yet is simply 0), so any item's stock can be set directly.
  const rows: WarehouseStockLine[] = [
    ...items.map((item) => lines.find((line) => line.itemId === item.id) ?? { itemId: item.id, balance: 0, inTransit: 0, available: 0, averageUnitCost: null }),
    ...lines.filter((line) => !items.some((item) => item.id === line.itemId)),
  ];

  function openEdit(line: WarehouseStockLine) {
    setEditing({ itemId: line.itemId, current: line.balance });
    setNewQuantity(String(line.balance));
    setReason('');
    setSaveError(null);
  }

  async function saveStock() {
    if (!editing) return;
    const quantity = Number(newQuantity);
    if (newQuantity.trim() === '' || Number.isNaN(quantity) || quantity < 0) {
      setSaveError('أدخل كمية صحيحة (صفر أو أكثر)');
      return;
    }
    setIsSaving(true);
    setSaveError(null);
    try {
      await apiClient.put(`/api/v1/warehouses/${params.id}/stock/${editing.itemId}`, { quantity, reason: reason.trim() === '' ? null : reason.trim() });
      setEditing(null);
      await load();
    } catch (caught) {
      setSaveError(caught instanceof ApiError ? caught.messageAr : 'تعذر حفظ الرصيد');
    } finally {
      setIsSaving(false);
    }
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
        <TableLoadingSkeleton columns={(isOwner ? 5 : 4) + (canSetStock ? 1 : 0)} />
      ) : rows.length === 0 ? (
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
                {canSetStock ? <TableHead>تعديل الرصيد</TableHead> : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((line) => (
                <TableRow key={line.itemId}>
                  {isOwner ? <TableCell>{line.averageUnitCost !== null ? formatCurrency(line.averageUnitCost) : '—'}</TableCell> : null}
                  <TableCell>{formatQuantity(line.available, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{formatQuantity(line.inTransit, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{formatQuantity(line.balance, units[line.itemId] ?? '')}</TableCell>
                  <TableCell>{itemName(line.itemId)}</TableCell>
                  {canSetStock ? (
                    <TableCell>
                      <Button variant="ghost" size="icon-sm" onClick={() => openEdit(line)} aria-label={`تعديل رصيد ${itemName(line.itemId)}`}>
                        <Pencil />
                      </Button>
                    </TableCell>
                  ) : null}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
      <Dialog open={editing !== null} onOpenChange={(open) => (open ? undefined : setEditing(null))}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>تعديل الرصيد مباشرة</DialogTitle>
            <DialogDescription>
              {editing ? `${itemName(editing.itemId)} - الرصيد الحالي: ${formatQuantity(editing.current, units[editing.itemId] ?? '')}. ` : ''}
              سيصبح الرصيد المدخل هو الرصيد الفعلي بالوحدة الأساسية، ويُسجَّل الفرق في سجل الحركات مع اسمك.
            </DialogDescription>
          </DialogHeader>
          {saveError ? <ErrorBanner message={saveError} /> : null}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="stock-new-quantity">الرصيد الجديد</Label>
            <Input id="stock-new-quantity" type="number" inputMode="decimal" min={0} step="any" dir="ltr" value={newQuantity} onChange={(event) => setNewQuantity(event.target.value)} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="stock-reason">السبب (اختياري)</Label>
            <Input id="stock-reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>
              إلغاء
            </Button>
            <Button loading={isSaving} onClick={() => void saveStock()}>
              تأكيد التعديل
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
