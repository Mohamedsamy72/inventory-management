"use client";

import { useEffect, useState, type FormEvent } from "react";
import type { CellContext, ColumnDef } from "@tanstack/react-table";
import { Plus, Pencil, Power, PowerOff, Search } from "lucide-react";
import { useSession } from "@/lib/auth/session-context";
import { useKeysetList } from "@/lib/use-keyset-list";
import { normalizeArabicSearch } from "@/lib/arabic-search";
import { apiClient, ApiError } from "@/lib/api-client";
import { DataTable } from "@/components/ui/data-table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { DeleteRecordButton } from "@/components/features/delete-record-button";
import { ErrorBanner } from "@/components/feedback/error-banner";
import { ForbiddenState } from "@/components/feedback/forbidden-state";
import { QuickAddModal } from "@/components/features/quick-add-modal";
import { ItemConversions } from "@/components/features/item-conversions";

interface ItemSummary {
  id: string;
  generatedCode: string;
  nameArabic: string;
  categoryId: string;
  baseUnitId: string;
  purchaseUnitId: string | null;
  defaultSupplierId: string | null;
  description: string | null;
  isActive: boolean;
}

interface NamedOption {
  id: string;
  nameArabic: string;
}

const NONE = "__none__";

/** Task F3 (guide §8.2 "`/items`", §5.3 quick-add pattern). */
export default function ItemsPage() {
  const { profile } = useSession();
  const [searchInput, setSearchInput] = useState("");
  const [query, setQuery] = useState("");
  const {
    items,
    isLoading,
    error,
    hasNextPage,
    hasPreviousPage,
    nextPage,
    previousPage,
    refetch,
  } = useKeysetList<ItemSummary>("/api/v1/items", { q: query });

  const [categories, setCategories] = useState<NamedOption[]>([]);
  const [units, setUnits] = useState<NamedOption[]>([]);
  const [suppliers, setSuppliers] = useState<NamedOption[]>([]);

  async function loadReferenceData() {
    // Names endpoints: available to anyone who may see items. Each list is independent, so one refused
    // call can never take the whole page down (an uncaught rejection surfaces as a runtime overlay).
    const [categoryList, unitList, supplierList] = await Promise.all([
      apiClient
        .get<NamedOption[]>("/api/v1/categories/names")
        .catch(() => [] as NamedOption[]),
      apiClient
        .get<NamedOption[]>("/api/v1/units/names")
        .catch(() => [] as NamedOption[]),
      apiClient
        .get<NamedOption[]>("/api/v1/suppliers/names")
        .catch(() => [] as NamedOption[]),
    ]);
    setCategories(categoryList);
    setUnits(unitList);
    setSuppliers(supplierList);
  }

  useEffect(() => {
    void loadReferenceData();
  }, []);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ItemSummary | null>(null);
  const [nameArabic, setNameArabic] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [baseUnitId, setBaseUnitId] = useState("");
  const [purchaseUnitId, setPurchaseUnitId] = useState(NONE);
  const [defaultSupplierId, setDefaultSupplierId] = useState(NONE);
  const [description, setDescription] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);

  if (profile && !profile.permissionCodes.includes("items:view")) {
    return <ForbiddenState reason="forbidden" />;
  }

  const canCreate = profile?.permissionCodes.includes("items:create") ?? false;
  const canUpdate = profile?.permissionCodes.includes("items:update") ?? false;
  const canDelete = profile?.permissionCodes.includes("items:delete") ?? false;

  function handleSearch(event: FormEvent) {
    event.preventDefault();
    setQuery(normalizeArabicSearch(searchInput));
  }

  function openCreate() {
    setEditing(null);
    setNameArabic("");
    setCategoryId("");
    setBaseUnitId("");
    setPurchaseUnitId(NONE);
    setDefaultSupplierId(NONE);
    setDescription("");
    setFormError(null);
    setDialogOpen(true);
  }

  function openEdit(row: ItemSummary) {
    setEditing(row);
    setNameArabic(row.nameArabic);
    setCategoryId(row.categoryId);
    setBaseUnitId(row.baseUnitId);
    setPurchaseUnitId(row.purchaseUnitId ?? NONE);
    setDefaultSupplierId(row.defaultSupplierId ?? NONE);
    setDescription(row.description ?? "");
    setFormError(null);
    setDialogOpen(true);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setIsSubmitting(true);

    try {
      if (editing) {
        await apiClient.put(`/api/v1/items/${editing.id}`, {
          nameArabic,
          categoryId,
          purchaseUnitId: purchaseUnitId === NONE ? null : purchaseUnitId,
          defaultSupplierId:
            defaultSupplierId === NONE ? null : defaultSupplierId,
          description: description || null,
        });
      } else {
        await apiClient.post("/api/v1/items", {
          nameArabic,
          categoryId,
          baseUnitId,
          purchaseUnitId: purchaseUnitId === NONE ? null : purchaseUnitId,
          defaultSupplierId:
            defaultSupplierId === NONE ? null : defaultSupplierId,
          description: description || null,
        });
        setDialogOpen(false);
      }
      refetch();
    } catch (caught) {
      setFormError(
        caught instanceof ApiError ? caught.messageAr : "تعذر حفظ الصنف",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function toggleActive(row: ItemSummary) {
    setTogglingId(row.id);
    try {
      await apiClient.post(
        `/api/v1/items/${row.id}/${row.isActive ? "deactivate" : "reactivate"}`,
      );
      refetch();
    } finally {
      setTogglingId(null);
    }
  }

  function categoryName(id: string): string {
    return categories.find((category) => category.id === id)?.nameArabic ?? "—";
  }

  const columns: ColumnDef<ItemSummary, unknown>[] = [
    {
      id: "actions",
      header: "",
      cell: ({ row }: CellContext<ItemSummary, unknown>) => (
        <div className="flex items-center gap-1">
          {canUpdate ? (
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={() => openEdit(row.original)}
              aria-label="تعديل"
            >
              <Pencil />
            </Button>
          ) : null}
          {canDelete ? (
            <>
              <Button
                variant="ghost"
                size="icon-sm"
                loading={togglingId === row.original.id}
                onClick={() => toggleActive(row.original)}
                aria-label={
                  row.original.isActive ? "إلغاء التفعيل" : "إعادة التفعيل"
                }
              >
                {row.original.isActive ? <PowerOff /> : <Power />}
              </Button>
              <DeleteRecordButton
                path={`/api/v1/items/${row.original.id}`}
                name={row.original.nameArabic}
                onDeleted={refetch}
              />
            </>
          ) : null}
        </div>
      ),
    },
    {
      id: "status",
      header: "الحالة",
      cell: ({ row }: CellContext<ItemSummary, unknown>) => (
        <Badge variant={row.original.isActive ? "default" : "secondary"}>
          {row.original.isActive ? "نشط" : "غير نشط"}
        </Badge>
      ),
    },
    {
      id: "category",
      header: "القسم",
      cell: ({ row }: CellContext<ItemSummary, unknown>) =>
        categoryName(row.original.categoryId),
    },
    {
      id: "nameArabic",
      header: "الاسم",
      cell: ({ row }: CellContext<ItemSummary, unknown>) =>
        row.original.nameArabic,
    },
    {
      id: "code",
      header: "الكود",
      cell: ({ row }: CellContext<ItemSummary, unknown>) => (
        <span dir="ltr">{row.original.generatedCode}</span>
      ),
    },
  ].reverse();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-heading text-xl font-medium text-foreground">
          الأصناف
        </h1>
        {canCreate ? (
          <Button onClick={openCreate}>
            <Plus />
            إضافة صنف
          </Button>
        ) : null}
      </div>

      <form
        onSubmit={handleSearch}
        className="flex max-w-sm items-center gap-2"
      >
        <Input
          type="search"
          placeholder="ابحث بالاسم..."
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
        />
        <Button type="submit" variant="outline" size="icon" aria-label="بحث">
          <Search />
        </Button>
      </form>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        error={error ?? undefined}
        onRetry={refetch}
        emptyMessage="لا توجد أصناف مطابقة"
        onNextPage={nextPage}
        onPreviousPage={previousPage}
        hasNextPage={hasNextPage}
        hasPreviousPage={hasPreviousPage}
        mobileCard={(row) => (
          <div className="flex items-center justify-between gap-2">
            <div>
              <p className="font-medium text-foreground">{row.nameArabic}</p>
              <p className="text-xs text-muted-foreground" dir="ltr">
                {row.generatedCode}
              </p>
              <Badge
                variant={row.isActive ? "default" : "secondary"}
                className="mt-1"
              >
                {row.isActive ? "نشط" : "غير نشط"}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              {canUpdate ? (
                <Button
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => openEdit(row)}
                  aria-label="تعديل"
                >
                  <Pencil />
                </Button>
              ) : null}
              {canDelete ? (
                <>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    loading={togglingId === row.id}
                    onClick={() => toggleActive(row)}
                    aria-label={
                      row.isActive ? "إلغاء التفعيل" : "إعادة التفعيل"
                    }
                  >
                    {row.isActive ? <PowerOff /> : <Power />}
                  </Button>
                  <DeleteRecordButton
                    path={`/api/v1/items/${row.id}`}
                    name={row.nameArabic}
                    onDeleted={refetch}
                  />
                </>
              ) : null}
            </div>
          </div>
        )}
      />

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <form
            onSubmit={handleSubmit}
            className="flex max-h-[75vh] flex-col gap-4 overflow-y-auto"
          >
            <DialogHeader>
              <DialogTitle>
                {editing ? "تعديل الصنف" : "إضافة صنف جديد"}
              </DialogTitle>
              <DialogDescription>
                أدخل بيانات الصنف ثم اضغط حفظ.
              </DialogDescription>
            </DialogHeader>

            {formError ? <ErrorBanner message={formError} /> : null}

            {editing ? (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="item-code">الكود</Label>
                {/* Read-only, server-generated - task 5.10/F3's own explicit rule
                    (docs/28 §5.1: the client never sends or edits `generatedCode`). */}
                <Input
                  id="item-code"
                  dir="ltr"
                  value={editing.generatedCode}
                  disabled
                />
              </div>
            ) : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="item-name">
                الاسم{" "}
                <span aria-hidden="true" className="text-destructive">
                  *
                </span>
              </Label>
              <Input
                id="item-name"
                required
                value={nameArabic}
                onChange={(event) => setNameArabic(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <div className="flex items-center justify-between">
                <Label htmlFor="item-category">
                  القسم{" "}
                  <span aria-hidden="true" className="text-destructive">
                    *
                  </span>
                </Label>
                <QuickAddModal
                  label="إضافة قسم جديد"
                  basePath="/api/v1/categories"
                  onCreated={(created) => {
                    setCategories((current) => [...current, created]);
                    setCategoryId(created.id);
                  }}
                />
              </div>
              {/* onValueChange guarded against "" - Radix Select's hidden native <select> fires a
                  stray change event with an empty value when a quick-added <option> mounts into
                  an already-open SelectContent, which would otherwise clobber a value this same
                  render cycle just set programmatically. No real SelectItem here ever has "" as
                  its value, so the guard only ever rejects that spurious reset. */}
              <Select
                value={categoryId}
                onValueChange={(value) => value && setCategoryId(value)}
                required
              >
                <SelectTrigger id="item-category" className="w-full">
                  <SelectValue placeholder="اختر قسماً" />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {!editing ? (
              <div className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between">
                  <Label htmlFor="item-base-unit">
                    الوحدة الأساسية{" "}
                    <span aria-hidden="true" className="text-destructive">
                      *
                    </span>
                  </Label>
                  <QuickAddModal
                    label="إضافة وحدة جديدة"
                    basePath="/api/v1/units"
                    secondaryField={{ key: "abbreviation", label: "الاختصار" }}
                    onCreated={(created) => {
                      setUnits((current) => [...current, created]);
                      setBaseUnitId(created.id);
                    }}
                  />
                </div>
                {units.length === 0 ? (
                  <QuickAddModal
                    appearance="field"
                    dialogTitle="إضافة وحدة جديدة"
                    label="لا توجد وحدات - اضغط لإضافة وحدة"
                    basePath="/api/v1/units"
                    secondaryField={{ key: "abbreviation", label: "الاختصار" }}
                    onCreated={(created) => {
                      setUnits((current) => [...current, created]);
                      setBaseUnitId(created.id);
                    }}
                  />
                ) : (
                  <Select
                    value={baseUnitId}
                    onValueChange={(value) => value && setBaseUnitId(value)}
                    required
                  >
                    <SelectTrigger id="item-base-unit" className="w-full">
                      <SelectValue placeholder="اختر وحدة" />
                    </SelectTrigger>
                    <SelectContent>
                      {units.length === 0 ? (
                        <p className="px-2 py-1.5 text-sm text-muted-foreground">
                          لا توجد وحدات بعد - استخدم «إضافة وحدة جديدة» أعلاه.
                        </p>
                      ) : null}
                      {units.map((unit) => (
                        <SelectItem key={unit.id} value={unit.id}>
                          {unit.nameArabic}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            ) : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="item-purchase-unit">وحدة الشراء</Label>
              <Select value={purchaseUnitId} onValueChange={setPurchaseUnitId}>
                <SelectTrigger id="item-purchase-unit" className="w-full">
                  <SelectValue placeholder="بدون" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>بدون</SelectItem>
                  {units.map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="item-supplier">المورد الافتراضي</Label>
              <Select
                value={defaultSupplierId}
                onValueChange={setDefaultSupplierId}
              >
                <SelectTrigger id="item-supplier" className="w-full">
                  <SelectValue placeholder="بدون" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>بدون</SelectItem>
                  {suppliers.map((supplier) => (
                    <SelectItem key={supplier.id} value={supplier.id}>
                      {supplier.nameArabic}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="item-description">الوصف</Label>
              <Input
                id="item-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>

            {editing ? (
              <ItemConversions
                itemId={editing.id}
                baseUnitId={editing.baseUnitId}
                units={units}
              />
            ) : null}

            <DialogFooter>
              <Button
                type="submit"
                loading={isSubmitting}
                disabled={!categoryId || (!editing && !baseUnitId)}
              >
                حفظ
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
