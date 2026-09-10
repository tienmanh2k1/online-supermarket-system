import { useState, useEffect, useCallback } from 'react'
import {
  reviewApi,
  type ProductReviewsDto,
  type ReviewEligibilityDto,
  type ReviewDto,
} from '../../api/reviewApi'
import { ReviewForm } from './ReviewForm'
import { useAuth } from '../auth/AuthContext'
import './ProductReviews.css'

export interface ProductReviewsProps {
  productId: string
  targetOrderItemId?: string | null
  targetReviewId?: string | null
}

export function ProductReviews({
  productId,
  targetOrderItemId,
  targetReviewId,
}: ProductReviewsProps) {
  const { isAuthenticated, accessToken } = useAuth()

  const [reviewsData, setReviewsData] = useState<ProductReviewsDto | null>(null)
  const [eligibility, setEligibility] = useState<ReviewEligibilityDto | null>(null)
  const [fetchedReview, setFetchedReview] = useState<ReviewDto | null>(null)
  const [isLoadingTargetReview, setIsLoadingTargetReview] = useState(Boolean(targetReviewId))
  const [targetReviewError, setTargetReviewError] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isEditing, setIsEditing] = useState(Boolean(targetReviewId))
  const [retryTrigger, setRetryTrigger] = useState(0)
  const [retryTargetReviewTrigger, setRetryTargetReviewTrigger] = useState(0)
  const [justSubmitted, setJustSubmitted] = useState(false)

  const fetchReviews = useCallback(
    (page: number, signal?: AbortSignal) => {
      setIsLoading(true)
      setError(null)

      reviewApi
        .getProductReviews(productId, page, 10, signal)
        .then((data) => {
          setReviewsData(data)
          setIsLoading(false)
        })
        .catch((err) => {
          if (err instanceof Error && err.name === 'AbortError') return
          setError('Không thể tải đánh giá sản phẩm.')
          setIsLoading(false)
        })
    },
    [productId]
  )

  const fetchEligibility = useCallback(
    (signal?: AbortSignal) => {
      if (!isAuthenticated || !accessToken) {
        setEligibility(null)
        return
      }

      reviewApi
        .getEligibility(productId, accessToken, signal, targetOrderItemId)
        .then((data) => {
          setEligibility(data)
          setJustSubmitted(false)
          if (targetReviewId && data.reviewId === targetReviewId) {
            setIsEditing(true)
          }
        })
        .catch((err) => {
          if (err instanceof Error && err.name === 'AbortError') return
          setEligibility(null)
        })
    },
    [productId, isAuthenticated, accessToken, targetReviewId, targetOrderItemId]
  )

  useEffect(() => {
    const controller = new AbortController()
    fetchReviews(currentPage, controller.signal)
    fetchEligibility(controller.signal)

    return () => controller.abort()
  }, [fetchReviews, fetchEligibility, currentPage, retryTrigger])

  useEffect(() => {
    const activeReviewId = targetReviewId || eligibility?.reviewId
    if (!activeReviewId) return

    if (eligibility?.reviewId === activeReviewId && eligibility?.existingRating != null) return
    if (reviewsData?.data.some((r) => r.id === activeReviewId)) return
    if (fetchedReview?.id === activeReviewId) return

    let cancelled = false
    setIsLoadingTargetReview(true)
    setTargetReviewError(false)
    reviewApi
      .getReviewById(activeReviewId)
      .then((data) => {
        if (!cancelled) {
          setFetchedReview(data)
          setIsLoadingTargetReview(false)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setTargetReviewError(true)
          setIsLoadingTargetReview(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [targetReviewId, eligibility?.reviewId, eligibility?.existingRating, reviewsData, fetchedReview?.id, retryTargetReviewTrigger])

  const handleRetry = () => {
    setRetryTrigger((prev) => prev + 1)
  }

  const handleReviewSuccess = () => {
    setIsEditing(false)
    setJustSubmitted(true)
    fetchReviews(1)
    fetchEligibility()
    setCurrentPage(1)
  }

  const averageRatingFormatted = reviewsData
    ? reviewsData.averageRating.toLocaleString('vi-VN', {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1,
      }) + '/5'
    : '0/5'

  const totalPages = reviewsData ? Math.ceil(reviewsData.totalCount / reviewsData.pageSize) : 0

  const activeReviewId = targetReviewId || eligibility?.reviewId
  const isEligibilityMatching = Boolean(activeReviewId && eligibility?.reviewId === activeReviewId)
  const userExistingReview = reviewsData?.data.find((r) => r.id === activeReviewId)
  const isFetchedReviewMatching = Boolean(activeReviewId && fetchedReview?.id === activeReviewId)

  const isDataAvailable =
    (isEligibilityMatching && eligibility?.existingRating != null) ||
    Boolean(userExistingReview) ||
    isFetchedReviewMatching

  const effectiveRating =
    (isEligibilityMatching ? eligibility?.existingRating : null) ??
    userExistingReview?.rating ??
    (isFetchedReviewMatching ? fetchedReview?.rating : null) ??
    5

  const effectiveComment =
    (isEligibilityMatching ? eligibility?.existingComment : null) ??
    userExistingReview?.comment ??
    (isFetchedReviewMatching ? fetchedReview?.comment : null) ??
    ''

  return (
    <section className="product-reviews" id="reviews" tabIndex={-1} aria-label="Đánh giá sản phẩm">
      <div className="product-reviews__header">
        <h2 className="product-reviews__heading">Đánh giá sản phẩm</h2>
        {reviewsData && reviewsData.totalCount > 0 && (
          <div className="product-reviews__aggregate">
            <span className="product-reviews__avg-score">{averageRatingFormatted}</span>
            <div className="product-reviews__stars-summary" aria-hidden="true">
              {[1, 2, 3, 4, 5].map((star) => (
                <span
                  key={star}
                  className={`product-reviews__star ${
                    star <= Math.round(reviewsData.averageRating) ? 'is-filled' : ''
                  }`}
                >
                  ★
                </span>
              ))}
            </div>
            <span className="product-reviews__count">
              ({reviewsData.totalCount} đánh giá)
            </span>
          </div>
        )}
      </div>

      {/* Form section based on auth and eligibility */}
      <div className="product-reviews__form-section">
        {!isAuthenticated ? (
          <div className="product-reviews__notice">
            <p>Đăng nhập để đánh giá sản phẩm sau khi mua hàng.</p>
          </div>
        ) : eligibility?.canReview && eligibility?.orderItemId ? (
          <ReviewForm
            productId={productId}
            orderItemId={eligibility.orderItemId}
            onSuccess={handleReviewSuccess}
          />
        ) : targetOrderItemId && !eligibility?.canReview && !eligibility?.reviewId && !justSubmitted ? (
          <div className="product-reviews__invalid-link" role="alert">
            <p>Liên kết đánh giá không còn hợp lệ.</p>
          </div>
        ) : activeReviewId ? (
          isEditing ? (
            targetReviewError ? (
              <div className="product-reviews__error-box">
                <p>Không thể tải chi tiết đánh giá.</p>
                <button
                  type="button"
                  className="product-reviews__retry-btn"
                  onClick={() => setRetryTargetReviewTrigger((p) => p + 1)}
                >
                  Thử lại
                </button>
              </div>
            ) : !isDataAvailable && isLoadingTargetReview ? (
              <div className="product-reviews__loading" role="status">
                <p>Đang tải dữ liệu đánh giá...</p>
              </div>
            ) : isDataAvailable ? (
              <ReviewForm
                key={activeReviewId}
                productId={productId}
                reviewId={activeReviewId}
                initialRating={effectiveRating}
                initialComment={effectiveComment}
                onSuccess={handleReviewSuccess}
                onCancel={() => setIsEditing(false)}
              />
            ) : null
          ) : (
            <div className="product-reviews__reviewed-box">
              <p>Bạn đã gửi đánh giá cho sản phẩm này.</p>
              <button
                type="button"
                className="product-reviews__edit-btn"
                onClick={() => setIsEditing(true)}
              >
                Sửa đánh giá của bạn
              </button>
            </div>
          )
        ) : (
          <div className="product-reviews__notice">
            <p>Chỉ những khách hàng đã mua và nhận hàng thành công mới có thể đánh giá sản phẩm này.</p>
          </div>
        )}
      </div>

      {/* Reviews list */}
      <div className="product-reviews__list-section">
        {isLoading ? (
          <div className="product-reviews__loading" role="status">
            <p>Đang tải đánh giá...</p>
          </div>
        ) : error ? (
          <div className="product-reviews__error" role="alert">
            <p>{error}</p>
            <button
              type="button"
              className="product-reviews__retry-btn"
              onClick={handleRetry}
            >
              Thử lại
            </button>
          </div>
        ) : !reviewsData || reviewsData.data.length === 0 ? (
          <div className="product-reviews__empty">
            <p>Chưa có đánh giá nào cho sản phẩm này.</p>
          </div>
        ) : (
          <>
            <ul className="product-reviews__list" aria-label="Danh sách nhận xét">
              {reviewsData.data.map((rev) => (
                <li key={rev.id} className="product-review-item">
                  <div className="product-review-item__header">
                    <span className="product-review-item__author">{rev.reviewerName}</span>
                    <span className="product-review-item__date">
                      {new Date(rev.createdAtUtc).toLocaleDateString('vi-VN', {
                        year: 'numeric',
                        month: 'long',
                        day: 'numeric',
                      })}
                    </span>
                  </div>
                  <div
                    className="product-review-item__stars"
                    aria-label={`Đánh giá ${rev.rating} trên 5 sao`}
                  >
                    {[1, 2, 3, 4, 5].map((star) => (
                      <span
                        key={star}
                        className={`product-reviews__star ${
                          star <= rev.rating ? 'is-filled' : ''
                        }`}
                        aria-hidden="true"
                      >
                        ★
                      </span>
                    ))}
                  </div>
                  {rev.comment && (
                    <p className="product-review-item__comment">{rev.comment}</p>
                  )}
                </li>
              ))}
            </ul>

            {totalPages > 1 && (
              <nav className="product-reviews__pagination" aria-label="Phân trang đánh giá">
                <button
                  type="button"
                  className="product-reviews__page-btn"
                  onClick={() => setCurrentPage((prev) => Math.max(1, prev - 1))}
                  disabled={currentPage === 1}
                >
                  Trang trước
                </button>
                <span className="product-reviews__page-info">
                  Trang {currentPage} / {totalPages}
                </span>
                <button
                  type="button"
                  className="product-reviews__page-btn"
                  onClick={() => setCurrentPage((prev) => Math.min(totalPages, prev + 1))}
                  disabled={currentPage === totalPages}
                >
                  Trang sau
                </button>
              </nav>
            )}
          </>
        )}
      </div>
    </section>
  )
}
