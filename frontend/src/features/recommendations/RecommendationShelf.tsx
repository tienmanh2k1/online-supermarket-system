import type { RecommendationItemDto } from '../../api/recommendationApi'
import { RecommendationCard } from './RecommendationCard'
import './RecommendationShelf.css'

export interface RecommendationShelfProps {
  title: string
  contextLabel: string
  items: RecommendationItemDto[]
  loading: boolean
  isError: boolean
  branchId?: string
  onRetry?: () => void
}

export function RecommendationShelf({
  title,
  contextLabel,
  items,
  loading,
  isError,
  branchId,
  onRetry,
}: RecommendationShelfProps) {
  if (loading) {
    return (
      <section
        className="recommendation-shelf"
        aria-label={title}
        aria-busy="true"
      >
        <h2 className="recommendation-shelf__title">{title}</h2>
        <div className="recommendation-shelf__loading" data-testid="recommendation-shelf-loading">
          <div className="recommendation-shelf__skeleton shimmer" />
          <div className="recommendation-shelf__skeleton shimmer" />
          <div className="recommendation-shelf__skeleton shimmer" />
        </div>
      </section>
    )
  }

  if (isError) {
    return (
      <section className="recommendation-shelf" aria-label={title}>
        <h2 className="recommendation-shelf__title">{title}</h2>
        <p className="recommendation-shelf__error">
          Không thể tải sản phẩm đề xuất.
          {onRetry && (
            <button type="button" onClick={onRetry} className="recommendation-shelf__retry">
              Thử lại
            </button>
          )}
        </p>
      </section>
    )
  }

  if (items.length === 0) {
    return null
  }

  return (
    <section className="recommendation-shelf" aria-label={title}>
      <h2 className="recommendation-shelf__title">{title}</h2>
      <ul className="recommendation-shelf__track">
        {items.map((item) => (
          <li key={item.productId}>
            <RecommendationCard item={item} branchId={branchId} contextLabel={contextLabel} />
          </li>
        ))}
      </ul>
    </section>
  )
}