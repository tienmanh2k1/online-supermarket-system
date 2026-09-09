import { test, expect, type APIRequestContext, type BrowserContext, type Page } from 'playwright/test';

const adminEmail = process.env.E2E_ADMIN_EMAIL || 'admin@test.com';
const adminPassword = process.env.E2E_ADMIN_PASSWORD || 'Test@123';

function normalizeCurrency(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

test.describe('Submission Pages Integration Suite', () => {
  let adminToken: string;
  let adminApiContext: APIRequestContext;

  test.beforeAll(async ({ playwright, baseURL }) => {
    // 1. Authenticate Admin via API
    const guestApi = await playwright.request.newContext({ baseURL });
    const loginRes = await guestApi.post('/api/auth/login', {
      data: {
        email: adminEmail,
        password: adminPassword,
      },
    });
    expect(loginRes.ok(), 'Admin API login should succeed').toBeTruthy();
    const loginData = await loginRes.json();
    adminToken = loginData.accessToken;

    // Create admin API request context with Authorization header
    adminApiContext = await playwright.request.newContext({
      baseURL,
      extraHTTPHeaders: {
        Authorization: `Bearer ${adminToken}`,
      },
    });
    await guestApi.dispose();
  });

  test.afterAll(async () => {
    await adminApiContext?.dispose();
  });

  test('Admin can log in via UI, view dashboard metrics and navigate to recent order detail', async ({ browser, baseURL }) => {
    const adminContext = await browser.newContext({ baseURL });
    const adminPage = await adminContext.newPage();

    await adminPage.goto('/');

    // Open login modal
    const loginBtn = adminPage.locator('.btn-login');
    await expect(loginBtn).toBeVisible();
    await loginBtn.click();

    // Fill login form
    await adminPage.locator('#login-email').fill(adminEmail);
    await adminPage.locator('#login-password').fill(adminPassword);
    await adminPage.locator('.auth-submit-btn').click();

    // Verify admin is authenticated
    const adminRoleBadge = adminPage.locator('.user-role-badge');
    await expect(adminRoleBadge).toContainText('Quản trị viên');

    // Navigate to admin dashboard
    await adminPage.goto('/admin/dashboard');
    await expect(adminPage).toHaveURL(/.*\/admin\/dashboard/);

    // Verify dashboard metrics and cards
    await expect(adminPage.locator('.admin-dashboard__title')).toHaveText('Tổng quan hệ thống');
    await expect(adminPage.locator('.admin-dashboard__card').filter({ hasText: 'Tổng số đơn hàng' })).toBeVisible();
    await expect(adminPage.locator('.admin-dashboard__card').filter({ hasText: 'Đơn cần xử lý' })).toBeVisible();
    await expect(adminPage.locator('.admin-dashboard__card--revenue')).toBeVisible();

    // Verify recent orders table and click first order link
    const orderLinks = adminPage.locator('.admin-dashboard__order-link');
    const orderCount = await orderLinks.count();
    expect(orderCount, 'Seed data should contain recent orders').toBeGreaterThan(0);

    const firstOrderLink = orderLinks.first();
    const orderId = (await firstOrderLink.textContent())?.trim();
    expect(orderId).toBeTruthy();

    await firstOrderLink.click();
    await expect(adminPage).toHaveURL(new RegExp(`/admin/orders/${orderId}`));
    await expect(adminPage.locator('.admin-content')).toBeVisible();

    await adminContext.close();
  });

  test('Admin sales report changes range and reconciles with backend API', async ({ browser, baseURL }) => {
    const adminContext = await browser.newContext({ baseURL });
    await adminContext.addInitScript((token) => {
      localStorage.setItem('os_access_token', token);
    }, adminToken);
    const adminPage = await adminContext.newPage();

    await adminPage.goto('/admin/reports/sales');
    await expect(adminPage).toHaveURL(/.*\/admin\/reports\/sales/);

    // Verify header and UTC timezone notice
    await expect(adminPage.locator('.admin-sales-report__title')).toHaveText('Báo cáo doanh số');
    await expect(adminPage.locator('.admin-sales-report__timezone-note')).toContainText('Nhóm theo ngày tạo đơn (UTC)');

    // Switch preset to 7 days
    const preset7Btn = adminPage.locator('.admin-sales-report__preset-btn', { hasText: '7 ngày' });
    await preset7Btn.click();
    await expect(preset7Btn).toHaveClass(/admin-sales-report__preset-btn--active/);

    // Read selected from/to dates from inputs
    const fromVal = await adminPage.locator('#sales-from').inputValue();
    const toVal = await adminPage.locator('#sales-to').inputValue();
    expect(fromVal).toBeTruthy();
    expect(toVal).toBeTruthy();

    // Call the real sales report API using admin API context
    const apiReportRes = await adminApiContext.get(`/api/admin/reports/sales?from=${fromVal}&to=${toVal}`);
    expect(apiReportRes.ok(), 'Admin sales report API should return 200 OK').toBeTruthy();
    const reportData = await apiReportRes.json();

    // Format expected total revenue in vi-VN currency
    const expectedRevenueFormatted = normalizeCurrency(
      new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(reportData.totalRevenue)
    );

    // Reconcile total revenue KPI on the UI
    const revenueKpi = adminPage.locator('.admin-sales-report__kpi-value--revenue');
    await expect(revenueKpi).toBeVisible();
    const uiRevenueText = normalizeCurrency((await revenueKpi.textContent()) || '');
    expect(uiRevenueText).toBe(expectedRevenueFormatted);

    // Reconcile completed orders count
    const completedOrdersKpi = adminPage.locator('.admin-sales-report__kpi-card').nth(1).locator('.admin-sales-report__kpi-value');
    await expect(completedOrdersKpi).toHaveText(String(reportData.completedOrderCount));

    await adminContext.close();
  });

  test('Customer lifecycle: register, forgot password, reset via dev mailbox, login with new password, and RBAC guard', async ({ browser, baseURL }) => {
    const customerContext = await browser.newContext({ baseURL });
    const customerPage = await customerContext.newPage();

    const timestamp = Date.now();
    const customerEmail = `customer_${timestamp}@test.com`;
    const initialPassword = 'Test@123';
    const newPassword = 'Changed@123';

    // 1. Register fresh customer via API
    const regRes = await adminApiContext.post('/api/auth/register', {
      data: {
        fullName: `Test Customer ${timestamp}`,
        email: customerEmail,
        password: initialPassword,
      },
    });
    expect(regRes.ok(), 'Customer registration should succeed').toBeTruthy();

    // 2. Customer opens forgot password modal on frontend
    await customerPage.goto('/');
    await customerPage.locator('.btn-login').click();
    await customerPage.locator('.auth-form__forgot-row .btn-link').click();

    await expect(customerPage.locator('.auth-form__title')).toHaveText('Khôi phục mật khẩu');
    await customerPage.locator('#forgot-email').fill(customerEmail);
    await customerPage.locator('.auth-submit-btn').click();

    // Verify success confirmation message
    const alertSuccess = customerPage.locator('.auth-form__alert--success');
    await expect(alertSuccess).toBeVisible();
    await expect(alertSuccess).toContainText('Nếu email tồn tại');

    // 3. Admin fetches reset URL from dev mailbox (DO NOT LOG URL OR TOKEN)
    let resetUrl: string | null = null;
    for (let attempt = 0; attempt < 10; attempt++) {
      const mailboxRes = await adminApiContext.get(`/api/dev/password-reset-emails?email=${encodeURIComponent(customerEmail)}`);
      if (mailboxRes.ok()) {
        const mailData = await mailboxRes.json();
        if (mailData?.resetUrl) {
          resetUrl = mailData.resetUrl;
          break;
        }
      }
      await customerPage.waitForTimeout(500);
    }
    expect(resetUrl, 'Reset URL must be available in dev mailbox').toBeTruthy();

    // 4. Open reset URL in Customer browser context
    await customerPage.goto(resetUrl!);
    await expect(customerPage.locator('.reset-password-page__title')).toHaveText('Đặt lại mật khẩu');

    // Fill new password form
    await customerPage.locator('#new-password').fill(newPassword);
    await customerPage.locator('#confirm-password').fill(newPassword);
    await customerPage.locator('button[type="submit"].auth-submit-btn').click();

    // Verify reset success
    await expect(customerPage.locator('.auth-form__alert--success')).toContainText(
      'Đặt lại mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới.'
    );

    // 5. Navigate to home and log in with new password
    await customerPage.goto('/');
    await customerPage.locator('.btn-login').click();
    await customerPage.locator('#login-email').fill(customerEmail);
    await customerPage.locator('#login-password').fill(newPassword);
    await customerPage.locator('.auth-submit-btn').click();

    // Verify customer is authenticated
    await expect(customerPage.locator('.user-avatar')).toBeVisible();
    await expect(customerPage.locator('.btn-logout')).toBeVisible();

    // 6. RBAC Guard: Customer must be redirected away from admin dashboard
    await customerPage.goto('/admin/dashboard');
    await expect(customerPage).toHaveURL(/^(?!.*\/admin\/dashboard).*$/);
    await expect(customerPage.locator('.admin-dashboard__title')).toHaveCount(0);

    await customerContext.close();
  });

  test('404 Not Found page renders properly and navigates back to home', async ({ page }) => {
    await page.goto('/random-non-existent-route-404');
    await expect(page.locator('.not-found-page__title')).toHaveText('Không tìm thấy trang');
    await expect(page.locator('.not-found-page__code')).toHaveText('404');

    const homeLink = page.locator('.not-found-page__btn-home');
    await expect(homeLink).toBeVisible();
    await homeLink.click();
    await expect(page).toHaveURL(/.*\/$/);
  });

  test('Legal pages (/privacy, /terms) and footer links are functional', async ({ page }) => {
    await page.goto('/');

    // Check footer links
    const privacyFooterLink = page.locator('.site-footer__links a[href="/privacy"]');
    const termsFooterLink = page.locator('.site-footer__links a[href="/terms"]');
    await expect(privacyFooterLink).toBeVisible();
    await expect(termsFooterLink).toBeVisible();

    // Check Privacy page
    await privacyFooterLink.click();
    await expect(page).toHaveURL(/.*\/privacy/);
    await expect(page.locator('.legal-page__title')).toHaveText('Chính sách bảo mật');
    await expect(page.locator('.legal-page__section')).toHaveCount(5);

    // Check Terms page
    await page.goto('/terms');
    await expect(page.locator('.legal-page__title')).toHaveText('Điều khoản dịch vụ');
    await expect(page.locator('.legal-page__callout')).toContainText('VNPay');
    await expect(page.locator('.legal-page__callout')).toContainText('MoMo');
  });

  test('Responsive smoke: new routes render cleanly on mobile viewport', async ({ browser, baseURL }) => {
    const mobileContext = await browser.newContext({
      baseURL,
      viewport: { width: 375, height: 667 },
      isMobile: true,
    });
    const mobilePage = await mobileContext.newPage();

    // 404 page on mobile
    await mobilePage.goto('/random-404-mobile');
    await expect(mobilePage.locator('.not-found-page__title')).toBeVisible();
    await expect(mobilePage.locator('.not-found-page__btn-home')).toBeVisible();

    // Legal pages on mobile
    await mobilePage.goto('/privacy');
    await expect(mobilePage.locator('.legal-page__title')).toBeVisible();
    await mobilePage.goto('/terms');
    await expect(mobilePage.locator('.legal-page__title')).toBeVisible();

    // Reset password on mobile
    await mobilePage.goto('/reset-password?token=dummy-mobile-token');
    await expect(mobilePage.locator('.reset-password-page__title')).toBeVisible();
    await expect(mobilePage.locator('#new-password')).toBeVisible();

    // Admin pages on mobile with admin session
    await mobileContext.addInitScript((token) => {
      localStorage.setItem('os_access_token', token);
    }, adminToken);
    await mobilePage.goto('/admin/dashboard');
    await expect(mobilePage.locator('.admin-dashboard__title')).toBeVisible();

    await mobilePage.goto('/admin/reports/sales');
    await expect(mobilePage.locator('.admin-sales-report__title')).toBeVisible();

    await mobileContext.close();
  });
});
