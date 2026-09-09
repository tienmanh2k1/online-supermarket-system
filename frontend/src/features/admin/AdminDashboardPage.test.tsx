import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { adminApi, type DashboardSummaryDto } from '../../api/adminApi'
import { useAuth } from '../auth/AuthContext'
import { AdminDashboardPage } from './AdminDashboardPage'

vi.mock('../../api/adminApi', () => ({
  adminApi: {
    getDashboardSummary: vi.fn(),
  },
}))

vi.mock('../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}))

const mockSummary: DashboardSummaryDto = {
  totalOrders: 42,
  pendingOrders: 5,
  completedRevenue: 15500000,
  lowStockItems: 3,
  recentOrders: [
    {
      id: 'ord-001',
      createdAtUtc: '2026-09-08T10:00:00Z',
      totalAmount: 350000,
      status: 'Pending',
      fulfillmentType: 'Delivery',
      itemCount: 2,
    },
    {
      id: 'ord-002',
      createdAtUtc: '2026-09-08T11:00:00Z',
      totalAmount: 1200000,
      status: 'Completed',
      fulfillmentType: 'Pickup',
      itemCount: 4,
    },
  ],
}

describe('AdminDashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'admin-1', role: 'Admin', email: 'admin@test.com', fullName: 'Admin' },
      accessToken: 'test-admin-token',
      isAuthenticated: true,
      isLoading: false,
    } as any)
  })

  it('renders loading state with aria-busy while fetching', () => {
    vi.mocked(adminApi.getDashboardSummary).mockReturnValue(new Promise(() => {}))

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    expect(screen.getByRole('region', { name: /tổng quan/i })).toHaveAttribute('aria-busy', 'true')
  })

  it('renders ready state with four metrics and recent order links', async () => {
    vi.mocked(adminApi.getDashboardSummary).mockResolvedValue(mockSummary)

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    expect(await screen.findByText('42')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText(/tổng doanh thu đơn hoàn tất/i)).toBeInTheDocument()
    expect(screen.getByText(/15\.500\.000/)).toBeInTheDocument()

    const orderLink = screen.getByRole('link', { name: /ord-001/i })
    expect(orderLink).toHaveAttribute('href', '/admin/orders/ord-001')
  })

  it('renders empty state when there are no recent orders', async () => {
    const emptySummary: DashboardSummaryDto = {
      totalOrders: 0,
      pendingOrders: 0,
      completedRevenue: 0,
      lowStockItems: 0,
      recentOrders: [],
    }
    vi.mocked(adminApi.getDashboardSummary).mockResolvedValue(emptySummary)

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    expect(await screen.findByText(/chưa có đơn hàng nào/i)).toBeInTheDocument()
  })

  it('renders error alert with retry button and re-fetches on retry click', async () => {
    vi.mocked(adminApi.getDashboardSummary)
      .mockRejectedValueOnce(new Error('Network failure'))
      .mockResolvedValueOnce(mockSummary)

    render(
      <MemoryRouter>
        <AdminDashboardPage />
      </MemoryRouter>
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/network failure/i)

    const retryBtn = screen.getByRole('button', { name: /thử lại/i })
    fireEvent.click(retryBtn)

    expect(await screen.findByText('42')).toBeInTheDocument()
    expect(adminApi.getDashboardSummary).toHaveBeenCalledTimes(2)
  })
})
