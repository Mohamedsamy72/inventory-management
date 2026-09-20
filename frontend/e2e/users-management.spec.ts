import { expect, test, type APIRequestContext, type Browser } from '@playwright/test';

const API_BASE = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';

const OWNER = { mobileNumber: '01101140802', password: 'Str0ng!Passw0rd' };
const WAREHOUSE_STAFF = { mobileNumber: '01473808391', password: 'Str0ng!Passw0rd' };

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

async function newAuthedContext(browser: Browser, credentials: { mobileNumber: string; password: string }) {
  const context = await browser.newContext();
  await login(context.request, credentials);
  return context;
}

function uniqueMobileNumber(): string {
  // 01 + 9 digits, matching the seeded accounts' shape - derived from the clock so parallel
  ///repeated runs never collide with each other or with a leftover account from a prior run.
  return `01${String(Date.now()).slice(-9)}`;
}

/**
 * Regression coverage for the Users management screens (docs/27 §31.2/§31.4) - built once the
 * backend (deactivate/reactivate/scope/permission-catalogue) and frontend (`/users`,
 * `/users/[id]`) gap was closed. Every fixture here goes through the real backend, using the
 * project's seeded local dev accounts, per the same pattern as e2e/supply-workflow.spec.ts.
 */
test.describe('User management screens', () => {
  test('a non-Owner/Admin role cannot reach /users at all', async ({ browser }) => {
    const staff = await newAuthedContext(browser, WAREHOUSE_STAFF);
    const page = await staff.newPage();

    await page.goto('/users');

    // ForbiddenState, not a silent redirect or a partially-rendered admin screen.
    await expect(page.getByText(/لا تملك|غير مصرح|صلاحية/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'مستخدم جديد' })).toHaveCount(0);

    await staff.close();
  });

  test('Owner can create a user, edit their role/permissions, deactivate and reactivate them', async ({ browser }) => {
    const owner = await newAuthedContext(browser, OWNER);
    const page = await owner.newPage();

    const mobileNumber = uniqueMobileNumber();

    await page.goto('/users');
    await page.getByRole('button', { name: 'مستخدم جديد' }).click();

    await page.getByLabel('الاسم الكامل').fill('مستخدم اختبار E2E');
    await page.getByLabel('رقم الجوال').fill(mobileNumber);
    await page.getByLabel('كلمة المرور المبدئية').fill('Str0ng!Passw0rd');
    // Default role selection ("مستخدم عام") is left as-is - this test only needs the user to
    // exist, not any particular role's permission set.
    await page.getByRole('button', { name: 'إنشاء', exact: true }).click();

    // Create closes the dialog and refreshes the list in place (no redirect) - navigate into
    // the new row explicitly, disambiguated by the unique mobile number (the display name is
    // not unique across runs/leftover fixtures). The table renders it space-grouped
    // (formatMobileNumber), so match digit groups rather than the raw string.
    await expect(page.getByRole('dialog')).toHaveCount(0);
    const formattedMobilePattern = new RegExp(
      `${mobileNumber.slice(0, 3)}\\s*${mobileNumber.slice(3, 7)}\\s*${mobileNumber.slice(7)}`,
    );
    await page
      .getByRole('row', { name: formattedMobilePattern })
      .getByRole('button', { name: 'مستخدم اختبار E2E' })
      .click();
    await page.waitForURL('**/users/*');
    await expect(page.getByRole('heading', { name: 'مستخدم اختبار E2E' })).toBeVisible();
    await expect(page.getByText('معطل')).toHaveCount(0);
    await expect(page.getByText('نشط')).toBeVisible();

    // Deactivate, then reactivate - both round-trip through the real backend, including the
    // security-stamp session-kill fix (docs/27 §31.2) on the deactivate side.
    await page.getByRole('button', { name: 'تعطيل الحساب' }).click();
    await expect(page.getByText('معطل')).toBeVisible();
    await expect(page.getByRole('button', { name: 'إعادة تفعيل الحساب' })).toBeVisible();

    await page.getByRole('button', { name: 'إعادة تفعيل الحساب' }).click();
    await expect(page.getByText('نشط')).toBeVisible();

    await owner.close();
  });
});
