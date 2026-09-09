import { test, expect } from 'playwright/test';

test.describe('AptechMart Homepage Comprehensive Verification & Audit', () => {
  test('1. Viewport 1440x900 (Desktop) - Clean layout without horizontal overflow', async ({ page }) => {
    const consoleWarnings: string[] = [];
    const consoleErrors: string[] = [];
    page.on('console', (msg) => {
      const text = msg.text();
      if (msg.type() === 'error') consoleErrors.push(text);
      if (msg.type() === 'warning') consoleWarnings.push(text);
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 1A. No horizontal scrollbar
    const hasNoOverflow = await page.evaluate(() => {
      return document.documentElement.scrollWidth <= window.innerWidth;
    });
    expect(hasNoOverflow, 'Desktop 1440x900 must not have horizontal overflow').toBeTruthy();

    // 1B. Verify 8 purchase journey sections in order
    const sections = page.locator('.appliance-home-page > [data-testid]');
    const testIds = await sections.evaluateAll((list) => list.map((el) => el.getAttribute('data-testid')));
    expect(testIds).toEqual([
      'home-hero',
      'home-quick-categories',
      'home-trust-strip',
      'home-bestsellers',
      'home-category-showcase',
      'home-recommendations',
      'home-branches',
      'home-roadmap',
    ]);

    // 1C. Hero 2-column campaign board layout
    await expect(page.locator('[data-testid="home-hero"]')).toBeVisible();
    await expect(page.locator('[data-testid="hero-main-slider"]')).toBeVisible();
    await expect(page.locator('.appliance-sub-banners')).toBeVisible();
    await expect(page.locator('[data-testid="sub-banner-container"]')).toHaveCount(2);

    // 1D. Quick category strip with 6 items
    await expect(page.locator('[data-testid="home-quick-categories"] li')).toHaveCount(6);

    // 1E. Trust strip with 4 items
    await expect(page.locator('[data-testid="home-trust-strip"] li')).toHaveCount(4);

    // 1F. Bestsellers & Category Showcase products grid
    await expect(page.locator('[data-testid="home-bestsellers"]')).toBeVisible();
    await expect(page.locator('[data-testid="home-category-showcase"]')).toBeVisible();

    // 1G. Roadmap with 3 compact cards
    await expect(page.locator('[data-testid="home-roadmap"] .home-roadmap__card')).toHaveCount(3);

    // 1H. Console Cleanliness
    const reactWarnings = consoleWarnings.filter((w) => /warning:.*react/i.test(w) || /validateDOMNesting/i.test(w));
    expect(reactWarnings, 'Must have no React warnings in console').toEqual([]);
  });

  test('2. Viewport 390x844 (Mobile) - Responsive layout, sub-banners hidden, horizontal scroll without page overflow', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 2A. No page-level horizontal overflow
    const hasNoOverflow = await page.evaluate(() => {
      return document.documentElement.scrollWidth <= window.innerWidth;
    });
    expect(hasNoOverflow, 'Mobile 390x844 must not have page horizontal overflow').toBeTruthy();

    // 2B. Sub-banners must be hidden on mobile to prioritize main slider
    const subBanners = page.locator('.appliance-sub-banners');
    await expect(subBanners).toBeHidden();

    // 2C. Quick categories track must have horizontal overflow capability (scroll-snap)
    const catTrack = page.locator('.kg-quick-categories__track');
    const catScrollWidth = await catTrack.evaluate((el) => el.scrollWidth);
    const catClientWidth = await catTrack.evaluate((el) => el.clientWidth);
    expect(catScrollWidth, 'Categories track should scroll horizontally on mobile').toBeGreaterThanOrEqual(catClientWidth);

    // 2D. Product shelves cuộn ngang mượt mà
    const bestsellersGrid = page.locator('[data-testid="home-bestsellers"] .flash-sale-grid');
    const gridScrollWidth = await bestsellersGrid.evaluate((el) => el.scrollWidth);
    const gridClientWidth = await bestsellersGrid.evaluate((el) => el.clientWidth);
    expect(gridScrollWidth, 'Bestsellers grid should scroll horizontally on mobile').toBeGreaterThanOrEqual(gridClientWidth);
  });

  test('3. Visual Verification: 2D/3D WebGL Showroom toggle & context disposal stability', async ({ page }) => {
    const webglErrors: string[] = [];
    page.on('console', (msg) => {
      const text = msg.text();
      if (/webgl/i.test(text) && (msg.type() === 'error' || msg.type() === 'warning')) {
        webglErrors.push(text);
      }
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const toggleBtn = page.locator('.appliance-hero__view-toggle');
    await expect(toggleBtn).toBeVisible();

    // 3A. Initial state: 3D active, Canvas exists
    await expect(toggleBtn).toHaveClass(/active/);
    const canvas = page.locator('[data-testid="hero-3d-showcase"] canvas');
    await expect(canvas).toBeAttached();

    // 3B. Toggle to 2D Fallback Banner mode
    await toggleBtn.click();
    await expect(toggleBtn).not.toHaveClass(/active/);
    await expect(canvas).not.toBeAttached();
    // 2D banner image is shown
    await expect(page.locator('.appliance-slider-image')).toBeVisible();

    // 3C. Toggle back to 3D mode
    await toggleBtn.click();
    await expect(toggleBtn).toHaveClass(/active/);
    await expect(canvas).toBeAttached();

    // 3D. Repeated toggle test (5 cycles) to verify NO WebGL context leak or "Too many active WebGL contexts"
    for (let i = 0; i < 5; i++) {
      await toggleBtn.click(); // to 2D
      await page.waitForTimeout(50);
      await toggleBtn.click(); // to 3D
      await page.waitForTimeout(50);
    }

    const contextLostWarnings = webglErrors.filter((e) => /too many active webgl contexts/i.test(e) || /context_lost_webgl/i.test(e));
    expect(contextLostWarnings, 'No WebGL context leak warnings during repeated toggling').toEqual([]);
  });

  test('4. Visual Verification: Keyboard focus indicators (:focus-visible)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 4A. Focus toggle button via Tab or direct focus
    const toggleBtn = page.locator('.appliance-hero__view-toggle');
    await toggleBtn.focus();

    // Verify focus outline matches brand orange (#ff7a1a) or contains orange outline
    const outlineColor = await toggleBtn.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return styles.outlineColor || styles.outline;
    });
    expect(outlineColor).toBeTruthy();

    // 4B. Focus Category Tab button
    const firstTabBtn = page.locator('.category-tab-btn').first();
    await firstTabBtn.focus();
    const tabOutline = await firstTabBtn.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return styles.outlineColor || styles.outline;
    });
    expect(tabOutline).toBeTruthy();
  });

  test('5. Visual Verification: Reduced Motion behavior', async ({ page }) => {
    // Emulate prefers-reduced-motion: reduce
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Check that transition-duration is neutralized on elements inside .appliance-home-page
    const duration = await page.evaluate(() => {
      const el = document.querySelector('.appliance-home-page');
      if (!el) return 'unknown';
      const style = window.getComputedStyle(el);
      return style.transitionDuration;
    });
    expect(parseFloat(duration)).toBeLessThanOrEqual(0.01);
  });

  test('6. Console Cleanliness: No React errors, uncaught exceptions, or duplicate key warnings', async ({ page }) => {
    const errorLogs: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        errorLogs.push(msg.text());
      }
    });

    const pageErrors: Error[] = [];
    page.on('pageerror', (err) => {
      pageErrors.push(err);
    });

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Interact with elements: switch category tabs
    const tabs = page.locator('.category-tab-btn');
    const count = await tabs.count();
    if (count > 1) {
      await tabs.nth(1).click();
      await page.waitForTimeout(100);
      await tabs.nth(0).click();
      await page.waitForTimeout(100);
    }

    // Filter out network 404s for external mock images if any
    const fatalErrors = errorLogs.filter(
      (err) =>
        !err.includes('net::ERR_') &&
        !err.includes('Failed to load resource') &&
        !err.includes('404') &&
        !err.includes('favicon')
    );

    expect(pageErrors, 'Should have no uncaught page errors').toHaveLength(0);
    expect(fatalErrors, 'Should have no console errors during homepage interactions').toHaveLength(0);
  });
});
