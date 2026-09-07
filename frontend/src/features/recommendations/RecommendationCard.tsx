import { Link } from 'react-router-dom'
import type { RecommendationItemDto } from '../../api/recommendationApi'
import './RecommendationCard.css'

export interface RecommendationCardProps {
  item: RecommendationItemDto
  branchId?: string
  contextLabel: string
}

export function RecommendationCard({ item, branchId, contextLabel }: RecommendationCardProps) {
  const search = branchId ? `?branchId=${encodeURIComponent(branchId)}` : ''
  const href = `/product/${item.productId}${search}`

  return (
    <article
      className="recommendation-card"
      data-testid={`recommendation-card-${item.productId}`}
    >
      <div className="recommendation-card__image-frame">
        {item.imageUrl ? (
          <img
            src={item.imageUrl}
            alt={item.name}
            className="recommendation-card__image"
            loading="lazy"
          />
        ) : (
          <div className="recommendation-card__placeholder" aria-hidden="true">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.5"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M2.25 15.75l5.159-5.159a2.25 2.25 0 013.182 0l5.159 5.159m-1.5-1.5l1.409-1.409a2.25 2.25 0 013.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 001.5-1.5V6a1.5 1.5 0 00-1.5-1.5H3.75A1.5 1.5 0 002.25 6v12a1.5 1.5 0 001.5 1.5zm10.5-11.25h.008v.008h-.008V8.25zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z"
              />
            </svg>
            <span>Hình ảne sản phẩm</span>
          </div>
        )}
      </div>

      <div className="recommendation-card__body">
        <h3 className="recommendation-card__name" title={item.name}>
          <Link className="recommendation-card__name-link" to={href}>
            {item.name}
          </Link>
        </h3>
        <p className="recommendation-card__reason">
          <span className="recommendation-card__context">{contextLabel}: </span>
          {item.reason}
        </p>

        <div className="recommendation-card__footer">
          <span className="recommendation-card__price">
            {new Intl.NumberFormat('vi-VN', {
              style: 'currency',
              currency: 'VND',
            }).format(item.price)}
          </span>
          {item.availableQuantity !== undefined && item.availableQuantity !== null && (
            <span className="recommendation-card__stock">
              {item.availableQuantity > 0
                ? `Còn ${item.availableQuantity} tại kho này`
                : 'Tạm hết hàng'}
            </span>
          )}
        </div>
      </div>
    </article>
  )
}