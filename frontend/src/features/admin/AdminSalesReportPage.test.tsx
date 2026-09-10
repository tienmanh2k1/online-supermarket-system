import { act, render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { adminApi, type SalesReportDto } from '../../api/adminApi'
import { useAuth } from '../auth/AuthContext'
import { AdminSalesReportPage } from './AdminSalesReportPage'
import { ApiError } from '../../api/httpClient'

vi.mock('../../api/adminApi', () => ({
  adminApi: {
    getSalesReport: vi.fn(),
  },
}))

vi.mock('../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}))

const mockReport30Days: SalesReportDto = {
  from: '2026-08-11',
  to: '2026-09-09',
  totalRevenue: 5000000,
  completedOrderCount: 10,
  averageOrderValue: 500000,
  daily: [
    { date: '2026-09-08', revenue: 2000000, orderCount: 4 },
    { date: '2026-09-09', revenue: 3000000, orderCount: 6 },
  ],
}

describe('AdminSalesReportPage', () => {
  it.each([
    [{ detail: 'Detailed validation error', title: 'Invalid range' }, 'Detailed validation error'],
    [{ title: 'Invalid range' }, 'Invalid range'],
  ])('displays ProblemDetails from ApiError.data: %j', async (body, expected) => {
    vi.mocked(adminApi.getSalesReport).mockRejectedValueOnce(new ApiError(400, body))
    render(<AdminSalesReportPage />)
    expect(await screen.findByRole('alert')).toHaveTextContent(expected)
  })

  it.each(['success', 'failure'])('ignores stale %s after the latest request succeeds', async (outcome) => {
    let resolveOld!: (report: SalesReportDto) => void
    let rejectOld!: (error: Error) => void
    vi.mocked(adminApi.getSalesReport)
      .mockImplementationOnce(() => new Promise((resolve, reject) => {
        resolveOld = resolve
        rejectOld = reject
      }))
      .mockResolvedValueOnce({ ...mockReport30Days, completedOrderCount: 7 })
    render(<AdminSalesReportPage />)
    const oldSignal = vi.mocked(adminApi.getSalesReport).mock.calls[0][3]
    fireEvent.click(screen.getByRole('button', { name: '7 ngày' }))
    await screen.findByText('7 đơn hàng hoàn tất')
    expect(oldSignal?.aborted).toBe(true)
    await act(async () => {
      if (outcome === 'success') resolveOld(mockReport30Days)
      else rejectOld(new ApiError(500, undefined, 'Old request failed'))
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('7 đơn hàng hoàn tất')).toBeInTheDocument()
  })

  it('aborts the pending request when unmounted', () => {
    vi.mocked(adminApi.getSalesReport).mockImplementationOnce(() => new Promise(() => {}))
    const { unmount } = render(<AdminSalesReportPage />)
    const signal = vi.mocked(adminApi.getSalesReport).mock.calls[0][3]
    unmount()
    expect(signal?.aborted).toBe(true)
  })

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-09-09T12:00:00Z'))
    vi.clearAllMocks()
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'admin-1', role: 'Admin', email: 'admin@test.com', fullName: 'Admin' },
      accessToken: 'test-admin-token',
      isAuthenticated: true,
      isLoading: false,
    } as any)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('defaults to 30 days UTC, displays note and KPIs', async () => {
    vi.mocked(adminApi.getSalesReport).mockResolvedValue(mockReport30Days)

    render(
      <MemoryRouter>
        <AdminSalesReportPage />
      </MemoryRouter>
    )

    expect(
      screen.getByText(/nhóm theo ngày tạo đơn \(utc\)/i)
    ).toBeInTheDocument()

    await waitFor(() => {
      expect(adminApi.getSalesReport).toHaveBeenCalledWith(
        '2026-08-11',
        '2026-09-09',
        'test-admin-token',
        expect.anything()
      )
    })

    expect(await screen.findByText('10')).toBeInTheDocument()
    expect(screen.getByText(/5\.000\.000/)).toBeInTheDocument()
    expect(screen.getByText(/500\.000/)).toBeInTheDocument()
    expect(screen.getByText('2026-09-08')).toBeInTheDocument()
  })

  it('switches to 7 days preset and reloads', async () => {
    vi.mocked(adminApi.getSalesReport).mockResolvedValue(mockReport30Days)

    render(
      <MemoryRouter>
        <AdminSalesReportPage />
      </MemoryRouter>
    )

    await screen.findByText('10')

    const btn7Days = screen.getByRole('button', { name: /7 ngày/i })
    fireEvent.click(btn7Days)

    await waitFor(() => {
      expect(adminApi.getSalesReport).toHaveBeenCalledWith(
        '2026-09-03',
        '2026-09-09',
        'test-admin-token',
        expect.anything()
      )
    })
  })

  it('validates from > to and does not call API with invalid dates', async () => {
    vi.mocked(adminApi.getSalesReport).mockResolvedValue(mockReport30Days)

    render(
      <MemoryRouter>
        <AdminSalesReportPage />
      </MemoryRouter>
    )

    await screen.findByText('10')
    vi.clearAllMocks()

    const fromInput = screen.getByLabelText(/từ ngày/i)
    const toInput = screen.getByLabelText(/đến ngày/i)

    fireEvent.change(fromInput, { target: { value: '2026-09-15' } })
    fireEvent.change(toInput, { target: { value: '2026-09-09' } })

    const submitBtn = screen.getByRole('button', { name: /áp dụng|lọc|xem báo cáo/i })
    fireEvent.click(submitBtn)

    expect(
      await screen.findByText(/ngày bắt đầu không được lớn hơn ngày kết thúc/i)
    ).toBeInTheDocument()
    expect(adminApi.getSalesReport).not.toHaveBeenCalled()
  })

  it('renders table even when completedOrderCount is 0', async () => {
    const zeroReport: SalesReportDto = {
      from: '2026-08-11',
      to: '2026-09-09',
      totalRevenue: 0,
      completedOrderCount: 0,
      averageOrderValue: 0,
      daily: [
        { date: '2026-09-08', revenue: 0, orderCount: 0 },
        { date: '2026-09-09', revenue: 0, orderCount: 0 },
      ],
    }
    vi.mocked(adminApi.getSalesReport).mockResolvedValue(zeroReport)

    render(
      <MemoryRouter>
        <AdminSalesReportPage />
      </MemoryRouter>
    )

    expect(await screen.findByText('0 đơn hàng hoàn tất')).toBeInTheDocument()
    expect(screen.getByText('2026-09-08')).toBeInTheDocument()
  })

  it('handles error state and retries fetch', async () => {
    vi.mocked(adminApi.getSalesReport)
      .mockRejectedValueOnce(new Error('Server error'))
      .mockResolvedValueOnce(mockReport30Days)

    render(
      <MemoryRouter>
        <AdminSalesReportPage />
      </MemoryRouter>
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/server error/i)

    const retryBtn = screen.getByRole('button', { name: /thử lại/i })
    fireEvent.click(retryBtn)

    expect(await screen.findByText('10')).toBeInTheDocument()
    expect(adminApi.getSalesReport).toHaveBeenCalledTimes(2)
  })
})
