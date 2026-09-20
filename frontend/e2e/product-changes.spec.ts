import { expect, test, type APIRequestContext, type Browser } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
const OWNER = { mobileNumber: '01101140802', password: 'Str0ng!Passw0rd' };
const WAREHOUSE_STAFF = { mobileNumber: '01473808391', password: 'Str0ng!Passw0rd' };
const RESTAURANT_SUPERVISOR = { mobileNumber: '01849965248', password: 'Str0ng!Passw0rd' };

async function login(request: APIRequestContext, credentials: { mobileNumber: string; password: string }) {
  const csrf = (await (await request.get(`${API_BASE}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string };
  const response = await request.post(`${API_BASE}/api/v1/auth/login`, { headers: { 'X-CSRF-TOKEN': csrf.csrfToken }, data: credentials });
  if (!response.ok()) {
    throw new Error(`Login failed for ${credentials.mobileNumber}: ${response.status()}`);
  }
}

async function authedContext(browser: Browser, credentials: { mobileNumber: string; password: string }) {
  const context = await browser.newContext();
  await login(context.request, credentials);
  return context;
}

/**
 * Browser verification for the product-change batch (docs/27 §32): direct issue ("أمر صرف") is
 * Owner/Admin-only, receiving terminology is "أمر استلام", Settings and the audit screen
 * ("الحركات") render real backend data. Real backend, seeded local accounts, no mocks. Keep the
 * login count low - the login endpoint is rate limited to 5/IP/minute by design.
 */
test.describe('Product changes - browser verification', () => {
  test('Owner sees أمر صرف, the receiving button says أمر استلام, Settings and الحركات show real data', async ({ browser }) => {
    const owner = await authedContext(browser, OWNER);
    const page = await owner.newPage();

    await page.goto('/supplies');
    await expect(page.getByRole('button', { name: 'أمر صرف' })).toBeVisible();

    await page.goto('/receiving');
    await expect(page.getByRole('button', { name: 'أمر استلام جديد' })).toBeVisible();
    await expect(page.getByText('أمر توريد')).toHaveCount(0);

    await page.goto('/settings');
    await expect(page.getByRole('heading', { name: 'إعدادات المنشأة' })).toBeVisible();
    await expect(page.getByText('المنطقة الزمنية')).toBeVisible();
    await expect(page.getByText('قادم في مرحلة لاحقة')).toHaveCount(0);

    await page.goto('/audit');
    await expect(page.getByRole('heading', { name: 'الحركات' })).toBeVisible();
    await expect(page.getByRole('table').getByRole('row').nth(1)).toBeVisible();
    await expect(page.getByText('قادم في مرحلة لاحقة')).toHaveCount(0);

    await owner.close();
  });

  test('the restaurant form has no serving-warehouse field', async ({ browser }) => {
    const owner = await authedContext(browser, OWNER);
    const page = await owner.newPage();

    await page.goto('/locations');
    await page.getByRole('tab', { name: 'الفروع' }).click();
    await page.getByRole('button', { name: 'إضافة فرع جديد' }).click();
    await expect(page.getByLabel('الاسم')).toBeVisible();
    await expect(page.getByText('المخزن المغذي')).toHaveCount(0);

    await owner.close();
  });

  test('Warehouse Staff never sees أمر صرف, and the backend rejects the direct call', async ({ browser }) => {
    const staff = await authedContext(browser, WAREHOUSE_STAFF);
    const page = await staff.newPage();

    await page.goto('/supplies');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'أمر صرف' })).toHaveCount(0);

    const csrf = (await (await staff.request.get(`${API_BASE}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string };
    const direct = await staff.request.post(`${API_BASE}/api/v1/supplies/direct-issue`, {
      headers: { 'X-CSRF-TOKEN': csrf.csrfToken, 'X-Idempotency-Key': crypto.randomUUID() },
      data: { warehouseId: crypto.randomUUID(), restaurantId: crypto.randomUUID(), lines: [] },
    });
    expect(direct.status()).toBe(403);

    await staff.close();
  });

  test('Restaurant Supervisor never sees أمر صرف, and gets 404 on /audit and 403 on /settings', async ({ browser }) => {
    const supervisor = await authedContext(browser, RESTAURANT_SUPERVISOR);
    const page = await supervisor.newPage();

    await page.goto('/supplies');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'أمر صرف' })).toHaveCount(0);

    await page.goto('/settings');
    await expect(page.getByText('غير مصرح لك بالوصول')).toBeVisible();

    const auditApi = await supervisor.request.get(`${API_BASE}/api/v1/audit`);
    expect(auditApi.status()).toBe(403);

    await supervisor.close();
  });
});
