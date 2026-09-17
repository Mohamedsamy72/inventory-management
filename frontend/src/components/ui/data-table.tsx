'use client';

import {
  type ColumnDef,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { TableLoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { EmptyState } from '@/components/feedback/empty-state';
import { ErrorBanner } from '@/components/feedback/error-banner';
import { ChevronRight, ChevronLeft } from 'lucide-react';

export interface DataTableProps<TData> {
  columns: ColumnDef<TData, unknown>[];
  data: TData[];
  /** One of guide section 6's 5 mandatory states - undefined/false means "success". */
  isLoading?: boolean;
  error?: string;
  onRetry?: () => void;
  emptyMessage?: string;
  /** Keyset pagination (task F1.10) - not offset-based; there is no page number. */
  onNextPage?: () => void;
  onPreviousPage?: () => void;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  /** Renders each row as a card below this breakpoint (guide section 5.4). */
  mobileCard?: (row: TData) => React.ReactNode;
}

/**
 * Task F1.10. Sticky header, keyset pagination controls, and a `< 768px` card collapse.
 * Composes the shadcn `Table` primitive with TanStack Table for column definitions -
 * the established shadcn convention (`stacks/shadcn.csv`: "DataTable pattern for
 * sorting filtering pagination"), rather than a hand-rolled table implementation.
 */
export function DataTable<TData>({
  columns,
  data,
  isLoading,
  error,
  onRetry,
  emptyMessage = 'لا توجد سجلات مطابقة',
  onNextPage,
  onPreviousPage,
  hasNextPage,
  hasPreviousPage,
  mobileCard,
}: DataTableProps<TData>) {
  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  if (error) {
    return <ErrorBanner message={error} onRetry={onRetry} />;
  }

  if (isLoading) {
    return <TableLoadingSkeleton columns={columns.length} />;
  }

  if (data.length === 0) {
    return <EmptyState message={emptyMessage} />;
  }

  return (
    <div className="flex flex-col gap-3">
      {/* Desktop: table layout (guide section 5.4, docs/31 4.3 point 4 - the first
          logical column sits at the right edge, which the browser already does under
          dir="rtl" without any extra markup). */}
      <div className="hidden overflow-auto rounded-lg border border-border md:block">
        <Table>
          <TableHeader className="sticky top-0 z-10 bg-card">
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id}>
                    {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows.map((row) => (
              <TableRow key={row.id}>
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Mobile (< 768px): stacked cards, no horizontal scroll (guide section 5.4). */}
      <div className="flex flex-col gap-3 md:hidden">
        {data.map((row, index) => (
          <div key={index} className="rounded-lg border border-border bg-card p-4">
            {mobileCard ? mobileCard(row) : null}
          </div>
        ))}
      </div>

      {(onNextPage || onPreviousPage) && (
        <div className="flex items-center justify-center gap-2">
          <Button variant="outline" size="sm" onClick={onNextPage} disabled={!hasNextPage}>
            <ChevronLeft />
            التالي
          </Button>
          <Button variant="outline" size="sm" onClick={onPreviousPage} disabled={!hasPreviousPage}>
            السابق
            <ChevronRight />
          </Button>
        </div>
      )}
    </div>
  );
}
