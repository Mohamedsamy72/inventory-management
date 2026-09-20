import { expect, test, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
const PASSWORD = 'Str0ng!Passw0rd';
const OWNER = { mobileNumber: '01101140802', password: PASSWORD };
const STAFF = { mobileNumber: '01473808391', password: PASSWORD };
const SUPERVISOR = { mobileNumber: '01849965248', password: PASSWORD };
const RESTAURANT_ID = '5c4b5f50-b7bb-44b0-bf14-16f6160c2f06';
const WAREHOUSE_ID = 'b872f3a3-5ecb-424c-8980-11b5dc779dbd';
const ITEM_ID = '5afe09a8-c0bc-44f6-af7d-eb7658e3b841';

type Creds = { mobileNumber: string; password: string };

async function csrf(request: APIRequestContext): Promise<Record<string, string>> {
  const body = (await (await request.get(`${API}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string };
  return { 'X-CSRF-TOKEN': body.csrfToken };
}

async function ctx(browser: Browser, creds: Creds): Promise<BrowserContext> {
  const context = await browser.newContext();
  const response = await context.request.post(`${API}/api/v1/auth/login`, { headers: await csrf(context.request), data: creds });
  if (!response.ok()) {
    throw new Error(`login ${creds.mobileNumber}: ${response.status()}`);
  }
  return context;
}

/**
 * Role-specific workspace verification, real backend + real browser (no mocks). Authorization is
 * asserted through direct HTTP/URL access, never by nav visibility alone. Login is rate limited
 * to 5/IP/minute, so the whole journey shares four contexts created once.
 */
test.describe.configure({ mode: 'serial' });

test('role workspaces: supervisor -> warehouse -> receipt -> discrepancy, and financial visibility', async ({ browser }) => {
  test.setTimeout(180_000);

  const owner = await ctx(browser, OWNER);
  const supervisor = await ctx(browser, SUPERVISOR);
  const staff = await ctx(browser, STAFF);

  const item = (await (await owner.request.get(`${API}/api/v1/items/${ITEM_ID}`)).json()) as { baseUnitId: string };
  const warehouses = (await (await owner.request.get(`${API}/api/v1/warehouses/names`)).json()) as { id: string; nameArabic: string }[];
  const warehouseName = warehouses.find((w) => w.id === WAREHOUSE_ID)!.nameArabic;

  async function balance(): Promise<number> {
    const rows = (await (await owner.request.get(`${API}/api/v1/warehouses/${WAREHOUSE_ID}/stock`)).json()) as { itemId: string; balance: number }[];
    return rows.find((r) => r.itemId === ITEM_ID)!.balance;
  }

  // ---- 1. Supervisor: the CTA opens the create form DIRECTLY (no list page in between) ----
  const supPage = await supervisor.newPage();
  await supPage.goto('/dashboard');
  await supPage.getByRole('button', { name: 'طلب بضاعة جديد' }).click();
  await expect(supPage.getByRole('dialog').getByRole('heading', { name: 'طلب بضاعة جديد' })).toBeVisible();
  expect(new URL(supPage.url()).pathname).toBe('/dashboard');
  await expect(supPage.getByRole('heading', { name: 'طلبات البضاعة', exact: true })).toHaveCount(0);

  await supPage.getByRole('combobox', { name: 'المخزن' }).click();
  await supPage.getByRole('option', { name: warehouseName, exact: true }).click();
  await supPage.getByRole('button', { name: 'إنشاء', exact: true }).click();
  await supPage.waitForURL(/\/supply-requests\/[0-9a-f-]{36}$/);
  const fullRequestId = supPage.url().split('/').pop()!;

  // ---- 2. Supervisor nav is branch-only; direct URL/API to admin areas is refused ----
  const supNav = supPage.locator('[data-slot="sidebar"]');
  for (const forbidden of ['/users', '/settings', '/audit', '/items', '/master-data', '/suppliers', '/locations', '/receiving', '/stock-counts']) {
    await expect(supNav.locator(`a[href="${forbidden}"]`)).toHaveCount(0);
  }
  await expect(supNav.locator('a[href="/supply-requests"]')).toHaveCount(1);
  for (const path of ['users', 'settings', 'audit']) {
    expect((await supervisor.request.get(`${API}/api/v1/${path}`)).status()).toBe(403);
  }

  // ---- 3. Full request: add line (API), submit through the review dialog (UI) ----
  const addLine = async (requestId: string, quantity: number) =>
    supervisor.request.post(`${API}/api/v1/supply-requests/${requestId}/items`, {
      headers: await csrf(supervisor.request),
      data: { itemId: ITEM_ID, unitId: item.baseUnitId, requestedQuantity: quantity, notes: null },
    });
  expect((await addLine(fullRequestId, 10)).ok()).toBeTruthy();
  await supPage.reload();
  await supPage.getByRole('button', { name: 'إرسال الطلب الى المخزن' }).click();
  await supPage.getByRole('button', { name: 'إرسال الطلب', exact: true }).click();
  await expect(supPage.getByText('لا يمكن تعديل الأصناف بعد إرسال الطلب.')).toBeVisible();

  // ---- 4. Warehouse Staff: task-oriented nav; approves the request IN FULL ----
  const staffPage = await staff.newPage();
  await staffPage.goto('/dashboard');
  const staffNav = staffPage.locator('[data-slot="sidebar"]');
  for (const forbidden of ['/users', '/settings', '/audit', '/master-data', '/suppliers', '/locations', '/items', '/stock-counts']) {
    await expect(staffNav.locator(`a[href="${forbidden}"]`)).toHaveCount(0);
  }
  for (const allowed of ['/receiving', '/supply-requests', '/supplies', '/discrepancies']) {
    await expect(staffNav.locator(`a[href="${allowed}"]`)).toHaveCount(1);
  }
  for (const path of ['users', 'settings', 'audit']) {
    expect((await staff.request.get(`${API}/api/v1/${path}`)).status()).toBe(403);
  }
  expect((await staff.request.get(`${API}/api/v1/units`)).status()).toBe(403);
  expect((await staff.request.get(`${API}/api/v1/categories`)).status()).toBe(403);

  await staffPage.goto(`/supply-requests/${fullRequestId}`);
  await staffPage.getByRole('button', { name: 'تجهيز وإرسال للشحن' }).click();
  await staffPage.waitForURL(/\/supplies\/[0-9a-f-]{36}$/);
  const fullSupplyId = staffPage.url().split('/').pop()!;
  await staffPage.getByRole('button', { name: 'شحن التوريد للفرع' }).click();
  await expect(staffPage.getByText('تم الشحن / في الطريق')).toBeVisible();

  // ---- 5. Supervisor receives and confirms in full; stock drops by exactly 10 ----
  const before = await balance();
  await supPage.goto(`/supplies/${fullSupplyId}`);
  await supPage.getByRole('button', { name: 'تأكيد الاستلام', exact: true }).click();
  await expect(supPage.getByText('جميع الكميات مطابقة للمشحون')).toBeVisible();
  await supPage.getByRole('button', { name: 'تأكيد الاستلام نهائياً' }).click();
  await expect(supPage.getByText('تم تأكيد الاستلام', { exact: true })).toBeVisible();
  expect(await balance()).toBe(before - 10);

  // ---- 6. Partial approval (6 of 10) -> partial receipt (4 of 6) -> discrepancy ----
  const created = await supervisor.request.post(`${API}/api/v1/supply-requests`, {
    headers: await csrf(supervisor.request),
    data: { restaurantId: RESTAURANT_ID, warehouseId: WAREHOUSE_ID },
  });
  const partialRequestId = ((await created.json()) as { id: string }).id;
  expect((await addLine(partialRequestId, 10)).ok()).toBeTruthy();
  expect((await supervisor.request.post(`${API}/api/v1/supply-requests/${partialRequestId}/submit`, { headers: await csrf(supervisor.request) })).ok()).toBeTruthy();

  await staffPage.goto(`/supply-requests/${partialRequestId}`);
  await staffPage.getByLabel('الكمية المجهزة').fill('6');
  await staffPage.getByRole('button', { name: 'تجهيز وإرسال للشحن' }).click();
  await staffPage.waitForURL(/\/supplies\/[0-9a-f-]{36}$/);
  const partialSupplyId = staffPage.url().split('/').pop()!;
  await staffPage.getByRole('button', { name: 'شحن التوريد للفرع' }).click();
  await expect(staffPage.getByText('تم الشحن / في الطريق')).toBeVisible();

  await staffPage.goto(`/supply-requests/${partialRequestId}`);
  await expect(staffPage.getByText('تم تجهيز جزء من الطلب')).toBeVisible();

  await supPage.goto(`/supplies/${partialSupplyId}`);
  await supPage.getByRole('spinbutton').fill('4');
  await supPage.getByRole('button', { name: 'تأكيد الاستلام', exact: true }).click();
  await expect(supPage.getByText('يوجد فروقات في الكميات المستلمة')).toBeVisible();
  await supPage.getByRole('button', { name: 'تأكيد الاستلام نهائياً' }).click();
  await expect(supPage.getByText('تم الاستلام مع وجود فروقات')).toBeVisible();

  const discrepancies = (await (await supervisor.request.get(`${API}/api/v1/discrepancies?limit=50`)).json()) as {
    items: { type: string; referenceId: string; variance: number }[];
  };
  const variance = discrepancies.items.find((d) => d.type === 'SupplyReceiptVariance' && d.referenceId === partialSupplyId);
  expect(variance?.variance).toBe(-2);
  await supPage.goto('/discrepancies');
  await expect(supPage.getByText('فرق استلام من المخزن').first()).toBeVisible();

  // ---- 7. Financial visibility: server-enforced. Owner sees cost; Admin never does ----
  const adminMobile = `01${String(Date.now()).slice(-9)}`;
  const createAdmin = await owner.request.post(`${API}/api/v1/users`, {
    headers: await csrf(owner.request),
    data: { fullName: 'مسؤول اختبار', mobileNumber: adminMobile, password: PASSWORD, role: 'Admin' },
  });
  expect(createAdmin.ok()).toBeTruthy();
  const admin = await ctx(browser, { mobileNumber: adminMobile, password: PASSWORD });

  const stockFor = async (c: BrowserContext) =>
    ((await (await c.request.get(`${API}/api/v1/warehouses/${WAREHOUSE_ID}/stock`)).json()) as { itemId: string; averageUnitCost: number | null }[]).find((r) => r.itemId === ITEM_ID)!;
  expect((await stockFor(owner)).averageUnitCost).not.toBeNull();
  expect((await stockFor(admin)).averageUnitCost).toBeNull();
  expect((await stockFor(staff)).averageUnitCost).toBeNull();
  expect((await admin.request.get(`${API}/api/v1/audit`)).status()).toBe(403);
  expect((await owner.request.get(`${API}/api/v1/audit`)).status()).toBe(200);

  const adminPage = await admin.newPage();
  await adminPage.goto(`/locations/${WAREHOUSE_ID}/stock`);
  await expect(adminPage.getByText('متوسط تكلفة الوحدة')).toHaveCount(0);
  await adminPage.goto('/audit');
  await expect(adminPage.getByRole('heading', { name: 'الحركات' })).toHaveCount(0);

  const ownerPage = await owner.newPage();
  await ownerPage.goto(`/locations/${WAREHOUSE_ID}/stock`);
  await expect(ownerPage.getByText('متوسط تكلفة الوحدة')).toBeVisible();

  await owner.close();
  await supervisor.close();
  await staff.close();
  await admin.close();
});
