import React, { useState, useEffect } from 'react'
import type { CategoryDto, BrandDto } from '../../api/catalogApi'
import type { BranchDto } from '../../api/branchApi'
import './FilterSidebar.css'

export interface FilterState {
  search?: string
  categoryId?: string
  brandId?: string
  minPrice?: number
  maxPrice?: number
  branchId?: string
}

export interface FilterSidebarProps {
  categories: CategoryDto[]
  brands: BrandDto[]
  branches: BranchDto[]
  filters: FilterState
  onFilterChange: (filters: FilterState) => void
  onReset: () => void
  isMobileOpen?: boolean
  onCloseMobile?: () => void
}

const PRICE_PRESETS = [
  { label: 'Dưới 50.000đ', min: undefined, max: 50000 },
  { label: '50.000đ - 150.000đ', min: 50000, max: 150000 },
  { label: '150.000đ - 300.000đ', min: 150000, max: 300000 },
  { label: 'Trên 300.000đ', min: 300000, max: undefined },
]

export function FilterSidebar({
  categories,
  brands,
  branches,
  filters,
  onFilterChange,
  onReset,
  isMobileOpen = false,
  onCloseMobile,
}: FilterSidebarProps) {
  const [localSearch, setLocalSearch] = useState(filters.search ?? '')
  const [localMinPrice, setLocalMinPrice] = useState<string>(
    filters.minPrice !== undefined ? String(filters.minPrice) : ''
  )
  const [localMaxPrice, setLocalMaxPrice] = useState<string>(
    filters.maxPrice !== undefined ? String(filters.maxPrice) : ''
  )
  const [priceError, setPriceError] = useState<string>('')

  useEffect(() => {
    setLocalSearch(filters.search ?? '')
    setLocalMinPrice(filters.minPrice !== undefined ? String(filters.minPrice) : '')
    setLocalMaxPrice(filters.maxPrice !== undefined ? String(filters.maxPrice) : '')
    setPriceError('')
  }, [filters])

  const handleApplyPrice = (e?: React.FormEvent) => {
    if (e) e.preventDefault()
    setPriceError('')

    const parsedMin = localMinPrice ? parseFloat(localMinPrice) : undefined
    const parsedMax = localMaxPrice ? parseFloat(localMaxPrice) : undefined

    // Validate: min should not exceed max
    if (parsedMin !== undefined && parsedMax !== undefined && parsedMin > parsedMax) {
      setPriceError('Giá tối thiểu không được lớn hơn giá tối đa.')
      return
    }

    onFilterChange({
      ...filters,
      minPrice: parsedMin !== undefined && !isNaN(parsedMin) && parsedMin >= 0 ? parsedMin : undefined,
      maxPrice: parsedMax !== undefined && !isNaN(parsedMax) && parsedMax >= 0 ? parsedMax : undefined,
    })
  }

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    onFilterChange({
      ...filters,
      search: localSearch.trim() || undefined,
    })
  }

  const handleCategoryChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    onFilterChange({
      ...filters,
      categoryId: e.target.value || undefined,
    })
  }

  const handleBrandChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    onFilterChange({
      ...filters,
      brandId: e.target.value || undefined,
    })
  }

  const handleBranchChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    onFilterChange({
      ...filters,
      branchId: e.target.value || undefined,
    })
  }

  const handlePresetClick = (min?: number, max?: number) => {
    setPriceError('')
    setLocalMinPrice(min !== undefined ? String(min) : '')
    setLocalMaxPrice(max !== undefined ? String(max) : '')
    onFilterChange({
      ...filters,
      minPrice: min,
      maxPrice: max,
    })
  }

  const handleReset = () => {
    setPriceError('')
    setLocalSearch('')
    setLocalMinPrice('')
    setLocalMaxPrice('')
    onReset()
  }

  const compareCategoryName = (a: CategoryDto, b: CategoryDto) => {
    if (a.slug === 'uncategorized') return 1
    if (b.slug === 'uncategorized') return -1
    return a.name.localeCompare(b.name, 'vi')
  }

  const categoryOptions = categories
    .filter((category) => category.parentCategoryId === null)
    .sort(compareCategoryName)
    .flatMap((parent) => [
      { category: parent, label: parent.name },
      ...categories
        .filter((child) => child.parentCategoryId === parent.id)
        .sort(compareCategoryName)
        .map((child) => ({ category: child, label: `— ${child.name}` })),
    ])

  const hasActiveFilters = Boolean(
    filters.search ||
    filters.categoryId ||
    filters.brandId ||
    filters.branchId ||
    filters.minPrice !== undefined ||
    filters.maxPrice !== undefined
  )

  return (
    <>
      {isMobileOpen && (
        <div
          className="filter-backdrop"
          onClick={onCloseMobile}
          aria-hidden="true"
        />
      )}

      <aside
        className={`filter-sidebar ${isMobileOpen ? 'filter-sidebar--open' : ''}`}
        aria-label="Bộ lọc tìm kiếm sản phẩm"
      >
        <div className="filter-sidebar__header">
          <div className="filter-sidebar__title-wrap">
            <svg
              className="filter-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3c2.755 0 5.455.232 8.083.678.533.09.917.556.917 1.096v1.044a2.25 2.25 0 01-.659 1.591l-5.432 5.432a2.25 2.25 0 00-.659 1.591v2.927a2.25 2.25 0 01-1.244 2.013L9.75 21v-6.568a2.25 2.25 0 00-.659-1.591L3.659 7.409A2.25 2.25 0 013 5.818V4.774c0-.54.384-1.006.917-1.096A48.32 48.32 0 0112 3z" />
            </svg>
            <h2>Bộ lọc tìm kiếm</h2>
          </div>
          {isMobileOpen && (
            <button
              type="button"
              className="filter-close-btn"
              onClick={onCloseMobile}
              aria-label="Đóng bộ lọc"
            >
              ✕
            </button>
          )}
        </div>

        {/* 1. Tìm kiếm từ khóa */}
        <section className="filter-group">
          <label htmlFor="filter-search-input" className="filter-group__label">
            Tìm kiếm từ khóa / SKU
          </label>
          <form onSubmit={handleSearchSubmit} className="filter-search-form">
            <div className="filter-search-input-wrap">
              <input
                id="filter-search-input"
                type="text"
                className="filter-input"
                placeholder="Nhập tên sản phẩm, SKU..."
                value={localSearch}
                onChange={(e) => setLocalSearch(e.target.value)}
              />
              {localSearch && (
                <button
                  type="button"
                  className="filter-search-clear"
                  onClick={() => {
                    setLocalSearch('')
                    onFilterChange({ ...filters, search: undefined })
                  }}
                  aria-label="Xóa từ khóa"
                >
                  ✕
                </button>
              )}
            </div>
            <button type="submit" className="filter-search-btn">
              Tìm
            </button>
          </form>
        </section>

        {/* 2. Chi nhánh kho hàng */}
        <section className="filter-group">
          <label htmlFor="filter-branch-select" className="filter-group__label">
            Chi nhánh siêu thị
          </label>
          <select
            id="filter-branch-select"
            className="filter-select"
            value={filters.branchId ?? ''}
            onChange={handleBranchChange}
          >
            <option value="">-- Tất cả chi nhánh --</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name} ({b.address})
              </option>
            ))}
          </select>
        </section>

        {/* 3. Danh mục sản phẩm */}
        <section className="filter-group">
          <label htmlFor="filter-category-select" className="filter-group__label">
            Danh mục sản phẩm
          </label>
          <select
            id="filter-category-select"
            className="filter-select"
            value={filters.categoryId ?? ''}
            onChange={handleCategoryChange}
          >
            <option value="">-- Tất cả danh mục --</option>
            {categoryOptions.map(({ category, label }) => (
              <option key={category.id} value={category.id}>{label}</option>
            ))}
          </select>
        </section>

        {/* 4. Thương hiệu */}
        <section className="filter-group">
          <label htmlFor="filter-brand-select" className="filter-group__label">
            Thương hiệu
          </label>
          <select
            id="filter-brand-select"
            className="filter-select"
            value={filters.brandId ?? ''}
            onChange={handleBrandChange}
          >
            <option value="">-- Tất cả thương hiệu --</option>
            {brands.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </section>

        {/* 5. Khoảng giá */}
        <section className="filter-group">
          <span className="filter-group__label">Khoảng giá (VNĐ)</span>
          <form onSubmit={handleApplyPrice} className="filter-price-form">
            <div className="filter-price-inputs">
              <input
                type="number"
                min="0"
                step="1000"
                className="filter-input filter-input--price"
                placeholder="Từ"
                value={localMinPrice}
                onChange={(e) => {
                  setLocalMinPrice(e.target.value)
                  setPriceError('')
                }}
                aria-label="Giá tối thiểu"
              />
              <span className="filter-price-separator">-</span>
              <input
                type="number"
                min="0"
                step="1000"
                className="filter-input filter-input--price"
                placeholder="Đến"
                value={localMaxPrice}
                onChange={(e) => {
                  setLocalMaxPrice(e.target.value)
                  setPriceError('')
                }}
                aria-label="Giá tối đa"
              />
            </div>
            {priceError && (
              <div className="filter-price-error" role="alert">
                {priceError}
              </div>
            )}
            <button type="submit" className="filter-price-btn">
              Áp dụng giá
            </button>
          </form>

          {/* Quick presets */}
          <div className="filter-presets">
            {PRICE_PRESETS.map((preset, idx) => {
              const isActive =
                filters.minPrice === preset.min && filters.maxPrice === preset.max
              return (
                <button
                  key={idx}
                  type="button"
                  className={`filter-preset-chip ${isActive ? 'filter-preset-chip--active' : ''}`}
                  onClick={() => handlePresetClick(preset.min, preset.max)}
                >
                  {preset.label}
                </button>
              )
            })}
          </div>
        </section>

        {/* Nút đặt lại */}
        <div className="filter-actions">
          <button
            type="button"
            className="filter-reset-btn"
            onClick={handleReset}
            disabled={!hasActiveFilters}
          >
            <svg
              className="filter-reset-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
            </svg>
            Xóa toàn bộ bộ lọc
          </button>
        </div>
      </aside>
    </>
  )
}
