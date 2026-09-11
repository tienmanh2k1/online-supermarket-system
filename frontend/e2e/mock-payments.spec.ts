import { test, expect } from 'playwright/test'

const outcomes = [
  { provider: 'VNPay', outcome: 'Success', button: 'Thanh toán thành công', paymentStatus: 'Completed', orderStatus: 'Confirmed' },
  { provider: 'VNPay', outcome: 'Failed', button: 'Thanh toán thất bại', paymentStatus: 'Failed', orderStatus: 'Cancelled' },
  { provider: 'VNPay', outcome: 'Cancelled', button: 'Hủy thanh toán', paymentStatus: 'Failed', orderStatus: 'Cancelled' },
  { provider: 'MoMo', outcome: 'Success', button: 'Thanh toán thành công', paymentStatus: 'Completed', orderStatus: 'Confirmed' },
  { provider: 'MoMo', outcome: 'Failed', button: 'Thanh toán thất bại', paymentStatus: 'Failed', orderStatus: 'Cancelled' },
  { provider: 'MoMo', outcome: 'Cancelled', button: 'Hủy thanh toán', paymentStatus: 'Failed', orderStatus: 'Cancelled' },
] as const

for (const scenario of outcomes) {
  test(`Mock ${scenario.provider} ${scenario.outcome} completes through the UI`, async ({ page, playwright, baseURL }) => {
    const suffix = `${scenario.provider}-${scenario.outcome}-${Date.now()}`
    const email = `mock-e2e-${suffix}@test.com`
    const password = 'MockE2e@123'
    const api = await playwright.request.newContext({ baseURL })

    const register = await api.post('/api/auth/register', {
      data: { fullName: `Mock E2E ${suffix}`, email, password },
    })
    expect(register.ok(), await register.text()).toBeTruthy()
    const login = await api.post('/api/auth/login', { data: { email, password } })
    expect(login.ok(), await login.text()).toBeTruthy()
    const { accessToken, refreshToken } = await login.json()
    const customerApi = await playwright.request.newContext({
      baseURL,
      extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` },
    })

    const branches = await api.get('/api/branches')
    expect(branches.ok(), await branches.text()).toBeTruthy()
    const branchId = (await branches.json())[0].id
    const products = await api.get(`/api/products?branchId=${branchId}&pageSize=1`)
    expect(products.ok(), await products.text()).toBeTruthy()
    const productId = (await products.json()).data[0].id

    await page.addInitScript(({ token, refresh }) => {
      localStorage.setItem('os_access_token', token)
      localStorage.setItem('os_refresh_token', refresh)
    }, { token: accessToken, refresh: refreshToken })
    const addToCart = await customerApi.post('/api/cart/items', { data: { productId, quantity: 1 } })
    expect(addToCart.ok(), await addToCart.text()).toBeTruthy()
    await page.goto('/shopping/checkout')
    await expect(page.locator('.checkout-form')).toBeVisible()
    await page.locator(`input[value="${scenario.provider}"]`).check()
    await page.locator('.checkout-submit-btn').click()
    await expect(page).toHaveURL(/\/shopping\/payment\/mock\//)
    await expect(page.getByRole('heading', { name: 'Thanh toán giả lập — không thu tiền' })).toBeVisible()
    await page.getByRole('button', { name: scenario.button }).click()
    await expect(page.getByRole('link', { name: 'Xem đơn hàng' })).toBeVisible()

    const paymentId = page.url().split('/').pop()!
    const payment = await customerApi.get(`/api/payments/mock/${paymentId}`)
    expect(payment.ok(), await payment.text()).toBeTruthy()
    await expect(payment.json()).resolves.toMatchObject({
      paymentStatus: scenario.paymentStatus,
      orderStatus: scenario.orderStatus,
      outcome: scenario.outcome,
    })
    await customerApi.dispose()
    await api.dispose()
  })
}
