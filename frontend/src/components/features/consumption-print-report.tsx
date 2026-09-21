'use client';

import { formatQuantity } from '@/lib/formatters';

export interface ConsumptionReportRow {
  id: string;
  itemName: string | null;
  unitName: string | null;
  restaurantName: string | null;
  recordedByName: string | null;
  recordedAtLocal: string | null;
  quantity: number;
  notes: string | null;
}

export interface ConsumptionReport {
  period: { kind: 'Last24Hours' | 'Dates'; from: string | null; to: string | null; fromUtc: string; toUtc: string; timezoneId: string };
  generatedAtLocal: string;
  companyName: string;
  restaurantId: string | null;
  restaurantName: string | null;
  rows: ConsumptionReportRow[];
  totals: { itemId: string; itemName: string; unitId: string; unitName: string; quantity: number; recordCount: number }[];
  totalRecordCount: number;
  truncated: boolean;
}

/** 'yyyy-MM-dd' -> 'dd/MM/yyyy' by string, never through Date (no timezone shift). */
function isoToDisplay(iso: string): string {
  const [year, month, day] = iso.split('-');
  return `${day}/${month}/${year}`;
}

/** The period line, worded from what the SERVER actually applied (not from what the form currently shows). */
export function periodLabel(period: ConsumptionReport['period']): string {
  if (period.kind === 'Last24Hours' || !period.from || !period.to) {
    return 'الفترة: آخر 24 ساعة';
  }
  return period.from === period.to
    ? `التاريخ: ${isoToDisplay(period.from)}`
    : `الفترة: من ${isoToDisplay(period.from)} إلى ${isoToDisplay(period.to)}`;
}

/**
 * The printable consumption report. Rendered into a portal on <body> only while printing; the print stylesheet
 * (globals.css, `#consumption-print-root`) hides everything else - sidebar, header, filters and buttons never print.
 */
export function ConsumptionPrintReport({ report }: { report: ConsumptionReport }) {
  return (
    <div id="consumption-print-root" dir="rtl" className="hidden bg-white p-6 text-black print:block" data-testid="consumption-print-report">
      <header className="mb-4 border-b border-black pb-3">
        <p className="text-sm">{report.companyName}</p>
        <h1 className="text-2xl font-bold">سجل الاستهلاك</h1>
        <p className="mt-1 text-base font-medium" data-testid="print-period">{periodLabel(report.period)}</p>
        <p className="text-sm">
          الفرع: {report.restaurantName ?? 'كل الفروع'} · التوقيت: {report.period.timezoneId}
        </p>
        <p className="text-sm">تاريخ ووقت إنشاء التقرير: {report.generatedAtLocal}</p>
        <p className="text-sm">عدد السجلات: {report.totalRecordCount}</p>
      </header>

      {report.rows.length === 0 ? (
        <p className="py-6 text-center text-base">لا توجد سجلات استهلاك في هذه الفترة</p>
      ) : (
        <>
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="bg-gray-100">
                {['التاريخ والوقت', 'الصنف', 'الكمية المستهلكة', 'الفرع', 'المستخدم', 'ملاحظات'].map((heading) => (
                  <th key={heading} className="border border-black px-2 py-1 text-start">
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {report.rows.map((row) => (
                <tr key={row.id} className="break-inside-avoid">
                  <td className="border border-black px-2 py-1">{row.recordedAtLocal ?? '—'}</td>
                  <td className="border border-black px-2 py-1">{row.itemName ?? '—'}</td>
                  <td className="border border-black px-2 py-1">{formatQuantity(row.quantity, row.unitName ?? '')}</td>
                  <td className="border border-black px-2 py-1">{row.restaurantName ?? '—'}</td>
                  <td className="border border-black px-2 py-1">{row.recordedByName ?? '—'}</td>
                  <td className="border border-black px-2 py-1">{row.notes ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {report.truncated ? (
            <p className="mt-2 text-sm">
              ملاحظة: عُرضت أحدث {report.rows.length} سجل من أصل {report.totalRecordCount}؛ الإجماليات أدناه تشمل كل سجلات الفترة.
            </p>
          ) : null}

          <h2 className="mb-1 mt-5 text-lg font-bold">إجمالي الكمية المستهلكة</h2>
          <table className="w-full border-collapse text-sm" data-testid="print-totals">
            <thead>
              <tr className="bg-gray-100">
                {['الصنف', 'إجمالي الكمية', 'عدد السجلات'].map((heading) => (
                  <th key={heading} className="border border-black px-2 py-1 text-start">
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {report.totals.map((total) => (
                <tr key={`${total.itemId}-${total.unitId}`}>
                  <td className="border border-black px-2 py-1">{total.itemName}</td>
                  <td className="border border-black px-2 py-1">{formatQuantity(total.quantity, total.unitName)}</td>
                  <td className="border border-black px-2 py-1">{total.recordCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
