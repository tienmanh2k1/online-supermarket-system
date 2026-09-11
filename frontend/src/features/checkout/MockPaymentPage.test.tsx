import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { PropsWithChildren } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MockPaymentPage } from './MockPaymentPage'

const get = vi.hoisted(vi.fn)
const complete = vi.hoisted(vi.fn)

vi.mock('../../api/mockPaymentApi', () => ({
  mockPaymentApi: { get, complete },
}))

vi.mock('../auth/AuthContext', () => ({
  AuthProvider: ({ children }: PropsWithChildren) => <>{children}</>,
  useAuth: () => ({ isAuthenticated: true, accessToken: 'jwt-token' }),
}))

const pending = {
  paymentId: '00000000-0000-0000-0000-000000000001',
  orderId: '00000000-0000-0000-0000-000000000002',
  method: 'MoMo',
  amount: 100000,
  paymentStatus: 'Pending',
  orderStatus: 'Pending',
  isMock: true,
  outcome: null,
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/shopping/payment/mock/00000000-0000-0000-0000-000000000001']}>
      <Routes><Route path="/shopping/payment/mock/:paymentId" element={<MockPaymentPage />} /></Routes>
    </MemoryRouter>,
  )
}

describe('MockPaymentPage', () => {
  beforeEach(() => get.mockResolvedValue(pending))
  afterEach(() => vi.clearAllMocks())

  it('renders persisted provider, amount, and the no-money warning', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: 'Thanh toán giả lập — không thu tiền' })).toBeInTheDocument()
    expect(screen.getByText(/MoMo/)).toBeInTheDocument()
    expect(screen.getByText(/100\.000/)).toBeInTheDocument()
  })

  it.each(['Success', 'Failed', 'Cancelled'] as const)('submits %s once and renders its terminal state', async (outcome) => {
    const user = userEvent.setup()
    complete.mockResolvedValueOnce({
      ...pending,
      paymentStatus: outcome === 'Success' ? 'Completed' : 'Failed',
      orderStatus: outcome === 'Success' ? 'Confirmed' : 'Cancelled',
      outcome,
    })
    renderPage()
    await screen.findByRole('button', { name: 'Thanh toán thành công' })
    await user.click(screen.getByRole('button', {
      name: outcome === 'Success' ? 'Thanh toán thành công' : outcome === 'Failed' ? 'Thanh toán thất bại' : 'Hủy thanh toán',
    }))
    expect(complete).toHaveBeenCalledWith(pending.paymentId, outcome, 'jwt-token')
    expect(await screen.findByRole('link', { name: 'Xem đơn hàng' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Thanh toán thành công' })).not.toBeInTheDocument()
  })
})
