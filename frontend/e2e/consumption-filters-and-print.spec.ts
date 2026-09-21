import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
const PSQL = process.env.E2E_PSQL ?? 'C:/pg-inventory-system/pgsql/bin/psql.exe';

// A dedicated test tenant/Owner (created for browser tests only) - not any real account.
const OWNER = { mobileNumber: '01055500001', password: 'E2e!Owner1234' };

async function csrf(request: APIRequestContext): Promise<string> {
  const response = await request.get(`${API_BASE}/api/v1/auth/csrf-token`);
  return ((await response.json()) as { csrfToken: string }).csrfToken;
}

async function post<T>(request: APIRequestContext, path: string, data: unknown): Promise<T> {
  const response = await request.post(`${API_BASE}${path}`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data });
  expect(response.ok(), `${path} -> ${response.status()}`).toBeTruthy();
  return (await response.json()) as T;
}

/** yyyy-MM-dd of an instant in the company timezone (Africa/Cairo), matching the server's day boundaries. */
function cairoDate(instant: Date): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(instant);
}

function display(iso: string): string {
  const [y, m, d] = iso.split('-');
  return `${d}/${m}/${y}`;
}

function sql(statement: string): void {
  execFileSync(PSQL, ['-h', 'localhost', '-p', '5433', '-U', 'postgres', '-d', 'restaurant_inventory', '-c', statement], {
    env: { ...process.env, PGPASSWORD: 'postgres' },
  });
}

async function chooseMode(page: Page, label: string) {
  await page.locator('#consumption-filter-mode').click();
  await page.getByRole('option', { name: label }).click();
}

