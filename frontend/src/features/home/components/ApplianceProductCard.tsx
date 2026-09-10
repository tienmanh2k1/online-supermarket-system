import React, { useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import type { ApplianceProduct } from '../types'

export interface ApplianceProductCardProps {
  product: ApplianceProduct
}

export function formatVND(amount: number): string {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
  }).format(amount)
}

/**
 * ApplianceProductCard
 * Clean retail purchase hierarchy: Image -> Name -> Specs -> Price -> CTA.
 * Lightweight hover without heavy 3D tilt spring glare loops.
 */
export const ApplianceProductCard = React.memo(function ApplianceProductCard({
  product,
}: ApplianceProductCardProps) {
  const [imageError, setImageError] = useState(false)

  const progressPercent = Math.min(
    100,
    Math.round((product.stockProgress.sold / product.stockProgress.total) * 100)
  )

  const remainingSlots = Math.max(0, product.stockProgress.total - product.stockProgress.sold)
  const savingAmount = Math.max(0, product.originalPrice - product.salePrice)
  const searchParam = product.sku || product.searchTerm || product.name
  const href = `/browse?search=${encodeURIComponent(searchParam)}`

  return (
    <motion.article
      className="appliance-card kg-product-card"
      data-testid="appliance-product-card"
      whileHover={{ y: -2 }}
      transition={{ duration: 0.15 }}
    >
      {/* Badges Bar: Trả góp, Tiết kiệm, Đánh giá */}
      <div className="appliance-card__badges kg-product-card__badges">
        <div className="kg-badges-left">
          {product.installmentBadge && (
            <span className="appliance-badge appliance-badge--installment kg-badge-installment">
              {product.installmentBadge}
            </span>
          )}
          {product.energyRating && (
            <span className="appliance-badge appliance-badge--energy" title="Tem năng lượng chuẩn">
              ⭐ {product.energyRating} Sao
            </span>
          )}
        </div>

        {product.discountPercent > 0 && (
          <span className="appliance-badge appliance-badge--discount kg-badge-discount">
            -{product.discountPercent}%
          </span>
        )}
      </div>

      {/* 1. Product Image Frame with explicit dimensions, lazy loading, and onError fallback */}
      <Link to={href} className="appliance-card__image-wrap kg-product-card__image-wrap" aria-label={product.name}>
        {product.imageUrl && !imageError ? (
          <img
            src={product.imageUrl}
            alt={product.name}
            className="appliance-card__image kg-product-card__image"
            width={300}
            height={225}
            loading="lazy"
            decoding="async"
            onError={() => setImageError(true)}
          />
        ) : (
          <div className="appliance-card__image-placeholder" data-testid="appliance-card-placeholder">
            <span aria-hidden="true">🚰</span>
            <small>Hình ảnh thiết bị</small>
          </div>
        )}
      </Link>

      {/* 2. Product Name & Brand */}
      <div className="appliance-card__body kg-product-card__body">
        <div className="appliance-card__header">
          <span className="appliance-card__brand kg-product-brand">{product.brand}</span>
          <span className="appliance-card__model">{product.modelNumber}</span>
        </div>

        <h3 className="appliance-card__name kg-product-card__title" title={product.name}>
          <Link to={href} className="appliance-card__name-link">
            {product.name}
          </Link>
        </h3>

        {/* Technical Specs Chips */}
        {product.specs.length > 0 && (
          <div className="appliance-card__specs kg-product-card__specs" aria-label="Thông số kỹ thuật tiêu biểu">
            {product.specs.map((spec, idx) => (
              <span key={idx} className="appliance-card__spec-chip kg-spec-pill" title={`${spec.key}: ${spec.value}`}>
                {spec.value}
              </span>
            ))}
          </div>
        )}

        {/* 3. Pricing Hierarchy & Saving Label */}
        <div className="appliance-card__price-box kg-product-card__prices">
          <div className="appliance-card__price-row">
            <span className="appliance-card__sale-price kg-price-sale">
              {formatVND(product.salePrice)}
            </span>
            {product.originalPrice > product.salePrice && (
              <span className="appliance-card__orig-price kg-price-orig">
                {formatVND(product.originalPrice)}
              </span>
            )}
          </div>
          {savingAmount > 0 && (
            <p className="kg-product-saving">
              Tiết kiệm <strong>{formatVND(savingAmount)}</strong>
            </p>
          )}
        </div>

        {/* Gift Tag Sticker if available */}
        {product.giftNote && (
          <div className="appliance-card__gift-tag kg-gift-box" title={product.giftNote}>
            <span className="appliance-card__gift-icon" aria-hidden="true">
              🎁
            </span>
            <span className="appliance-card__gift-text">{product.giftNote}</span>
          </div>
        )}

        {/* Stock Status Bar */}
        <div className="appliance-card__progress-wrap kg-product-card__stock">
          <div className="kg-stock-header">
            <span className="kg-stock-flame">Còn {remainingSlots} suất</span>
            <span className="appliance-card__progress-label kg-stock-ratio">
              Đã bán {product.stockProgress.sold}/{product.stockProgress.total}
            </span>
          </div>
          <div className="appliance-card__progress-bar kg-progress-bar">
            <div
              className="appliance-card__progress-fill kg-progress-fill"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </div>

        {/* 4. Concise CTA Link */}
        <div className="appliance-card__actions kg-product-card__actions">
          <Link to={href} className="appliance-card__cta-btn kg-cta-buy-btn">
            <span>Xem chi tiết</span>
            <span aria-hidden="true">&rarr;</span>
          </Link>
        </div>
      </div>
    </motion.article>
  )
})
