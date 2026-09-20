"use client";

import { useState, type FormEvent } from "react";
import { Plus } from "lucide-react";
import { apiClient, ApiError } from "@/lib/api-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { ErrorBanner } from "@/components/feedback/error-banner";

interface CreatedEntity {
  id: string;
  nameArabic: string;
}

export interface QuickAddModalProps {
  /** e.g. "إضافة قسم جديد" - both the trigger button label and the dialog title. */
  label: string;
  basePath: string;
  secondaryField?: { key: string; label: string };
  onCreated: (entity: CreatedEntity) => void;
  /** 'field' renders the trigger as a full-width button that stands in for an empty select. */
  appearance?: "link" | "field";
  /** Dialog heading when it should differ from the trigger label. */
  dialogTitle?: string;
}

/**
 * Guide §5.3, task F3: the quick-add pattern used inside the Item form for creating a
 * Category or Unit without leaving it. On success, closes itself and hands the created
 * entity back to the caller, which selects it - zero page refresh, zero form-state loss
 * for the item form underneath.
 */
export function QuickAddModal({
  label,
  basePath,
  secondaryField,
  onCreated,
  appearance = "link",
  dialogTitle,
}: QuickAddModalProps) {
  const [open, setOpen] = useState(false);
  const [nameArabic, setNameArabic] = useState("");
  const [secondaryValue, setSecondaryValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    // React re-dispatches a portaled element's events by walking the REACT tree, not the DOM
    // tree - Radix renders this dialog's content into a document.body portal, but this form is
    // still a React descendant of the item form it is nested inside (guide §5.3's own pattern),
    // so without stopPropagation() this submit would ALSO fire the outer form's onSubmit with
    // whatever it currently holds (e.g. an empty baseUnitId, mid-selection).
    event.stopPropagation();
    setError(null);
    setIsSubmitting(true);

    try {
      const body: Record<string, string | null> = { nameArabic };
      if (secondaryField) {
        body[secondaryField.key] =
          secondaryValue.trim() === "" ? null : secondaryValue;
      }
      const created = await apiClient.post<CreatedEntity>(basePath, body);
      setOpen(false);
      setNameArabic("");
      setSecondaryValue("");
      onCreated(created);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.messageAr : "تعذر الحفظ");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {appearance === "field" ? (
          <Button
            type="button"
            variant="outline"
            className="w-full justify-between font-normal text-muted-foreground"
          >
            {label}
            <Plus className="size-4" />
          </Button>
        ) : (
          <Button type="button" variant="link" size="sm" className="h-auto p-0">
            <Plus className="size-3.5" />
            {label}
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{dialogTitle ?? label}</DialogTitle>
            <DialogDescription>
              سيُختار العنصر الجديد تلقائياً بعد الحفظ.
            </DialogDescription>
          </DialogHeader>

          {error ? <ErrorBanner message={error} /> : null}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="quick-add-name">
              الاسم{" "}
              <span aria-hidden="true" className="text-destructive">
                *
              </span>
            </Label>
            <Input
              id="quick-add-name"
              required
              autoFocus
              value={nameArabic}
              onChange={(event) => setNameArabic(event.target.value)}
            />
          </div>

          {secondaryField ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="quick-add-secondary">
                {secondaryField.label}
              </Label>
              <Input
                id="quick-add-secondary"
                value={secondaryValue}
                onChange={(event) => setSecondaryValue(event.target.value)}
              />
            </div>
          ) : null}

          <DialogFooter>
            <Button type="submit" loading={isSubmitting}>
              حفظ
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
