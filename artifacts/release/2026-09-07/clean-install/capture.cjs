const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const base = 'http://localhost:5178';
  const log = [];
  try {
    await page.goto(base + '/', { waitUntil: 'networkidle' }).catch(() => {});
    await page.waitForTimeout(1200);
    await page.screenshot({ path: 'home.png' });
    log.push('home captured ' + page.url());

    await page.click('button.btn-login:has-text("Đăng nhập")').catch(() => {});
    await page.waitForSelector('#login-email', { timeout: 10000 });
    await page.fill('#login-email', 'user1@test.com');
    await page.fill('#login-password', 'Test@123');
    await page.screenshot({ path: 'login-filled.png' });
    log.push('login form filled');

    await Promise.all([
      page.waitForNavigation({ waitUntil: 'networkidle', timeout: 15000 }).catch(() => {}),
      page.click('button[type="submit"]')
    ]).catch(() => {});
    await page.waitForTimeout(2500);
    await page.screenshot({ path: 'after-login.png' });
    log.push('after login url=' + page.url());
  } catch (e) {
    log.push('ERROR: ' + e.message);
  }
  console.log(log.join('\n'));
  await browser.close();
})();