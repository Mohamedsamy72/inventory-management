import { expect, test, type APIRequestContext, type Browser } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';

const RESTAURANT_SUPERVISOR = { mobileNumber: '01849965248', password: 'Str0ng!Passw0rd' };
const WAREHOUSE_STAFF = { mobileNumber: '01473808391', password: 'Str0ng!Passw0rd' };
const RESTAURANT_ID = '5c4b5f50-b7bb-44b0-bf14-16f6160c2f06';
const ITEM_ID = 'f59e915b-b28c-4499-9d6b-0f3210131429';
const UNIT_ID = '9d988f22-49ae-4ccf-827e-348c2a13d251';

async function login(request: APIRequestContext, credentials: { mobileNumber: string; password: string }) {
  const csrfResponse = await request.get(`${API_BASE}/api/v1/auth/csrf-token`);
  const { csrfToken } = (await csrfResponse.json()) as { csrfToken: string };
  const loginResponse = await request.post(`${API_BASE}/api/v1/auth/login`, {
    headers: { 'X-CSRF-TOKEN': csrfToken },
    data: credentials,
  });
  if (!loginResponse.ok()) {
    throw new Error(`Login failed for ${credentials.mobileNumber}: ${loginResponse.status()}`);
  }
}

async function csrfHeader(request: APIRequestContext): Promise<Record<string, string>> {
  const response = await request.get(`${API_BASE}/api/v1/auth/csrf-token`);
  const { csrfToken } = (await response.json()) as { csrfToken: string };
  return { 'X-CSRF-TOKEN': csrfToken };
}

async function newAuthedContext(browser: Browser, credentials: { mobileNumber: string; password: string }) {
  const context = await browser.newContext();
  await login(context.request, credentials);
  return context;
}

/**
 * Regression coverage for the supply-request confirmation UX fix: the receipt-confirmation
 * dialog ("تأكيد استلام التوريد بالفرع", `supplies/[id]`) must stay reachable only via the
 * `supplies:confirm` permission (Restaurant Supervisor by role default, never Warehouse
 * Staff), and a separate request-review confirmation must appear to the Restaurant
 * Supervisor BEFORE a Draft request is submitted - a step that previously did not exist at
 * all. Every fixture here is created through the real backend (no mocking), using the
 * project's seeded local dev accounts.
 */
test.describe('Supply request / supply confirmation workflow boundaries', () => {
  test('Restaurant Supervisor sees a request-review confirmation before submitting - never a stock-matching claim', async ({
    browser,
  }) => {
    const supervisor = await newAuthedContext(browser, RESTAURANT_SUPERVISOR);

    const createResponse = await supervisor.request.post(`${API_BASE}/api/v1/supply-requests`, {
      headers: await csrfHeader(supervisor.request),
      data: { restaurantId: RESTAURANT_ID },
    });
    expect(createResponse.ok()).toBeTruthy();
    const created = (await createResponse.json()) as { id: string };

    const lineResponse = await supervisor.request.post(`${API_BASE}/api/v1/supply-requests/${created.id}/items`, {
      headers: await csrfHeader(supervisor.request),
      data: { itemId: ITEM_ID, unitId: UNIT_ID, requestedQuantity: 3, notes: null },
    });
    expect(lineResponse.ok()).toBeTruthy();

    const page = await supervisor.newPage();
    await page.goto(`/supply-requests/${created.id}`);

    await page.getByRole('button', { name: 'إرسال الطلب الى المخزن' }).click();

    await expect(page.getByRole('heading', { name: 'مراجعة طلب التوريد' })).toBeVisible();
    await expect(page.getByText('راجع الأصناف والكميات المطلوبة قبل إرسال الطلب.')).toBeVisible();
    // The request-review step must describe REQUESTED quantities, never claim a stock
    // check that does not happen at this point in the workflow (docs/09 - fulfilment, not
    // submission, is where availability is checked).
    await expect(page.getByText(/مطابق(ة|ه)? .*مخزون/)).toHaveCount(0);

    await page.getByRole('button', { name: 'إرسال الطلب', exact: true }).click();
    await expect(page.getByText('لا يمكن تعديل الأصناف بعد إرسال الطلب.')).toBeVisible();

    await supervisor.close();
  });

  test('Warehouse Staff never sees the restaurant receipt-confirmation action on a dispatched supply', async ({ browser }) => {
    const supervisor = await newAuthedContext(browser, RESTAURANT_SUPERVISOR);
    const staff = await newAuthedContext(browser, WAREHOUSE_STAFF);

    const createResponse = await supervisor.request.post(`${API_BASE}/api/v1/supply-requests`, {
      headers: await csrfHeader(supervisor.request),
      data: { restaurantId: RESTAURANT_ID },
    });
    const request = (await createResponse.json()) as { id: string };

    const lineResponse = await supervisor.request.post(`${API_BASE}/api/v1/supply-requests/${request.id}/items`, {
      headers: await csrfHeader(supervisor.request),
      data: { itemId: ITEM_ID, unitId: UNIT_ID, requestedQuantity: 1, notes: null },
    });
    const line = (await lineResponse.json()) as { id: string };

    await supervisor.request.post(`${API_BASE}/api/v1/supply-requests/${request.id}/submit`, {
      headers: await csrfHeader(supervisor.request),
    });

    const fulfillResponse = await staff.request.post(`${API_BASE}/api/v1/supply-requests/${request.id}/fulfill`, {
      headers: await csrfHeader(staff.request),
      data: { lines: [{ supplyRequestItemId: line.id, fulfilledQuantity: 0 }] },
    });
    const fulfillment = (await fulfillResponse.json()) as { supply: { id: string } };

    await staff.request.post(`${API_BASE}/api/v1/supplies/${fulfillment.supply.id}/dispatch`, {
      headers: await csrfHeader(staff.request),
    });

    // Backend authorization must independently reject a Warehouse Staff confirm attempt -
    // the UI gate is a courtesy, not the actual control (docs/03, ADR-012's sibling rule for
    // this workflow boundary).
    const staffConfirmAttempt = await staff.request.post(`${API_BASE}/api/v1/supplies/${fulfillment.supply.id}/confirm`, {
      headers: await csrfHeader(staff.request),
      data: { lines: [] },
    });
    expect(staffConfirmAttempt.status()).toBe(403);

    const page = await staff.newPage();
    await page.goto(`/supplies/${fulfillment.supply.id}`);

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تأكيد الاستلام' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'تأكيد استلام التوريد بالفرع' })).toHaveCount(0);

    await supervisor.close();
    await staff.close();
  });
});
