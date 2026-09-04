import React, { useEffect, useState, useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  catalogApi,
  type ProductSummaryDto,
  type CategoryDto,
  type BrandDto,
  type PaginationMeta,
} from '../../api/catalogApi'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { FilterSidebar, type FilterState } from './FilterSidebar'
import { ProductGrid } from './ProductGrid'
import { Pagination } from './Pagination'
import { formatPrice } from './ProductCard'
import { RecommendationShelfLoader } from '../recommendations/RecommendationShelfLoader'
import './ProductBrowsePage.css'

export function ProductBrowsePage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [brands, setBrands] = useState<BrandDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])

  const [products, setProducts] = useState<ProductSummaryDto[]>([])
  const [paginationMeta, setPaginationMeta] = useState<PaginationMeta>({
    totalCount: 0,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  })

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isMobileFilterOpen, setIsMobileFilterOpen] = useState(false)

  // Parse filters from URL search params
  const currentFilters: FilterState = useMemo(() => {
    const categoryId = searchParams.get('categoryId') || searchParams.get('category') || undefined
    const brandId = searchParams.get('brandId') || searchParams.get('brand') || undefined
    const branchId = searchParams.get('branchId') || undefined
    const search = searchParams.get('search') || searchParams.get('q') || undefined
    const minPriceStr = searchParams.get('minPrice')
    const maxPriceStr = searchParams.get('maxPrice')

    const minPrice = minPriceStr ? parseFloat(minPriceStr) : undefined
    const maxPrice = maxPriceStr ? parseFloat(maxPriceStr) : undefined

    return {
      categoryId,
      brandId,
      branchId,
      search,
      minPrice: minPrice !== undefined && !isNaN(minPrice) ? minPrice : undefined,
      maxPrice: maxPrice !== undefined && !isNaN(maxPrice) ? maxPrice : undefined,
    }
  }, [searchParams])

  const currentPage = useMemo(() => {
    const pageStr = searchParams.get('page')
    const parsed = pageStr ? parseInt(pageStr, 10) : 1
    return isNaN(parsed) || parsed < 1 ? 1 : parsed
  }, [searchParams])

  // Load initial dropdown options (Categories, Brands, Branches)
  useEffect(() => {
    const abortController = new AbortController()

    async function loadMeta() {
      try {
        const [cats, brs, branchList] = await Promise.all([
          catalogApi.getCategories(abortController.signal).catch(() => []),
          catalogApi.getBrands(abortController.signal).catch(() => []),
          branchApi.getBranches(abortController.signal).catch(() => []),
        ])
        setCategories(cats)
        setBrands(brs)
        setBranches(branchList)
      } catch {
        // Handled via fallback default values
      }
    }

    loadMeta()
    return () => abortController.abort()
  }, [])

  // Load Products whenever searchParams change
  const fetchProducts = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await catalogApi.getProducts({
        categoryId: currentFilters.categoryId,
        brandId: currentFilters.brandId,
        branchId: currentFilters.branchId,
        search: currentFilters.search,
        minPrice: currentFilters.minPrice,
        maxPrice: currentFilters.maxPrice,
        page: currentPage,
        pageSize: 20,
      })

      setProducts(response.items)
      setPaginationMeta(response.meta)
    } catch (err: any) {
      setError(err?.message || 'Đã có lỗi xảy ra khi tải sản phẩm. Vui lòng thử lại!')
    } finally {
      setLoading(false)
    }
  }, [currentFilters, currentPage])

  useEffect(() => {
    fetchProducts()
  }, [fetchProducts])

  // Update query params when filter changes
  const handleFilterChange = (newFilters: FilterState) => {
    const nextParams = new URLSearchParams()

    if (newFilters.search) nextParams.set('search', newFilters.search)
    if (newFilters.categoryId) nextParams.set('categoryId', newFilters.categoryId)
    if (newFilters.brandId) nextParams.set('brandId', newFilters.brandId)
    if (newFilters.branchId) nextParams.set('branchId', newFilters.branchId)
    if (newFilters.minPrice !== undefined) nextParams.set('minPrice', String(newFilters.minPrice))
    if (newFilters.maxPrice !== undefined) nextParams.set('maxPrice', String(newFilters.maxPrice))

    // Always reset to page 1 when filters change
    nextParams.set('page', '1')
    setSearchParams(nextParams)
    setIsMobileFilterOpen(false)
  }

  const handlePageChange = (newPage: number) => {
    const nextParams = new URLSearchParams(searchParams)
    nextParams.set('page', String(newPage))
    setSearchParams(nextParams)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleResetFilters = () => {
    setSearchParams({})
    setIsMobileFilterOpen(false)
  }

  // Active filter names for tags
  const activeCategory = categories.find((c) => c.id === currentFilters.categoryId)
  const activeBrand = brands.find((b) => b.id === currentFilters.brandId)
  const activeBranch = branches.find((b) => b.id === currentFilters.branchId)

  const removeSingleFilter = (key: keyof FilterState) => {
    const updated = { ...currentFilters, [key]: undefined }
    if (key === 'minPrice' || key === 'maxPrice') {
      updated.minPrice = undefined
      updated.maxPrice = undefined
    }
    handleFilterChange(updated)
  }

  const activeFilterCount = [
    currentFilters.search,
    currentFilters.categoryId,
    currentFilters.brandId,
    currentFilters.branchId,
    currentFilters.minPrice !== undefined || currentFilters.maxPrice !== undefined,
  ].filter(Boolean).length

  return (
    <div className="product-browse-page">
      {/* Header Banner */}
      <section className="product-browse-header">
        <div className="product-browse-header__content">
          <div className="product-browse-header__text">
            <span className="eyebrow">Gian Hàng Trực Tuyến</span>
            <h1 className="product-browse-title">Duyệt & Tìm Kiếm Sản Phẩm</h1>
            <p className="product-browse-lead">
              Lựa chọn hàng hóa tươi sống, kiểm tra giá và tình trạng còn hàng theo từng chi nhánh siêu thị.
            </p>
          </div>

          {/* Branch Quick Selector Banner */}
          <div className="product-browse-branch-card">
            <div className="branch-card__header">
              <svg
                className="branch-card__icon"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                aria-hidden="true"
              >
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z" />
              </svg>
              <span>Kho hàng đang xem</span>
            </div>
            <div className="branch-card__body">
              <strong>{activeBranch ? activeBranch.name : 'Tất cả chi nhánh'}</strong>
              <small>{activeBranch ? activeBranch.address : 'Hiển thị giá gốc hệ thống'}</small>
            </div>
          </div>
        </div>
      </section>

      <RecommendationShelfLoader
        branchId={currentFilters.branchId}
      />

      {/* Main Container */}
      <div className="product-browse-layout">
        {/* Mobile Filter Trigger Button */}
        <div className="product-browse-mobile-bar">
          <button
            type="button"
            className="mobile-filter-trigger"
            onClick={() => setIsMobileFilterOpen(true)}
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              className="mobile-filter-icon"
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3c2.755 0 5.455.232 8.083.678.533.09.917.556.917 1.096v1.044a2.25 2.25 0 01-.659 1.591l-5.432 5.432a2.25 2.25 0 00-.659 1.591v2.927a2.25 2.25 0 01-1.244 2.013L9.75 21v-6.568a2.25 2.25 0 00-.659-1.591L3.659 7.409A2.25 2.25 0 013 5.818V4.774c0-.54.384-1.006.917-1.096A48.32 48.32 0 0112 3z" />
            </svg>
            Bộ lọc & Tìm kiếm {activeFilterCount > 0 && `(${activeFilterCount})`}
          </button>
        </div>

        {/* Left Sidebar */}
        <div className="product-browse-sidebar-col">
          <FilterSidebar
            categories={categories}
            brands={brands}
            branches={branches}
            filters={currentFilters}
            onFilterChange={handleFilterChange}
            onReset={handleResetFilters}
            isMobileOpen={isMobileFilterOpen}
            onCloseMobile={() => setIsMobileFilterOpen(false)}
          />
        </div>

        {/* Right Content Area */}
        <main className="product-browse-main-col">
          {/* Active Filter Tags Bar */}
          {activeFilterCount > 0 && (
            <div className="active-filters-bar" aria-label="Bộ lọc đang chọn">
              <span className="active-filters-label">Đang lọc:</span>
              <div className="active-filters-chips">
                {currentFilters.search && (
                  <span className="active-filter-chip">
                    Từ khóa: "<strong>{currentFilters.search}</strong>"
                    <button
                      type="button"
                      onClick={() => removeSingleFilter('search')}
                      aria-label="Xóa lọc từ khóa"
                    >
                      ✕
                    </button>
                  </span>
                )}
                {activeBranch && (
                  <span className="active-filter-chip">
                    Chi nhánh: <strong>{activeBranch.name}</strong>
                    <button
                      type="button"
                      onClick={() => removeSingleFilter('branchId')}
                      aria-label="Xóa lọc chi nhánh"
                    >
                      ✕
                    </button>
                  </span>
                )}
                {activeCategory && (
                  <span className="active-filter-chip">
                    Danh mục: <strong>{activeCategory.name}</strong>
                    <button
                      type="button"
                      onClick={() => removeSingleFilter('categoryId')}
                      aria-label="Xóa lọc danh mục"
                    >
                      ✕
                    </button>
                  </span>
                )}
                {activeBrand && (
                  <span className="active-filter-chip">
                    Thương hiệu: <strong>{activeBrand.name}</strong>
                    <button
                      type="button"
                      onClick={() => removeSingleFilter('brandId')}
                      aria-label="Xóa lọc thương hiệu"
                    >
                      ✕
                    </button>
                  </span>
                )}
                {(currentFilters.minPrice !== undefined || currentFilters.maxPrice !== undefined) && (
                  <span className="active-filter-chip">
                    Giá:{' '}
                    <strong>
                      {currentFilters.minPrice !== undefined ? formatPrice(currentFilters.minPrice) : '0đ'}{' '}
                      -{' '}
                      {currentFilters.maxPrice !== undefined
                        ? formatPrice(currentFilters.maxPrice)
                        : 'Vô cực'}
                    </strong>
                    <button
                      type="button"
                      onClick={() => removeSingleFilter('minPrice')}
                      aria-label="Xóa lọc giá"
                    >
                      ✕
                    </button>
                  </span>
                )}
                <button
                  type="button"
                  className="clear-all-chips-btn"
                  onClick={handleResetFilters}
                >
                  Xóa tất cả
                </button>
              </div>
            </div>
          )}

          {/* Product Grid */}
          <ProductGrid
            products={products}
            loading={loading}
            error={error}
            branchName={activeBranch?.name}
            branchId={currentFilters.branchId}
            onRetry={fetchProducts}
            onResetFilters={handleResetFilters}
          />

          {/* Pagination */}
          {!loading && !error && (
            <Pagination
              meta={paginationMeta}
              onPageChange={handlePageChange}
              disabled={loading}
            />
          )}
        </main>
      </div>
    </div>
  )
}
