// E2E / GUI tests for Completed Order -> Review Flow
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const frontendDir = path.resolve(path.dirname(__filename), '../../');

const PORT = 5173;
const BASE_URL = process.env.VITE_DEV_SERVER_URL || `http://localhost:${PORT}`;

const mockOrder = {
  id: '00000000-0000-0000-0000-000000000001',
  userId: '00000000-0000-0000-0000-000000000101',
  branchId: '00000000-0000-0000-0000-000000000201',
  fulfillmentType: 'Delivery',
  recipientName: 'Nguyen Van A',
  recipientPhone: '0900000000',
  deliveryAddressSnapshot: '123 Nguyen Trai, Q1',
  subtotal: 100000,
  discountAmount: 0,
  shippingFee: 15000,
  totalAmount: 115000,
  promotionCodeSnapshot: null,
  status: 'Completed',
  createdAtUtc: '2026-08-28T01:00:00Z',
  updatedAtUtc: '2026-08-28T02:00:00Z',
  items: [
    {
      orderItemId: 'oi-test-101',
      productId: 'prod-test-201',
      productName: 'Nước ép cam nguyên chất 1L',
      sku: 'CAM-1L',
      unitPrice: 45000,
      quantity: 2,
      lineTotal: 90000,
      canReview: true,
      reviewId: null,
    },
  ],
  statusHistory: [
    { fromStatus: 'Pending', toStatus: 'Completed', note: 'Giao hàng thành công', createdAtUtc: '2026-08-28T02:00:00Z' },
  ],
  payment: { id: 'pay-1', method: 'COD', status: 'Paid', amount: 115000, providerTransactionId: null, createdAtUtc: '2026-08-28T02:00:00Z' },
};

const mockProduct = {
  id: 'prod-test-201',
  name: 'Nước ép cam nguyên chất 1L',
  slug: 'nuoc-ep-cam-1l',
  sku: 'CAM-1L',
  description: 'Nước ép cam tươi tự nhiên 100%.',
  basePrice: 45000,
  unit: 'chai',
  imageUrl: null,
  categoryId: 'cat-1',
  categoryName: 'Đồ uống',
  categorySlug: 'do-uong',
  brandId: 'brand-1',
  brandName: 'FreshCo',
  branchInventory: { availableQuantity: 50 },
};

function checkServer(url) {
  return new Promise((resolve) => {
    const req = http.get(url, (res) => {
      resolve(res.statusCode >= 200 && res.statusCode < 300);
    });
    req.on('error', () => resolve(false));
    req.setTimeout(1000, () => {
      req.destroy();
      resolve(false);
    });
  });
}

async function ensureDevServer() {
  const isUp = await checkServer(BASE_URL);
  if (isUp) {
    console.log(`Server already running at ${BASE_URL}`);
    return null;
  }

  console.log(`Starting Vite dev server on port ${PORT}...`);
  const viteBin = path.join(frontendDir, 'node_modules', 'vite', 'bin', 'vite.js');
  const serverProc = spawn('node', [viteBin, '--port', String(PORT)], {
    cwd: frontendDir,
    shell: false,
    stdio: 'ignore',
  });

  const start = Date.now();
  while (Date.now() - start < 15000) {
    if (await checkServer(BASE_URL)) {
      console.log(`Vite dev server is ready at ${BASE_URL}`);
      return serverProc;
    }
    await new Promise((r) => setTimeout(r, 500));
  }

  serverProc.kill();
  throw new Error(`Failed to start Vite dev server within 15 seconds at ${BASE_URL}`);
}

