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

const isTestEnv = import.meta.env.MODE === 'test'

export function CategoryTabs({ tabs, products, defaultTab = 'all' }: CategoryTabsProps) {
  const [activeTabSlug, setActiveTabSlug] = useState<CategoryTabSlug>(defaultTab)

  const filteredProducts = useMemo(() => {
    if (activeTabSlug === 'all') return products
    return products.filter((p) => p.categorySlug === activeTabSlug)
  }, [activeTabSlug, products])

  return (
    <section
      className="category-showcase-section"
      data-testid="home-category-showcase"
      aria-labelledby="category-showcase-heading"
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
                  transition={{ type: 'spring', stiffness: 500, damping: 35 }}
                />
              )}
              <span className="category-tab-btn-text">{tab.name}</span>
            </button>
          )
        })}
      </div>

      {/* Products Grid with Smooth Reorder & AnimatePresence */}
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
                layout={!isTestEnv}
                initial={isTestEnv ? false : { opacity: 0, scale: 0.92, y: 15 }}
                animate={{ opacity: 1, scale: 1, y: 0 }}
                exit={isTestEnv ? undefined : { opacity: 0, scale: 0.92, y: -15 }}
                transition={
                  isTestEnv
                    ? { duration: 0 }
                    : { duration: 0.32, ease: [0.25, 1, 0.5, 1] }
                }
                className="category-card-wrapper"
              >
                <ApplianceProductCard product={product} />
              </motion.div>
            ))
          ) : (
            <motion.div
              key="empty-state"
              initial={isTestEnv ? false : { opacity: 0, scale: 0.85, y: 25 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={isTestEnv ? undefined : { opacity: 0, scale: 0.9, y: -15 }}
              transition={
                isTestEnv
                  ? { duration: 0 }
                  : { type: 'spring', stiffness: 350, damping: 22 }
              }
              className="category-products-empty"
              data-testid="category-empty-state"
            >
              <motion.span
                className="category-products-empty__icon"
                aria-hidden="true"
                animate={
                  isTestEnv
                    ? undefined
                    : { rotate: [0, -10, 10, -5, 0], scale: [1, 1.1, 1] }
                }
                transition={{ duration: 1.5, repeat: Infinity, repeatDelay: 2 }}
              >
                📦
              </motion.span>
              <p className="category-products-empty__text">
                Hiện chưa có sản phẩm nào trong danh mục này.
              </p>
              <motion.button
                type="button"
                className="category-tab-btn active category-empty-reset-btn"
                data-testid="category-empty-reset-btn"
                onClick={() => setActiveTabSlug('all')}
                whileHover={isTestEnv ? undefined : { scale: 1.05 }}
                whileTap={isTestEnv ? undefined : { scale: 0.95 }}
                transition={{ type: 'spring', stiffness: 400, damping: 17 }}
              >
                Xem tất cả sản phẩm
              </motion.button>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </section>
  )
}
