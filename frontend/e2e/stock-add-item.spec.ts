import { expect, test, type APIRequestContext } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
// Dedicated browser-test tenant/Owner (not a real account).
const OWNER = { mobileNumber: '01055500001', password: 'E2e!Owner1234' };

async function csrf(request: APIRequestContext): Promise<string> {
  return ((await (await request.get(`${API_BASE}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string }).csrfToken;
}

async function post<T>(request: APIRequestContext, path: string, data: unknown): Promise<T> {
  const response = await request.post(`${API_BASE}${path}`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data });
  expect(response.ok(), `${path} -> ${response.status()}`).toBeTruthy();
  return (await response.json()) as T;
}

test('the warehouse stock screen can add a brand-new item together with its opening stock', async ({ browser }) => {
  const context = await browser.newContext();
  const request = context.request;
  expect((await request.post(`${API_BASE}/api/v1/auth/login`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data: OWNER })).ok()).toBeTruthy();

  const stamp = String(Date.now()).slice(-6);
  const warehouse = await post<{ id: string }>(request, '/api/v1/warehouses', { nameArabic: `مخزن ${stamp}`, code: `WH-${stamp}`, address: null, description: null });
  const category = await post<{ id: string }>(request, '/api/v1/categories', { nameArabic: `قسم ${stamp}`, description: null });
  const unit = await post<{ id: string }>(request, '/api/v1/units', { nameArabic: `كرتونة${stamp}`, abbreviation: null });
  void category;
  void unit;

  const page = await context.newPage();
  await page.goto(`/locations/${warehouse.id}/stock`);
  await page.getByRole('button', { name: 'إضافة صنف بالرصيد' }).click();

  const itemName = `صنف جديد ${stamp}`;
  await page.getByLabel('اسم الصنف').fill(itemName);
  await page.locator('#add-item-category').click();
  await page.getByRole('option', { name: `قسم ${stamp}` }).click();
  await page.locator('#add-item-unit').click();
  await page.getByRole('option', { name: `كرتونة${stamp}` }).click();
  await page.getByLabel('الرصيد الافتتاحي في هذا المخزن').fill('12');
  await page.getByLabel('تكلفة الوحدة (ج.م)').fill('5');
  await page.getByRole('button', { name: 'حفظ الصنف والرصيد' }).click();

  await expect(page.getByRole('dialog')).toHaveCount(0);
  const row = page.getByRole('row', { name: new RegExp(itemName) });
  await expect(row).toBeVisible();
  await expect(row).toContainText('12');

  // The balance is a real ledger-backed value: the API agrees.
  const stock = (await (await request.get(`${API_BASE}/api/v1/warehouses/${warehouse.id}/stock`)).json()) as { balance: number; averageUnitCost: number | null }[];
  expect(stock.map((line) => line.balance)).toContain(12);
  expect(stock.find((line) => line.balance === 12)?.averageUnitCost).toBe(5);
  await context.close();
});
