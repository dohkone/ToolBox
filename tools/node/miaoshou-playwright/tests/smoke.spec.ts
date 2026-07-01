import { test, expect } from "@playwright/test";

test("homepage opens", async ({ page }) => {
  await page.goto(process.env.BASE_URL || "https://example.com");
  await expect(page).toHaveTitle(/Example/);
});
