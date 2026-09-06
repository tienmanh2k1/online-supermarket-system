import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductReviews } from './ProductReviews'
import { reviewApi, type ProductReviewsDto } from '../../api/reviewApi'

vi.mock('../../api/reviewApi', () => ({
  reviewApi: {
    getProductReviews: vi.fn(),
    getEligibility: vi.fn(),
    getReviewById: vi.fn(),
    createReview: vi.fn(),
    updateReview: vi.fn(),
  },
}))

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    accessToken: 'test-token',
  }),
}))

describe('ProductReviews', () => {
  const sampleReviewsResponse: ProductReviewsDto = {
    averageRating: 4.5,
    reviewCount: 2,
    data: [
      {
        id: 'rev-1',
        productId: 'p-1',
        reviewerName: 'Nguyen Van A',
        rating: 5,
        comment: 'Sản phẩm tuyệt vời',
        createdAtUtc: '2026-09-01T00:00:00Z',
        updatedAtUtc: '2026-09-01T00:00:00Z',
      },
      {
        id: 'rev-2',
        productId: 'p-1',
        reviewerName: 'Tran Van B',
        rating: 4,
        comment: 'Rất tốt',
        createdAtUtc: '2026-09-02T00:00:00Z',
        updatedAtUtc: '2026-09-02T00:00:00Z',
      },
    ],
    page: 1,
    pageSize: 10,
    totalCount: 2,
  }

  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders loading indicator initially then displays reviews and aggregate rating', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" />)

    expect(screen.getByText('Đang tải đánh giá...')).toBeInTheDocument()

    expect(await screen.findByText('4,5/5')).toBeInTheDocument()
    expect(screen.getByText('(2 đánh giá)')).toBeInTheDocument()
    expect(screen.getByText('Sản phẩm tuyệt vời')).toBeInTheDocument()
    expect(screen.getByText('Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByText('Tran Van B')).toBeInTheDocument()
  })

  it('renders empty state when there are no reviews', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue({
      averageRating: 0,
      reviewCount: 0,
      data: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
    })
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" />)

    expect(await screen.findByText('Chưa có đánh giá nào cho sản phẩm này.')).toBeInTheDocument()
  })

  it('renders error state and retry button on fetch failure', async () => {
    vi.mocked(reviewApi.getProductReviews).mockRejectedValueOnce(new Error('Network error'))
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" />)

    expect(await screen.findByText('Không thể tải đánh giá sản phẩm.')).toBeInTheDocument()
    const retryBtn = screen.getByRole('button', { name: 'Thử lại' })
    expect(retryBtn).toBeInTheDocument()

    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    await userEvent.click(retryBtn)

    expect(await screen.findByText('4,5/5')).toBeInTheDocument()
  })

  it('renders review form when user is eligible to review', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: true,
      orderItemId: 'oi-123',
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" />)

    expect(await screen.findByText('Viết đánh giá sản phẩm')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Gửi đánh giá' })).toBeInTheDocument()
  })

  it('submits a new review and triggers reload', async () => {
    const user = userEvent.setup()
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: true,
      orderItemId: 'oi-123',
      reviewId: null,
    })
    vi.mocked(reviewApi.createReview).mockResolvedValue({
      id: 'rev-3',
      productId: 'p-1',
      reviewerName: 'Me',
      rating: 5,
      comment: 'Quá đỉnh!',
      createdAtUtc: '2026-09-03T00:00:00Z',
      updatedAtUtc: '2026-09-03T00:00:00Z',
    })

    render(<ProductReviews productId="p-1" />)

    const submitBtn = await screen.findByRole('button', { name: 'Gửi đánh giá' })
    const textarea = screen.getByPlaceholderText(/Hãy chia sẻ trải nghiệm/i)

    await user.type(textarea, 'Quá đỉnh!')
    await user.click(submitBtn)

    expect(await screen.findByText('Gửi đánh giá thành công!')).toBeInTheDocument()
    expect(reviewApi.createReview).toHaveBeenCalledWith('test-token', {
      orderItemId: 'oi-123',
      rating: 5,
      comment: 'Quá đỉnh!',
    })
  })

  it('renders reviewed-box when user has already reviewed and allows toggling edit mode', async () => {
    const user = userEvent.setup()
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: 'rev-1',
    })

    render(<ProductReviews productId="p-1" />)

    expect(await screen.findByText('Bạn đã gửi đánh giá cho sản phẩm này.')).toBeInTheDocument()
    const editBtn = await screen.findByRole('button', { name: 'Sửa đánh giá của bạn' })
    await user.click(editBtn)

    expect(await screen.findByRole('button', { name: 'Cập nhật đánh giá' })).toBeInTheDocument()
  })

  it('pre-fills edit form from eligibility data when review is not on the current page', async () => {
    const user = userEvent.setup()
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: 'rev-99',
      existingRating: 3,
      existingComment: 'Bình luận từ trang khác',
    })

    render(<ProductReviews productId="p-1" />)

    const editBtn = await screen.findByRole('button', { name: 'Sửa đánh giá của bạn' })
    await user.click(editBtn)

    expect(await screen.findByRole('button', { name: 'Cập nhật đánh giá' })).toBeInTheDocument()
    const textarea = screen.getByPlaceholderText(/Hãy chia sẻ trải nghiệm/i) as HTMLTextAreaElement
    expect(textarea.value).toBe('Bình luận từ trang khác')
    expect(screen.getByText('3 sao - Bình thường')).toBeInTheDocument()
  })

  it('fetches and binds targetReviewId when different from eligibility reviewId', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: 'rev-latest',
      existingRating: 5,
      existingComment: 'Đánh giá đơn mới nhất',
    })
    vi.mocked(reviewApi.getReviewById).mockResolvedValue({
      id: 'rev-target-old',
      productId: 'p-1',
      reviewerName: 'Nguyen Van A',
      rating: 2,
      comment: 'Đánh giá đơn hàng cũ',
      createdAtUtc: '2026-08-01T00:00:00Z',
      updatedAtUtc: '2026-08-01T00:00:00Z',
    })

    render(<ProductReviews productId="p-1" targetReviewId="rev-target-old" />)

    expect(await screen.findByRole('button', { name: 'Cập nhật đánh giá' })).toBeInTheDocument()
    const textarea = screen.getByPlaceholderText(/Hãy chia sẻ trải nghiệm/i) as HTMLTextAreaElement
    expect(textarea.value).toBe('Đánh giá đơn hàng cũ')
    expect(screen.getByText('2 sao - Chưa tốt')).toBeInTheDocument()
    expect(reviewApi.getReviewById).toHaveBeenCalledWith('rev-target-old')
  })
  it('shows error state and retry button when targetReviewId fetch fails', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: 'rev-latest',
      existingRating: 5,
      existingComment: 'Đánh giá mới nhất',
    })
    vi.mocked(reviewApi.getReviewById).mockRejectedValue(new Error('Fetch target review failed'))

    render(<ProductReviews productId="p-1" targetReviewId="rev-target-old" />)

    expect(await screen.findByText('Không thể tải chi tiết đánh giá.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cập nhật đánh giá' })).not.toBeInTheDocument()
  })

  it('forwards targetOrderItemId to the eligibility request', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: true,
      orderItemId: 'oi-verified',
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" targetOrderItemId="oi-deep-link" />)

    await screen.findByRole('button', { name: 'Gửi đánh giá' })

    expect(reviewApi.getEligibility).toHaveBeenCalledWith('p-1', 'test-token', expect.anything(), 'oi-deep-link')
  })

  it('renders invalid link message when deep-link target is rejected', async () => {
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: null,
    })

    render(<ProductReviews productId="p-1" targetOrderItemId="oi-deep-link" />)

    expect(await screen.findByText('Liên kết đánh giá không còn hợp lệ.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Gửi đánh giá' })).not.toBeInTheDocument()
  })

  it('uses backend-confirmed orderItemId, not the raw deep-link target', async () => {
    const user = userEvent.setup()
    vi.mocked(reviewApi.getProductReviews).mockResolvedValue(sampleReviewsResponse)
    vi.mocked(reviewApi.getEligibility).mockResolvedValue({
      canReview: true,
      orderItemId: 'oi-verified',
      reviewId: null,
    })
    vi.mocked(reviewApi.createReview).mockResolvedValue({
      id: 'rev-new',
      productId: 'p-1',
      reviewerName: 'Me',
      rating: 4,
      comment: 'Hay',
      createdAtUtc: '2026-09-04T00:00:00Z',
      updatedAtUtc: '2026-09-04T00:00:00Z',
    })

    render(<ProductReviews productId="p-1" targetOrderItemId="oi-deep-link" />)

    const submitBtn = await screen.findByRole('button', { name: 'Gửi đánh giá' })
    const textarea = screen.getByPlaceholderText(/Hãy chia sẻ trải nghiệm/i)
    await user.type(textarea, 'Hay')
    await user.click(submitBtn)

    expect(await screen.findByText('Gửi đánh giá thành công!')).toBeInTheDocument()
    expect(reviewApi.createReview).toHaveBeenCalledWith('test-token', {
      orderItemId: 'oi-verified',
      rating: 5,
      comment: 'Hay',
    })
  })
})