async function runE2E() {
  console.log('--- Running Completed Order -> Review Flow E2E Test ---');

  let devServerProc = null;
  let browser = null;

  try {
    devServerProc = await ensureDevServer();
    browser = await chromium.launch({ headless: true });

    const context = await browser.newContext({
      viewport: { width: 1280, height: 800 },
    });

    await context.addInitScript(() => {
      localStorage.setItem('os_access_token', 'mock-jwt-token');
    });

    const page = await context.newPage();

    let reviewCreated = false;

    // Route mocking for isolated, deterministic testing - only intercept backend /api/ endpoints
    await page.route((url) => url.pathname.startsWith('/api/'), async (route) => {
      const url = route.request().url();
      const method = route.request().method();

      if (url.includes('/api/auth/me')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: '00000000-0000-0000-0000-000000000101',
            email: 'user@test.com',
            fullName: 'Nguyen Van A',
            role: 'Customer',
          }),
        });
      }

      if (url.includes('/api/branches')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { id: '00000000-0000-0000-0000-000000000201', name: 'Chi nhánh Quận 1', address: '123 Le Loi, Q1', phone: '0123456789' },
          ]),
        });
      }

      if (url.includes('/api/cart')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'cart-1', items: [], totalQuantity: 0, subtotal: 0 }),
        });
      }

      if (url.includes('/api/orders/00000000-0000-0000-0000-000000000001')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(mockOrder),
        });
      }

      if (url.includes('/api/products/prod-test-201/review-eligibility')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(
            reviewCreated
              ? { canReview: false, orderItemId: null, reviewId: 'rev-created-1', existingRating: 5, existingComment: 'Nước cam rất ngon!' }
              : { canReview: true, orderItemId: 'oi-test-101', reviewId: null }
          ),
        });
      }

      if (url.includes('/api/products/prod-test-201/reviews')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            averageRating: reviewCreated ? 5.0 : 0,
            reviewCount: reviewCreated ? 1 : 0,
            data: reviewCreated
              ? [
                  {
                    id: 'rev-created-1',
                    productId: 'prod-test-201',
                    reviewerName: 'Nguyen Van A',
                    rating: 5,
                    comment: 'Nước cam rất ngon!',
                    createdAtUtc: new Date().toISOString(),
                    updatedAtUtc: new Date().toISOString(),
                  },
                ]
              : [],
            page: 1,
            pageSize: 10,
            totalCount: reviewCreated ? 1 : 0,
          }),
        });
      }

      if (url.includes('/api/products/prod-test-201')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(mockProduct),
        });
      }

      if (url.endsWith('/api/reviews') && method === 'POST') {
        reviewCreated = true;
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'rev-created-1',
            productId: 'prod-test-201',
            reviewerName: 'Nguyen Van A',
            rating: 5,
            comment: 'Nước cam rất ngon!',
            createdAtUtc: new Date().toISOString(),
            updatedAtUtc: new Date().toISOString(),
          }),
        });
      }

      return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    // 1. Navigate to OrderDetailPage
    await page.goto(`${BASE_URL}/orders/history/00000000-0000-0000-0000-000000000001`, {
      timeout: 10000,
      waitUntil: 'networkidle',
    });
    console.log('✅ Loaded OrderDetailPage with authenticated state');

    // 2. Verify "Viết đánh giá" link exists
    const reviewLink = await page.waitForSelector('a.order-item-review-link', { timeout: 5000 });
    const linkText = await reviewLink.textContent();
    if (!linkText || !linkText.includes('Viết đánh giá')) {
      throw new Error(`Expected review link to contain "Viết đánh giá", but got: "${linkText}"`);
    }
    console.log(`✅ Found review action link: "${linkText?.trim()}"`);

    // 3. Click to navigate to Product detail with #reviews hash
    await reviewLink.click();
    await page.waitForURL((url) => url.hash === '#reviews', { timeout: 5000 });
    console.log('✅ Navigated to ProductDetailPage with #reviews hash');

    // 4. Confirm deep-link focus on #reviews section
    await page.waitForFunction(
      () => document.activeElement && document.activeElement.id === 'reviews',
      { timeout: 5000 }
    );
    console.log('✅ Verified #reviews section automatically received focus');

    // 5. Confirm review form is displayed
    const formHeading = await page.waitForSelector('.review-form__title', { timeout: 5000 });
    console.log(`✅ Review form title visible: "${await formHeading.textContent()}"`);

    // 6. Fill comment and submit
    await page.fill('#review-comment', 'Nước cam rất ngon!');
    await page.click('button.review-form__submit-btn');
    console.log('✅ Submitted review form');

    // 7. Confirm review aggregate and card are displayed
    await page.waitForSelector('.product-reviews__avg-score', { timeout: 5000 });
    console.log('✅ Review aggregate updated');

    // 8. Confirm state toggles to "Sửa đánh giá của bạn"
    const editBtn = await page.waitForSelector('.product-reviews__edit-btn', { timeout: 5000 });
    const editBtnText = await editBtn.textContent();
    if (!editBtnText || !editBtnText.includes('Sửa đánh giá')) {
      throw new Error(`Expected edit button to contain "Sửa đánh giá", got: "${editBtnText}"`);
    }
    console.log(`✅ Verified edit button is now displayed: "${editBtnText.trim()}"`);

    console.log('🎉 Completed Order -> Review Flow E2E PASSED ALL ASSERTIONS!');
  } catch (err) {
    console.error('❌ E2E Test FAILED:', err);
    process.exitCode = 1;
    throw err;
  } finally {
    if (browser) await browser.close();
    if (devServerProc) {
      console.log('Stopping spawned Vite dev server...');
      devServerProc.kill();
    }
  }
}

