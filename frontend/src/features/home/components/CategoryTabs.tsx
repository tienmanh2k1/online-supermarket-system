import { useState, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { motion, AnimatePresence } from 'motion/react'
import type { ApplianceProduct, CategoryTabItem, CategoryTabSlug } from '../types'
import { ApplianceProductCard } from './ApplianceProductCard'

export interface CategoryTabsProps {
  tabs: CategoryTabItem[]
  products: ApplianceProduct[]
  defaultTab?: CategoryTabSlug
}

export function CategoryTabs({ tabs, products, defaultTab = 'all' }: CategoryTabsProps) {
  const [activeTabSlug, setActiveTabSlug] = useState<CategoryTabSlug>(defaultTab)

  const isTestEnv = import.meta.env.MODE === 'test'
  const reduceMotion =
    isTestEnv ||
    (typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches)

  const filteredProducts = useMemo(() => {
    if (activeTabSlug === 'all') return products
    return products.filter((p) => p.categorySlug === activeTabSlug)
  }, [activeTabSlug, products])

  return (
    <section
      className="category-showcase-section bg-white rounded-2xl border border-slate-200 shadow-sm"
      data-testid="home-category-showcase"
      aria-label="Sản phẩm theo ngành hàng"
    >
      <div className="home-section-header">
        <div>
          <h2 id="category-showcase-heading" className="home-section-title">
            Thiết Bị Điện Máy Chính Hãng
          </h2>
          <p className="home-section-desc">
            Chọn ngành hàng yêu thích để khám phá công nghệ mới nhất từ Samsung, LG, Daikin, Electrolux...
          </p>
        </div>
        <Link to="/browse" className="home-section-link">
          Xem toàn bộ kho hàng &rarr;
        </Link>
      </div>

      {/* Tabs Bar with Motion Layout Indicator */}
      <div className="category-tabs-bar" role="tablist" aria-label="Lọc sản phẩm theo ngành hàng">
        {tabs.map((tab) => {
          const isActive = tab.slug === activeTabSlug
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls={`tabpanel-${tab.slug}`}
              id={`tab-${tab.slug}`}
              className={`category-tab-btn ${isActive ? 'active' : ''}`}
              onClick={() => setActiveTabSlug(tab.slug)}
            >
              {isActive && (
                <motion.span
                  layoutId="categoryActiveIndicator"
                  className="category-tab-active-indicator"
                  transition={{ duration: reduceMotion ? 0 : 0.2, ease: 'easeOut' }}
                />
              )}
              <span className="category-tab-btn-text">{tab.name}</span>
            </button>
          )
        })}
      </div>

      {/* Products Grid with Smooth Transition */}
      <div
        id={`tabpanel-${activeTabSlug}`}
        role="tabpanel"
        aria-labelledby={`tab-${activeTabSlug}`}
        className="category-products-grid"
        data-testid="category-products-grid"
      >
        <AnimatePresence mode="popLayout">
          {filteredProducts.length > 0 ? (
            filteredProducts.map((product) => (
              <motion.div
                key={product.id}
                layout={!reduceMotion}
                initial={reduceMotion ? false : { opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={reduceMotion ? undefined : { opacity: 0, y: -10 }}
                transition={{ duration: reduceMotion ? 0 : 0.2 }}
                className="category-card-wrapper"
              >
                <ApplianceProductCard product={product} />
              </motion.div>
            ))
          ) : (
            <motion.div
              key="empty-state"
              initial={reduceMotion ? false : { opacity: 0, y: 15 }}
              animate={{ opacity: 1, y: 0 }}
              exit={reduceMotion ? undefined : { opacity: 0, y: -15 }}
              transition={{ duration: reduceMotion ? 0 : 0.2 }}
              className="category-products-empty"
              data-testid="category-empty-state"
            >
              <span className="category-products-empty__icon" aria-hidden="true">
                📦
              </span>
              <p className="category-products-empty__text">
                Hiện chưa có sản phẩm nào trong danh mục này.
              </p>
              <button
                type="button"
                className="category-tab-btn active category-empty-reset-btn"
                data-testid="category-empty-reset-btn"
                onClick={() => setActiveTabSlug('all')}
              >
                Xem tất cả sản phẩm
              </button>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </section>
  )
}
