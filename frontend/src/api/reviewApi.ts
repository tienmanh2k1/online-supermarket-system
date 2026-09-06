import { getJson, postJson, putJson } from './httpClient'

export interface ReviewDto {
  id: string
  productId: string
  reviewerName: string
  rating: number
  comment: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface ProductReviewsDto {
  averageRating: number
  reviewCount: number
  data: ReviewDto[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ReviewEligibilityDto {
  canReview: boolean
  orderItemId: string | null
  reviewId: string | null
  existingRating?: number | null
  existingComment?: string | null
}

export interface CreateReviewRequest {
  orderItemId: string
  rating: number
  comment?: string | null
}

export interface UpdateReviewRequest {
  rating: number
  comment?: string | null
}

export const reviewApi = {
  getProductReviews: (productId: string, page = 1, pageSize = 10, signal?: AbortSignal) =>
    getJson<ProductReviewsDto>(`/products/${productId}/reviews?page=${page}&pageSize=${pageSize}`, { signal }),

  getEligibility: (productId: string, token?: string | null, signal?: AbortSignal, orderItemId?: string | null) =>
    getJson<ReviewEligibilityDto>(`/products/${productId}/review-eligibility${orderItemId ? `?orderItemId=${orderItemId}` : ''}`, {
      token: token ?? undefined,
      signal,
    }),

  getReviewById: (reviewId: string, signal?: AbortSignal) =>
    getJson<ReviewDto>(`/reviews/${reviewId}`, { signal }),

  createReview: (token: string, body: CreateReviewRequest) =>
    postJson<ReviewDto>('/reviews', body, { token }),

  updateReview: (reviewId: string, token: string, body: UpdateReviewRequest) =>
    putJson<ReviewDto>(`/reviews/${reviewId}`, body, { token }),
}