async function main() {
  await runE2E();
  await runForecastE2E();
}

main().catch((error) => {
  console.error('❌ GUI test run failed:', error);
  process.exit(1);
});

async function runForecastE2E() {
  console.log('--- Running Inventory -> Forecast Admin Flow E2E Test ---');

  let devServerProc = null;
  let browser = null;

  try {
    devServerProc = await ensureDevServer();
    browser = await chromium.launch({ headless: true });

    const context = await browser.newContext({
      viewport: { width: 1280, height: 800 },
    });

    await context.addInitScript(() => {
      localStorage.setItem('os_access_token', 'mock-jwt-token');
    });

    const page = await context.newPage();

    const forecastRows = [
      {
        id: 'fcst-e2e-1',
        branchInventoryId: 'inv-1',
        productId: 'prod-fc-1',
        productName: 'Nước ép cam nguyên chất 1L',
        horizonDays: 7,
        predictedQuantity: 14,
        actualDataDays: 28,
        dataQuality: 'Sufficient',
        forecastStartDate: '2026-08-05',
        forecastEndDate: '2026-09-01',
        generatedAtUtc: '2026-09-04T01:00:00Z',
        jobRunId: 'job-fc-00001',
      },
    ];

    let queued = false;

    await page.route((url) => url.pathname.startsWith('/api/'), async (route) => {
      const url = route.request().url();
      const method = route.request().method();

      if (url.includes('/api/auth/me')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: '00000000-0000-0000-0000-000000000901',
            email: 'admin@test.com',
            fullName: 'Admin',
            role: 'Admin',
          }),
        });
      }

      if (url.includes('/api/branches')) {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { id: '00000000-0000-0000-0000-000000000801', name: 'Chi nhánh Quận 1', address: '123 Le Loi', phone: '0123456789' },
          ]),
        });
      }

      if (url.includes('/api/admin/forecast') && method === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(forecastRows),
        });
      }

      if (url.includes('/api/admin/jobs/forecast/runs') && method === 'POST') {
        queued = true;
        return route.fulfill({
          status: 202,
          contentType: 'application/json',
          body: JSON.stringify({ jobRunId: 'job-fc-00002', statusUrl: '/api/admin/jobs/job-fc-00002' }),
        });
      }

      if (url.includes('/api/admin/jobs/job-fc-00002') && method === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'job-fc-00002',
            jobName: 'Forecast',
            status: 'Succeeded',
            createdAtUtc: '2026-09-04T01:00:00Z',
            startedAtUtc: '2026-09-04T01:00:01Z',
            completedAtUtc: '2026-09-04T01:00:05Z',
            errorSummary: null,
          }),
        });
      }

      return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await page.goto(`${BASE_URL}/admin/forecast`, { timeout: 10000, waitUntil: 'networkidle' });

    const table = await page.waitForSelector('table[aria-label="Dự báo nhu cầu"]', { timeout: 5000 });
    const tableText = await table.textContent();
    if (!tableText || !tableText.includes('Nước ép cam nguyên chất 1L')) {
      throw new Error('Expected forecast table to contain the forecasted product.');
    }
    if (!tableText || !tableText.includes('Đủ')) {
      throw new Error('Expected forecast table to show the Sufficient quality label.');
    }
    console.log('✅ Forecast result table rendered with product and quality label');

    await page.click('button:has-text("Chạy lại dự báo")');
    await page.waitForSelector('.admin-note--ok', { timeout: 5000 });
    const noteText = await page.locator('.admin-note--ok').textContent();
    if (!noteText || !noteText.includes('Đã đưa vào hàng đợi')) {
      throw new Error(`Expected queued confirmation note, got: "${noteText}"`);
    }
    console.log('✅ Manual forecast run queued successfully');

    console.log('🎉 Inventory -> Forecast Admin Flow E2E PASSED ALL ASSERTIONS!');
  } catch (err) {
    console.error('❌ Forecast E2E Test FAILED:', err);
    process.exitCode = 1;
    throw err;
  } finally {
    if (browser) await browser.close();
    if (devServerProc) {
      console.log('Stopping spawned Vite dev server...');
      devServerProc.kill();
    }
  }
}