test('consumption log: default 24h, single date, date range, and printing the filtered report', async ({ browser }) => {
  const context = await browser.newContext();
  await context.addInitScript(() => {
    // The print dialog itself is not automatable; count the calls instead.
    (window as unknown as { __printCalls: number }).__printCalls = 0;
    window.print = () => {
      (window as unknown as { __printCalls: number }).__printCalls += 1;
    };
  });
  const request = context.request;
  const login = await request.post(`${API_BASE}/api/v1/auth/login`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data: OWNER });
  expect(login.ok()).toBeTruthy();

  // ---- fixtures through the real API: one record made now, one back-dated 10 days ----
  const stamp = String(Date.now()).slice(-6);
  const restaurant = await post<{ id: string }>(request, '/api/v1/restaurants', { nameArabic: `فرع ${stamp}`, code: `BR-${stamp}`, address: null, description: null });
  const category = await post<{ id: string }>(request, '/api/v1/categories', { nameArabic: `قسم ${stamp}`, description: null });
  const unit = await post<{ id: string }>(request, '/api/v1/units', { nameArabic: `كيلو${stamp}`, abbreviation: null });
  const recentItem = await post<{ id: string }>(request, '/api/v1/items', { nameArabic: `صنف حديث ${stamp}`, categoryId: category.id, baseUnitId: unit.id, purchaseUnitId: null, defaultSupplierId: null, description: null });
  const oldItem = await post<{ id: string }>(request, '/api/v1/items', { nameArabic: `صنف قديم ${stamp}`, categoryId: category.id, baseUnitId: unit.id, purchaseUnitId: null, defaultSupplierId: null, description: null });
  const today = cairoDate(new Date());
  await post(request, '/api/v1/consumption', { restaurantId: restaurant.id, itemId: recentItem.id, quantity: 2, unitId: unit.id, consumptionDate: today, notes: null });
  const oldRecord = await post<{ id: string }>(request, '/api/v1/consumption', { restaurantId: restaurant.id, itemId: oldItem.id, quantity: 3, unitId: unit.id, consumptionDate: today, notes: null });
  const oldInstant = new Date(Date.now() - 10 * 24 * 3600 * 1000);
  const oldDate = cairoDate(oldInstant);
  sql(`update consumption_records set created_at = now() - interval '10 days', updated_at = now() - interval '10 days' where id = '${oldRecord.id}'`);

  const page = await context.newPage();

  // ---- default view: rolling last 24 hours, decided by the server ----
  await page.goto('/consumption');
  await expect(page.getByTestId('consumption-active-filter')).toContainText('آخر 24 ساعة');
  await expect(page.getByRole('cell', { name: `صنف حديث ${stamp}` })).toBeVisible();
  await expect(page.getByRole('cell', { name: `صنف قديم ${stamp}` })).toHaveCount(0);

  // ---- a specific date (the old record's day) ----
  await chooseMode(page, 'تاريخ محدد');
  await page.locator('#consumption-filter-date').fill(oldDate);
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await expect(page.getByTestId('consumption-active-filter')).toContainText(`التاريخ: ${display(oldDate)}`);
  await expect(page.getByRole('cell', { name: `صنف قديم ${stamp}` })).toBeVisible();
  await expect(page.getByRole('cell', { name: `صنف حديث ${stamp}` })).toHaveCount(0);

  // ---- a date with nothing recorded shows the Arabic empty state ----
  await page.locator('#consumption-filter-date').fill('2020-01-01');
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await expect(page.getByText('لا توجد سجلات استهلاك في هذه الفترة')).toBeVisible();

  // ---- a range covering both ----
  await chooseMode(page, 'فترة زمنية');
  await page.locator('#consumption-filter-from').fill(oldDate);
  await page.locator('#consumption-filter-to').fill(today);
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await expect(page.getByTestId('consumption-active-filter')).toContainText(`من ${display(oldDate)} إلى ${display(today)}`);
  await expect(page.getByRole('cell', { name: `صنف حديث ${stamp}` })).toBeVisible();
  await expect(page.getByRole('cell', { name: `صنف قديم ${stamp}` })).toBeVisible();

  // ---- printing the currently filtered (range) report ----
  await page.getByRole('button', { name: 'طباعة التقرير' }).click();
  const report = page.getByTestId('consumption-print-report');
  await expect(report).toBeAttached();
  await expect.poll(() => page.evaluate(() => (window as unknown as { __printCalls: number }).__printCalls)).toBeGreaterThan(0);
  await page.emulateMedia({ media: 'print' });
  await expect(report).toBeVisible();
  await expect(report.getByRole('heading', { name: 'سجل الاستهلاك' })).toBeVisible();
  await expect(page.getByTestId('print-period')).toHaveText(`الفترة: من ${display(oldDate)} إلى ${display(today)}`);
  await expect(report).toContainText(`صنف حديث ${stamp}`);
  await expect(report).toContainText(`صنف قديم ${stamp}`);
  await expect(report).toContainText('تاريخ ووقت إنشاء التقرير');
  await expect(report).toContainText('إجمالي الكمية المستهلكة');
  // Sidebar, filters and buttons are not part of the printed output.
  await expect(page.getByTestId('consumption-filters')).toBeHidden();
  await expect(page.getByRole('navigation')).toBeHidden();
  await page.emulateMedia({ media: 'screen' });
  await page.evaluate(() => window.dispatchEvent(new Event('afterprint')));
  await expect(report).toHaveCount(0);

  // ---- default filter prints "آخر 24 ساعة" and only the recent record ----
  await chooseMode(page, 'آخر 24 ساعة');
  await expect(page.getByTestId('consumption-active-filter')).toContainText('آخر 24 ساعة');
  await page.getByRole('button', { name: 'طباعة التقرير' }).click();
  await expect(page.getByTestId('print-period')).toHaveText('الفترة: آخر 24 ساعة');
  await expect(page.getByTestId('consumption-print-report')).toContainText(`صنف حديث ${stamp}`);
  await expect(page.getByTestId('consumption-print-report')).not.toContainText(`صنف قديم ${stamp}`);

  // ---- single-date print wording ----
  await page.evaluate(() => window.dispatchEvent(new Event('afterprint')));
  await chooseMode(page, 'تاريخ محدد');
  await page.locator('#consumption-filter-date').fill(oldDate);
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await page.getByRole('button', { name: 'طباعة التقرير' }).click();
  await expect(page.getByTestId('print-period')).toHaveText(`التاريخ: ${display(oldDate)}`);

  await context.close();
});
