'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSession } from '@/lib/auth/session-context';
import { apiClient, ApiError } from '@/lib/api-client';
import { formatCurrency, formatQuantity } from '@/lib/formatters';
import { Pencil, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { QuickAddModal } from '@/components/features/quick-add-modal';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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

  const canAddItem = canSetStock && (profile?.permissionCodes.includes('items:create') ?? false);

  const [categoryOptions, setCategoryOptions] = useState<NamedOption[]>([]);
  const [unitOptions, setUnitOptions] = useState<NamedOption[]>([]);
  const [addOpen, setAddOpen] = useState(false);
  const [addName, setAddName] = useState('');
  const [addCategoryId, setAddCategoryId] = useState('');
  const [addUnitId, setAddUnitId] = useState('');
  const [addQuantity, setAddQuantity] = useState('0');
  const [addUnitCost, setAddUnitCost] = useState('');
  // Entering a unit cost is a financial input - offered only to accounts that may see costs.
  const canEnterCost = profile?.permissionCodes.includes('costs:view') ?? false;
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);

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
      const [stock, itemPage, warehouseNames, unitNames, categoryNames] = await Promise.all([
        apiClient.get<WarehouseStockLine[]>(`/api/v1/warehouses/${params.id}/stock`),
        apiClient.get<{ items: (NamedOption & { baseUnitId: string })[] }>('/api/v1/items?limit=200').catch(() => ({ items: [] as (NamedOption & { baseUnitId: string })[] })),
        apiClient.get<NamedOption[]>('/api/v1/warehouses/names').catch(() => [] as NamedOption[]),
        apiClient.get<NamedOption[]>('/api/v1/units/names').catch(() => [] as NamedOption[]),
        apiClient.get<NamedOption[]>('/api/v1/categories/names').catch(() => [] as NamedOption[]),
      ]);
      setUnitOptions(unitNames);
      setCategoryOptions(categoryNames);
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

  function openAdd() {
    setAddName('');
    setAddCategoryId('');
    setAddUnitId('');
    setAddQuantity('0');
    setAddUnitCost('');
    setAddError(null);
    setAddOpen(true);
  }

  async function addItemWithStock() {
    const quantity = Number(addQuantity);
    if (addName.trim() === '' || addCategoryId === '' || addUnitId === '') {
      setAddError('أدخل اسم الصنف واختر القسم والوحدة الأساسية');
      return;
    }
    if (addQuantity.trim() === '' || Number.isNaN(quantity) || quantity < 0) {
      setAddError('أدخل رصيداً صحيحاً (صفر أو أكثر)');
      return;
    }
    const unitCost = addUnitCost.trim() === '' ? null : Number(addUnitCost);
    if (canEnterCost && quantity > 0 && (unitCost === null || Number.isNaN(unitCost) || unitCost < 0)) {
      setAddError('أدخل تكلفة الوحدة (صفر أو أكثر)');
      return;
    }
    setIsAdding(true);
    setAddError(null);
    let createdItemId: string | null = null;
    try {
      const created = await apiClient.post<{ id: string }>('/api/v1/items', {
        nameArabic: addName.trim(),
        categoryId: addCategoryId,
        baseUnitId: addUnitId,
        purchaseUnitId: null,
        defaultSupplierId: null,
        description: null,
      });
      createdItemId = created.id;
      if (quantity > 0) {
        await apiClient.put(`/api/v1/warehouses/${params.id}/stock/${created.id}`, { quantity, unitCost: canEnterCost ? unitCost : null, reason: 'رصيد افتتاحي عند إضافة الصنف' });
      }
      setAddOpen(false);
      await load();
    } catch (caught) {
      const message = caught instanceof ApiError ? caught.messageAr : 'تعذر الحفظ';
      if (createdItemId) {
        // The item exists; only the opening balance failed - it can be set from the row's pencil.
        setAddOpen(false);
        await load();
        setError(`تم إنشاء الصنف لكن تعذر ضبط رصيده: ${message} يمكنك ضبطه من زر التعديل بجانب الصنف.`);
      } else {
        setAddError(message);
      }
    } finally {
      setIsAdding(false);
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
        <div className="flex items-center gap-2">
          {canAddItem ? (
            <Button onClick={openAdd}>
              <Plus />
              إضافة صنف بالرصيد
            </Button>
          ) : null}
          <Button variant="ghost" onClick={() => router.push('/locations')}>
            رجوع للمخازن والفروع
          </Button>
        </div>
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
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>إضافة صنف جديد بالرصيد</DialogTitle>
            <DialogDescription>ينشئ الصنف ويضبط رصيده الافتتاحي في هذا المخزن مباشرة، ويُسجَّل ذلك في سجل الحركات.</DialogDescription>
          </DialogHeader>
          {addError ? <ErrorBanner message={addError} /> : null}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="add-item-name">اسم الصنف</Label>
            <Input id="add-item-name" value={addName} onChange={(event) => setAddName(event.target.value)} />
          </div>
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="add-item-category">القسم</Label>
              <QuickAddModal
                label="إضافة قسم جديد"
                basePath="/api/v1/categories"
                onCreated={(created) => {
                  setCategoryOptions((current) => [...current, created]);
                  setAddCategoryId(created.id);
                }}
              />
            </div>
            {categoryOptions.length === 0 ? (
              <p className="text-sm text-muted-foreground">لا توجد أقسام - استخدم «إضافة قسم جديد».</p>
            ) : (
              <Select value={addCategoryId} onValueChange={(value) => value && setAddCategoryId(value)}>
                <SelectTrigger id="add-item-category" className="w-full">
                  <SelectValue placeholder="اختر قسماً" />
                </SelectTrigger>
                <SelectContent>
                  {categoryOptions.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="add-item-unit">الوحدة الأساسية</Label>
              <QuickAddModal
                label="إضافة وحدة جديدة"
                basePath="/api/v1/units"
                secondaryField={{ key: 'abbreviation', label: 'الاختصار' }}
                onCreated={(created) => {
                  setUnitOptions((current) => [...current, created]);
                  setAddUnitId(created.id);
                }}
              />
            </div>
            {unitOptions.length === 0 ? (
              <p className="text-sm text-muted-foreground">لا توجد وحدات - استخدم «إضافة وحدة جديدة».</p>
            ) : (
              <Select value={addUnitId} onValueChange={(value) => value && setAddUnitId(value)}>
                <SelectTrigger id="add-item-unit" className="w-full">
                  <SelectValue placeholder="اختر وحدة" />
                </SelectTrigger>
                <SelectContent>
                  {unitOptions.map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="add-item-quantity">الرصيد الافتتاحي في هذا المخزن</Label>
            <Input id="add-item-quantity" type="number" inputMode="decimal" min={0} step="any" dir="ltr" value={addQuantity} onChange={(event) => setAddQuantity(event.target.value)} />
          </div>
          {canEnterCost ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="add-item-cost">تكلفة الوحدة (ج.م)</Label>
              <Input id="add-item-cost" type="number" inputMode="decimal" min={0} step="any" dir="ltr" value={addUnitCost} onChange={(event) => setAddUnitCost(event.target.value)} />
            </div>
          ) : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              إلغاء
            </Button>
            <Button loading={isAdding} onClick={() => void addItemWithStock()}>
              حفظ الصنف والرصيد
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
