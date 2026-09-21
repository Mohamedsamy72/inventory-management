import { expect, test, type APIRequestContext } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
// Dedicated browser-test tenant/Owner (not a real account).
const OWNER = { mobileNumber: '01055500001', password: 'E2e!Owner1234' };

async function csrf(request: APIRequestContext): Promise<string> {
  return ((await (await request.get(`${API_BASE}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string }).csrfToken;
}

async function send<T>(request: APIRequestContext, method: 'post' | 'put', path: string, data: unknown): Promise<T> {
  const response = await request[method](`${API_BASE}${path}`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data });
  expect(response.ok(), `${path} -> ${response.status()}`).toBeTruthy();
  return (await response.json()) as T;
}

test('removing an item from the stock asks for the password, then records who removed what quantity', async ({ browser }) => {
  const context = await browser.newContext();
  const request = context.request;
  expect((await request.post(`${API_BASE}/api/v1/auth/login`, { headers: { 'X-CSRF-TOKEN': await csrf(request) }, data: OWNER })).ok()).toBeTruthy();

  const stamp = String(Date.now()).slice(-6);
  const warehouse = await send<{ id: string }>(request, 'post', '/api/v1/warehouses', { nameArabic: `مخزن ${stamp}`, code: `WH-${stamp}`, address: null, description: null });
  const category = await send<{ id: string }>(request, 'post', '/api/v1/categories', { nameArabic: `قسم ${stamp}`, description: null });
  const unit = await send<{ id: string }>(request, 'post', '/api/v1/units', { nameArabic: `كرتونة${stamp}`, abbreviation: null });
  const itemName = `صنف للحذف ${stamp}`;
  const item = await send<{ id: string }>(request, 'post', '/api/v1/items', { nameArabic: itemName, categoryId: category.id, baseUnitId: unit.id, purchaseUnitId: null, defaultSupplierId: null, description: null });
  await send(request, 'put', `/api/v1/warehouses/${warehouse.id}/stock/${item.id}`, { quantity: 8, unitCost: 4, reason: 'اختبار' });

  const page = await context.newPage();
  await page.goto(`/locations/${warehouse.id}/stock`);
  const row = page.getByRole('row', { name: new RegExp(itemName) });
  await expect(row).toBeVisible();

  // A wrong password does not delete anything.
  await page.getByRole('button', { name: `حذف ${itemName} من المخزن` }).click();
  await page.getByLabel('كلمة المرور').fill('definitely-wrong');
  await page.getByRole('button', { name: 'تأكيد الحذف' }).click();
  await expect(page.getByText('كلمة المرور غير صحيحة')).toBeVisible();
  await expect(page.getByLabel('كلمة المرور')).toHaveValue('');
  await page.getByRole('button', { name: 'إلغاء' }).click();
  await expect(row).toBeVisible();

  // The right password removes it from the list.
  await page.getByRole('button', { name: `حذف ${itemName} من المخزن` }).click();
  await page.getByLabel('كلمة المرور').fill(OWNER.password);
  await page.getByRole('button', { name: 'تأكيد الحذف' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('row', { name: new RegExp(itemName) })).toHaveCount(0);

  // It can be brought back into view (with zero stock) through the toggle.
  await page.getByLabel('إظهار الأصناف بدون رصيد').check();
  await expect(page.getByRole('row', { name: new RegExp(itemName) })).toBeVisible();

  // The movements screen data carries who removed it and how much there was.
  const audit = (await (await request.get(`${API_BASE}/api/v1/audit?limit=20`)).json()) as { items: { action: string; descriptionArabic: string }[] };
  const entry = audit.items.find((a) => a.action === 'STOCK_ITEM_REMOVED' && a.descriptionArabic.includes(itemName));
  expect(entry, 'the removal is in the audit trail').toBeTruthy();
  expect(entry!.descriptionArabic).toContain('قام المستخدم');
  expect(entry!.descriptionArabic).toContain('8');
  expect(JSON.stringify(audit)).not.toContain(OWNER.password);
  await context.close();
});
