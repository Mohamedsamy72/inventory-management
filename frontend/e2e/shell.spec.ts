import { expect, test } from '@playwright/test';

/**
 * Application shell / auth-guard end-to-end coverage.
 *
 * `/` server-redirects to `/dashboard` (task F2), which is inside the authenticated
 * route group - an unauthenticated visitor's session bootstrap 401s there, and
 * `api-client` redirects on to `/login`. What is verifiable now, without a seeded
 * backend user for this Playwright run, is that whole chain: real browser, real HTTP,
 * landing on a real Arabic, right-to-left login page - not a broken or blank one.
 */
test.describe('Application shell and auth guard', () => {
  test('an unauthenticated visitor to / lands on /login, Arabic and right-to-left', async ({ page }) => {
    await page.goto('/');

    await page.waitForURL('**/login');

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');

    const heading = page.locator('h1');
    await expect(heading).toBeVisible();
    await expect(heading).toContainText(/[؀-ۿ]/);
  });

  test('login page renders the mobile number and password fields', async ({ page }) => {
    await page.goto('/login');

    await expect(page.getByLabel('رقم الجوال')).toBeVisible();
    await expect(page.getByLabel('كلمة المرور')).toBeVisible();
  });

  test('submitting invalid credentials against the real API shows the Arabic error banner', async ({ page }) => {
    await page.goto('/login');

    await page.getByLabel('رقم الجوال').fill('01000000000');
    await page.getByLabel('كلمة المرور').fill('WrongPassword123!');
    await page.getByRole('button', { name: 'تسجيل الدخول' }).click();

    // Next.js's own route announcer is also role="alert" (always present, always empty) -
    // filter it out rather than assuming ErrorBanner is the first/only match.
    await expect(page.getByRole('alert').filter({ hasText: /.+/ })).toContainText(
      'رقم الجوال أو كلمة المرور غير صحيحة',
    );
    // Still on /login - a failed login must never navigate away.
    await expect(page).toHaveURL(/\/login$/);
  });
});
