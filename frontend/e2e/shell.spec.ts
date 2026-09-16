import { expect, test } from '@playwright/test';

/**
 * Phase 1 end-to-end coverage.
 *
 * Real browser, real HTTP. Full role journeys are Phase T2; what is verifiable now is
 * that the document itself is Arabic and right-to-left at the root element, which every
 * later screen inherits.
 */
test.describe('Phase 1 application shell', () => {
  test('document is Arabic and right-to-left', async ({ page }) => {
    await page.goto('/');

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
  });

  test('root page renders Arabic content', async ({ page }) => {
    await page.goto('/');

    const heading = page.locator('h1');
    await expect(heading).toBeVisible();
    await expect(heading).toContainText(/[؀-ۿ]/);
  });
});
