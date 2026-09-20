import { expect, test } from '@playwright/test';

const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5165';
const OWNER = { mobileNumber: '01101140802', password: 'Str0ng!Passw0rd' };

/**
 * Regression: the item edit dialog embedded the unit-conversions block, which was its own <form>
 * nested inside the item <form> - invalid HTML that raised a React hydration error in the console.
 * Opens the real item edit dialog against the real backend and asserts no such console error.
 */
test('the item edit dialog renders without nested-form / hydration console errors', async ({ browser }) => {
  const context = await browser.newContext();
  const csrf = (await (await context.request.get(`${API}/api/v1/auth/csrf-token`)).json()) as { csrfToken: string };
  const login = await context.request.post(`${API}/api/v1/auth/login`, { headers: { 'X-CSRF-TOKEN': csrf.csrfToken }, data: OWNER });
  expect(login.ok()).toBeTruthy();

  const page = await context.newPage();
  const problems: string[] = [];
  page.on('console', (message) => {
    const text = message.text();
    if (message.type() === 'error' && /cannot be a descendant of <form>|cannot contain a nested <form>|hydration/i.test(text)) {
      problems.push(text);
    }
  });

  await page.goto('/items');
  await page.getByRole('button', { name: 'تعديل' }).first().click();
  await expect(page.getByRole('dialog').getByRole('heading', { name: 'تعديل الصنف' })).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('button', { name: 'إضافة', exact: true })).toHaveAttribute('type', 'button');
  await page.waitForTimeout(1000);

  expect(problems).toEqual([]);
  await context.close();
});
