import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AdminForecastPage } from './AdminForecastPage'
import { inventoryIntelligenceApi } from '../../api/inventoryIntelligenceApi'
import { branchApi } from '../../api/branchApi'
import { ApiError } from '../../api/httpClient'

const mockAuth = { accessToken: 'jwt-token' as string | null }

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

const branches = [
  { id: 'b-1', name: 'Chi nhánh Quận 1', address: '1 Test St', phone: null, latitude: null, longitude: null, isActive: true },
]

const forecastRows = [
  {
    id: 'fcst-1',
    branchInventoryId: 'inv-1',
    productId: 'prod-a',
    productName: 'Nước ép cam 1L',
    horizonDays: 7,
    predictedQuantity: 14,
    actualDataDays: 28,
    dataQuality: 'Sufficient',
    forecastStartDate: '2026-08-05',
    forecastEndDate: '2026-09-01',
    generatedAtUtc: '2026-09-04T01:00:00Z',
    jobRunId: 'job-00001',
  },
  {
    id: 'fcst-2',
    branchInventoryId: 'inv-2',
    productId: 'prod-b',
    productName: 'Sữa tươi 1L',
    horizonDays: 7,
    predictedQuantity: 7,
    actualDataDays: 10,
    dataQuality: 'Partial',
    forecastStartDate: '2026-08-05',
    forecastEndDate: '2026-09-01',
    generatedAtUtc: '2026-09-04T01:00:00Z',
    jobRunId: 'job-00001',
  },
]

const forecastRun = {
  id: 'job-00001',
  jobName: 'Forecast',
  status: 'Succeeded',
  createdAtUtc: '2026-09-04T01:00:00Z',
  startedAtUtc: '2026-09-04T01:00:01Z',
  completedAtUtc: '2026-09-04T01:00:05Z',
  errorSummary: null,
}

const runHistoryResponse = {
  items: [forecastRun],
  totalCount: 1,
  page: 1,
  pageSize: 20,
}

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminForecastPage />
    </MemoryRouter>,
  )
}

function stubForecastApi() {
  vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
  vi.spyOn(inventoryIntelligenceApi, 'getForecast').mockResolvedValue(forecastRows)
  vi.spyOn(inventoryIntelligenceApi, 'getForecastRuns').mockResolvedValue(runHistoryResponse)
}

describe('AdminForecastPage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders forecast rows with quality labels', async () => {
    stubForecastApi()

    renderPage()

    await waitFor(() => {
      expect(screen.getByRole('table', { name: 'Dự báo nhu cầu' })).toBeInTheDocument()
    })
    expect(screen.getByText('Nước ép cam 1L')).toBeInTheDocument()
    expect(screen.getByText('Đủ')).toBeInTheDocument()
    expect(screen.getByText('Phần hoàn')).toBeInTheDocument()
  })

  it('renders the latest run status and history for the branch', async () => {
    stubForecastApi()

    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/Lượt gần nhất #job-0000/)).toBeInTheDocument()
    })
    expect(screen.getByRole('table', { name: 'Lịch sử lượt dự báo' })).toBeInTheDocument()
    expect(screen.getAllByText('Succeeded').length).toBeGreaterThan(0)
  })

  it('switches between strict 7 and 14 day forecasts', async () => {
    stubForecastApi()

    renderPage()
    await screen.findByRole('table', { name: 'Dự báo nhu cầu' })

    await userEvent.selectOptions(screen.getByLabelText('Kỳ dự báo'), '14')

    await waitFor(() => {
      expect(inventoryIntelligenceApi.getForecast).toHaveBeenLastCalledWith(
        'b-1',
        14,
        expect.objectContaining({ token: 'jwt-token' }),
      )
    })
  })

  it('shows a loading state while fetching', async () => {
    stubForecastApi()

    renderPage()

    expect(screen.getByRole('main', { name: 'Dự báo nhu cầu' })).toBeInTheDocument()
    expect(screen.getByText('Đang tải dự báo nhu cầu...')).toBeInTheDocument()
  })

  it('shows an empty state before any materialized run', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(inventoryIntelligenceApi, 'getForecast').mockResolvedValue([])

    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/Chưa có dự báo/)).toBeInTheDocument()
    })
  })

  it('shows error state with retry', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(inventoryIntelligenceApi, 'getForecast').mockRejectedValue(
      new ApiError(500, { message: 'boom' }),
    )

    renderPage()

    await screen.findByText('Không thể tải dự báo. Vui lòng thử lại.')
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument()
  })

  it('shows 409 without losing the current result table', async () => {
    stubForecastApi()
    vi.spyOn(inventoryIntelligenceApi, 'triggerForecast').mockRejectedValue(
      new ApiError(409, { message: 'active' }),
    )

    renderPage()
    await screen.findByRole('table', { name: 'Dự báo nhu cầu' })

    await userEvent.click(screen.getByRole('button', { name: 'Chạy lại dự báo' }))

    await waitFor(() => {
      expect(screen.getByText(/Dự báo đang chạy/)).toBeInTheDocument()
    })
    expect(screen.getByRole('table', { name: 'Dự báo nhu cầu' })).toBeInTheDocument()
  })

  it('accepts a manual run and reports the queued job', async () => {
    stubForecastApi()
    vi.spyOn(inventoryIntelligenceApi, 'triggerForecast').mockResolvedValue({
      jobRunId: 'job-new-1',
      statusUrl: '/api/admin/jobs/job-new-1',
    })
    vi.spyOn(inventoryIntelligenceApi, 'getForecastRun').mockResolvedValue({
      id: 'job-new-1',
      jobName: 'Forecast',
      status: 'Queued',
      createdAtUtc: '2026-09-04T01:00:00Z',
      startedAtUtc: null,
      completedAtUtc: null,
      errorSummary: null,
    })

    renderPage()
    await screen.findByRole('table', { name: 'Dự báo nhu cầu' })

    await userEvent.click(screen.getByRole('button', { name: 'Chạy lại dự báo' }))

    await waitFor(() => {
      expect(screen.getByText(/Đã đưa vào hàng đợi/)).toBeInTheDocument()
    })
  })
})