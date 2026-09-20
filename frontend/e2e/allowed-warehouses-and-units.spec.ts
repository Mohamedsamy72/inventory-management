import { expect, test, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
const PASSWORD = 'Str0ng!Passw0rd';
const OWNER = { mobileNumber: '01101140802', password: PASSWORD };
const SUPERVISOR = { mobileNumber: '01849965248', password: PASSWORD };
const RESTAURANT_ID = '5c4b5f50-b7bb-44b0-bf14-16f6160c2f06';
const WAREHOUSE_ID = 'b872f3a3-5ecb-424c-8980-11b5dc779dbd';
const ITEM_ID = '5afe09a8-c0bc-44f6-af7d-eb7658e3b841';

async function csrf(request: APIRequestContext): Promise<Record<string, string>> {
  const body = (await (await request.get(`${API}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string };
  return { 'X-CSRF-TOKEN': body.csrfToken };
}

async function ctx(browser: Browser, creds: { mobileNumber: string; password: string }): Promise<BrowserContext> {
  const context = await browser.newContext();
  const response = await context.request.post(`${API}/api/v1/auth/login`, { headers: await csrf(context.request), data: creds });
  if (!response.ok()) {
    throw new Error(`login ${creds.mobileNumber}: ${response.status()}`);
  }
  return context;
}

/**
 * Allowed-warehouse mapping and direct issue in a non-base unit, real backend + real browser.
 * Product decisions: a restaurant may only request from warehouses Owner/Admin allowed for it;
 * direct issue supports defined conversion units (server-side conversion, base-unit ledger).
 */
test('allowed warehouses drive the supervisor selector; direct issue converts a defined unit', async ({ browser }) => {
  test.setTimeout(120_000);
  const owner = await ctx(browser, OWNER);
  const supervisor = await ctx(browser, SUPERVISOR);

  const headers = async (c: BrowserContext) => csrf(c.request);
  const allWarehouses = (await (await owner.request.get(`${API}/api/v1/warehouses/names`)).json()) as { id: string; nameArabic: string }[];
  const allowedNames = async () =>
    ((await (await supervisor.request.get(`${API}/api/v1/restaurants/${RESTAURANT_ID}/warehouses`)).json()) as { id: string; nameArabic: string }[]);

  // Narrow the supervisor's restaurant to exactly ONE known warehouse, through the real manage UI.
  const target = allWarehouses.find((w) => w.id === WAREHOUSE_ID)!;
  const original = (await allowedNames()).map((w) => w.id);
  const put = await owner.request.put(`${API}/api/v1/restaurants/${RESTAURANT_ID}/warehouses`, { headers: await headers(owner), data: { warehouseIds: [WAREHOUSE_ID] } });
  expect(put.ok()).toBeTruthy();

  // Supervisor: the selector offers the allowed warehouse and nothing else.
  const supPage = await supervisor.newPage();
  await supPage.goto('/dashboard');
  await supPage.getByRole('button', { name: 'طلب بضاعة جديد' }).click();
  await supPage.getByRole('combobox', { name: 'المخزن' }).click();
  await expect(supPage.getByRole('option')).toHaveCount(1);
  await expect(supPage.getByRole('option', { name: target.nameArabic, exact: true })).toBeVisible();
  await supPage.keyboard.press('Escape');

  // Backend authority: any other warehouse is refused even by a direct call.
  const other = allWarehouses.find((w) => w.id !== WAREHOUSE_ID)!;
  const refused = await supervisor.request.post(`${API}/api/v1/supply-requests`, {
    headers: await headers(supervisor),
    data: { restaurantId: RESTAURANT_ID, warehouseId: other.id },
  });
  expect(refused.status()).toBe(409);

  // Owner manages the mapping from the UI (dialog opens with the current selection).
  const ownerPage = await owner.newPage();
  await ownerPage.goto('/locations');
  await ownerPage.getByRole('tab', { name: 'الفروع' }).click();
  await ownerPage.getByRole('button', { name: 'المخازن المسموح بها' }).first().click();
  await expect(ownerPage.getByRole('dialog').getByText('المخازن المسموح بها')).toBeVisible();
  await ownerPage.keyboard.press('Escape');

  // ---- Direct issue in a defined non-base unit: 1 carton = 2 base units ----
  const item = (await (await owner.request.get(`${API}/api/v1/items/${ITEM_ID}`)).json()) as { baseUnitId: string };
  const unitName = `كرتونة ${Date.now()}`;
  const unit = (await (await owner.request.post(`${API}/api/v1/units`, { headers: await headers(owner), data: { nameArabic: unitName, abbreviation: null } })).json()) as { id: string };
  const conversion = await owner.request.post(`${API}/api/v1/items/${ITEM_ID}/conversions`, { headers: await headers(owner), data: { fromUnitId: unit.id, conversionFactor: 2 } });
  expect(conversion.ok()).toBeTruthy();
  expect(item.baseUnitId).not.toBe(unit.id);

  const balance = async () =>
    ((await (await owner.request.get(`${API}/api/v1/warehouses/${WAREHOUSE_ID}/stock`)).json()) as { itemId: string; balance: number }[]).find((r) => r.itemId === ITEM_ID)!.balance;
  const before = await balance();

  await ownerPage.goto('/supplies');
  await ownerPage.getByRole('button', { name: 'أمر صرف' }).click();
  const dialog = ownerPage.getByRole('dialog');
  await dialog.getByRole('combobox', { name: 'المخزن' }).click();
  await ownerPage.getByRole('option', { name: target.nameArabic, exact: true }).click();
  await dialog.getByRole('combobox', { name: 'الفرع' }).click();
  await ownerPage.getByRole('option').first().click();
  await dialog.getByRole('combobox', { name: 'الصنف 1' }).click();
  const itemName = ((await (await owner.request.get(`${API}/api/v1/items/${ITEM_ID}`)).json()) as { nameArabic: string }).nameArabic;
  await ownerPage.getByRole('option', { name: itemName, exact: true }).first().click();
  await dialog.getByRole('combobox', { name: 'الوحدة 1' }).click();
  await ownerPage.getByRole('option', { name: unitName, exact: true }).click();
  await dialog.getByRole('spinbutton', { name: 'الكمية 1' }).fill('3');
  await dialog.getByRole('button', { name: 'تنفيذ أمر الصرف' }).click();
  await ownerPage.waitForURL(/\/supplies\/[0-9a-f-]{36}$/);

  expect(await balance()).toBe(before - 6);

  // restore the original allowed set so other specs/dev data are unaffected
  await owner.request.put(`${API}/api/v1/restaurants/${RESTAURANT_ID}/warehouses`, { headers: await headers(owner), data: { warehouseIds: original } });

  await owner.close();
  await supervisor.close();
});
