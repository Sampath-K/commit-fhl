import { test, expect } from '@playwright/test';

test.describe('J-01: Morning Digest \u2192 approve first task', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('shows morning digest on first load', async ({ page }) => {
    // Will be implemented when CommitPane is live (T-009)
    await expect(page.locator('[data-testid="morning-digest"]')).toBeVisible();
  });

  test('approving first task shows completion animation', async ({ page }) => {
    // Will be implemented when psychology layer is live (T-C07)
    await expect(page.locator('[data-testid="commit-pane"]')).toBeVisible();
  });
});
