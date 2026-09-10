import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AdminRecommendationsPage } from './AdminRecommendationsPage'
import { recommendationApi } from '../../api/recommendationApi'
import { ApiError } from '../../api/httpClient'

const mockAuth = { accessToken: 'jwt-token' as string | null }

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => mockAuth,
}))

const sampleResponse = {
  jobRunId: 'job-1234567890abcdef',
  generatedAtUtc: '2026-09-04T07:00:00Z',
  expiresAtUtc: '2026-09-04T08:00:00Z',
  algorithmVersion: 'content-v1',
  items: [
    {
      productId: 'prod-a',
      scope: 'Global',
      audienceKey: 'global',
      score: 0.9,
      rank: 1,
      reason: 'Được nhiều người quan tâm',
    },
    {
      productId: 'prod-b',
      scope: 'User',
      audienceKey: 'user:u1',
      score: 0.8,
      rank: 1,
      reason: 'Phù hợp danh mục đã xem',
    },
  ],
}

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminRecommendationsPage />
    </MemoryRouter>,
  )
}

describe('AdminRecommendationsPage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders the sample rows and algorithm metadata', async () => {
    vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue(sampleResponse)

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('Được nhiều người quan tâm')).toBeInTheDocument()
      expect(screen.getByText('Phù hợp danh mục đã xem')).toBeInTheDocument()
    })
    const scopeCells = screen.getAllByText('Toàn hệ thống')
    expect(scopeCells.length).toBeGreaterThan(1)
    expect(screen.getByText(/content-v1/)).toBeInTheDocument()
  })

  it('requests only the selected scope', async () => {
    const getSample = vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue({
      ...sampleResponse,
      items: [sampleResponse.items[0]],
    })

    renderPage()
    await screen.findByText('Toàn hệ thống')

    await userEvent.selectOptions(screen.getByLabelText('Phạm vi'), 'User')

    await waitFor(() => {
      expect(getSample).toHaveBeenLastCalledWith(
        expect.objectContaining({ scope: 'User', token: 'jwt-token' }),
      )
    })
  })

  it('shows an empty state before any materialized run', async () => {
    vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue({
      jobRunId: '00000000-0000-0000-0000-000000000000',
      generatedAtUtc: '0001-01-01T00:00:00Z',
      expiresAtUtc: '0001-01-01T00:00:00Z',
      algorithmVersion: 'content-v1',
      items: [],
    })

    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/Chưa có dữ liệu gợi ý/)).toBeInTheDocument()
    })
  })

  it('shows a conflict message when a run is already active', async () => {
    vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue(sampleResponse)
    vi.spyOn(recommendationApi, 'triggerRun').mockRejectedValue(
      new ApiError(409, { message: 'already active' }),
    )

    renderPage()
    await screen.findByText('Toàn hệ thống')

    await userEvent.click(screen.getByRole('button', { name: 'Chạy lại lần tính' }))

    await waitFor(() => {
      expect(screen.getByText(/đang hoạt động/)).toBeInTheDocument()
    })
  })

  it('accepts a manual run, polls until Succeeded, and refreshes the sample', async () => {
    const getSample = vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue(sampleResponse)
    vi.spyOn(recommendationApi, 'triggerRun').mockResolvedValue({
      jobRunId: 'job-new-123',
      statusUrl: '/api/admin/jobs/job-new-123',
    })
    vi.spyOn(recommendationApi, 'getJobRun').mockResolvedValue({
      id: 'job-new-123',
      jobName: 'Recommendations',
      status: 'Succeeded',
      createdAtUtc: '2026-09-08T05:00:00Z',
      startedAtUtc: '2026-09-08T05:00:01Z',
      completedAtUtc: '2026-09-08T05:00:03Z',
      errorSummary: null,
    })

    renderPage()
    await screen.findByText('Toàn hệ thống')
    const callsBefore = getSample.mock.calls.length

    await userEvent.click(screen.getByRole('button', { name: 'Chạy lại lần tính' }))

    await waitFor(() => {
      expect(screen.getByText(/Đã đưa vào hàng đợi/)).toBeInTheDocument()
    })
    await waitFor(() => {
      expect(screen.getByText(/hoàn thành thành công/)).toBeInTheDocument()
    }, { timeout: 3000 })
    await waitFor(() => {
      expect(getSample.mock.calls.length).toBeGreaterThan(callsBefore)
    })
  })

  it('polls the run until Failed and unblocks UI with error message', async () => {
    vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue(sampleResponse)
    vi.spyOn(recommendationApi, 'triggerRun').mockResolvedValue({
      jobRunId: 'job-failed-456',
      statusUrl: '/api/admin/jobs/job-failed-456',
    })
    vi.spyOn(recommendationApi, 'getJobRun').mockResolvedValue({
      id: 'job-failed-456',
      jobName: 'Recommendations',
      status: 'Failed',
      createdAtUtc: '2026-09-08T05:00:00Z',
      startedAtUtc: '2026-09-08T05:00:01Z',
      completedAtUtc: '2026-09-08T05:00:03Z',
      errorSummary: 'Trainer matrix factorization error',
    })

    renderPage()
    await screen.findByText('Toàn hệ thống')

    const button = screen.getByRole('button', { name: 'Chạy lại lần tính' })
    await userEvent.click(button)

    await waitFor(() => {
      expect(screen.getByText(/Đã đưa vào hàng đợi/)).toBeInTheDocument()
    })
    await waitFor(() => {
      expect(screen.getByText(/Lượt chạy thất bại: Trainer matrix factorization error/)).toBeInTheDocument()
    }, { timeout: 3000 })
    expect(button).not.toBeDisabled()
  })

  it('exits waiting state and displays error when triggerRun fails', async () => {
    vi.spyOn(recommendationApi, 'getAdminSample').mockResolvedValue(sampleResponse)
    vi.spyOn(recommendationApi, 'triggerRun').mockRejectedValue(
      new Error('Internal server error'),
    )

    renderPage()
    await screen.findByText('Toàn hệ thống')

    const button = screen.getByRole('button', { name: 'Chạy lại lần tính' })
    await userEvent.click(button)

    await waitFor(() => {
      expect(screen.getByText(/Không thể kích hoạt lượt chạy/)).toBeInTheDocument()
    })
    expect(button).not.toBeDisabled()
  })
})
