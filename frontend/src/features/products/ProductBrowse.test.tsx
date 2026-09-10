import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { ProductCard, formatPrice } from './ProductCard'
import { ProductGrid } from './ProductGrid'
import { FilterSidebar } from './FilterSidebar'
import { Pagination } from './Pagination'
import { ProductBrowsePage } from './ProductBrowsePage'
import { catalogApi } from '../../api/catalogApi'
import { branchApi } from '../../api/branchApi'
import { CompareProvider } from '../compare/CompareContext'

const mockProduct = {
  id: 'prod-1',
  name: 'Sữa tươi tiệt trùng ít đường 1L',
  slug: 'sua-tuoi-tiet-trung-it-duong-1l',
  sku: 'MILK-001',
  basePrice: 38000,
  imageUrl: 'https://example.com/milk.jpg',
  categoryId: 'cat-2',
  categoryName: 'Sữa & Bơ sữa',
  categorySlug: 'sua-bo-sua',
  brandName: 'Vinamilk',
}

const mockCategories = [
  { id: 'cat-1', name: 'Rau củ quả', slug: 'rau-cu-qua', parentCategoryId: null, isActive: true },
  { id: 'cat-2', name: 'Sữa & Bơ sữa', slug: 'sua-bo-sua', parentCategoryId: null, isActive: true },
]

const mockBrands = [
  { id: 'brand-1', name: 'Vinamilk', slug: 'vinamilk', isActive: true },
  { id: 'brand-2', name: 'TH True Milk', slug: 'th-true-milk', isActive: true },
]

const mockBranches = [
  {
    id: 'branch-1',
    name: 'Chi nhánh Quận 1',
    address: '123 Lê Lợi, Q1, TP.HCM',
    phone: '0901234567',
    latitude: 10.77,
    longitude: 106.7,
    isActive: true,
  },
]

describe('Product Browse Feature', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('formatPrice helper', () => {
    it('formats numbers into VND string correctly', () => {
      const formatted = formatPrice(38000)
      expect(formatted).toContain('38.000')
      expect(formatted).toMatch(/₫|đ|VND/i)
    })
  })

  describe('ProductCard component', () => {
    it('renders product information properly', () => {
      render(
        <MemoryRouter>
          <CompareProvider>
            <ProductCard product={mockProduct} branchName="Chi nhánh Quận 1" />
          </CompareProvider>
        </MemoryRouter>
      )

      expect(screen.getByText('Sữa tươi tiệt trùng ít đường 1L')).toBeInTheDocument()
      expect(screen.getByText('Vinamilk')).toBeInTheDocument()
      expect(screen.getByText('Sữa & Bơ sữa')).toBeInTheDocument()
      expect(screen.getByText(/SKU: MILK-001/)).toBeInTheDocument()
      expect(screen.getByText(/38\.000/)).toBeInTheDocument()
      expect(screen.getByText('Chi nhánh Quận 1')).toBeInTheDocument()
    })

    it('renders a native detail link and preserves branchId', () => {
      render(
        <MemoryRouter>
          <CompareProvider>
            <ProductCard product={mockProduct} branchName="Chi nhánh Quận 1" branchId="branch-1" />
          </CompareProvider>
        </MemoryRouter>
      )

      expect(screen.getByRole('link', { name: `Xem ${mockProduct.name}` }))
        .toHaveAttribute('href', '/product/prod-1?branchId=branch-1')
    })

    it('omits branchId when no branch is selected', () => {
      render(
        <MemoryRouter>
          <CompareProvider>
            <ProductCard product={mockProduct} branchName="Chi nhánh Quận 1" />
          </CompareProvider>
        </MemoryRouter>
      )

      const linkWithoutBranch = screen.getByRole('link', { name: `Xem ${mockProduct.name}` })
      expect(linkWithoutBranch).toHaveAttribute('href', '/product/prod-1')
    })
  })

  describe('FilterSidebar component', () => {
    it('renders roots, then children, then uncategorized last and filters by leaf', () => {
      const onFilterChange = vi.fn()
      const topLevelCategories = [
        { id: 'tv-parent', name: 'TV & Màn hình', slug: 'tv-man-hinh', parentCategoryId: null, isActive: true },
        { id: 'tv', name: 'TV', slug: 'tivi', parentCategoryId: 'tv-parent', isActive: true },
        { id: 'monitor', name: 'Màn hình máy tính', slug: 'man-hinh-may-tinh', parentCategoryId: 'tv-parent', isActive: true },
        { id: 'uncategorized', name: 'Chưa phân loại', slug: 'uncategorized', parentCategoryId: null, isActive: true },
      ]

      render(
        <FilterSidebar
          categories={topLevelCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{}}
          onFilterChange={onFilterChange}
          onReset={vi.fn()}
        />
      )

      const select = screen.getByLabelText('Danh mục sản phẩm')
      const labels = within(select).getAllByRole('option').map((option) => option.textContent)
      expect(labels).toEqual([
        '-- Tất cả danh mục --',
        'TV & Màn hình',
        '— Màn hình máy tính',
        '— TV',
        'Chưa phân loại',
      ])

      fireEvent.change(select, { target: { value: 'monitor' } })
      expect(onFilterChange).toHaveBeenCalledWith(
        expect.objectContaining({ categoryId: 'monitor' })
      )
    })

    it('renders categories, brands, branches and handles filter changes', () => {
      const onFilterChange = vi.fn()
      const onReset = vi.fn()

      render(
        <FilterSidebar
          categories={mockCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{}}
          onFilterChange={onFilterChange}
          onReset={onReset}
        />
      )

      // Test category selection
      const categorySelect = screen.getByLabelText('Danh mục sản phẩm')
      fireEvent.change(categorySelect, { target: { value: 'cat-2' } })
      expect(onFilterChange).toHaveBeenCalledWith(
        expect.objectContaining({ categoryId: 'cat-2' })
      )

      // Test brand selection
      const brandSelect = screen.getByLabelText('Thương hiệu')
      fireEvent.change(brandSelect, { target: { value: 'brand-1' } })
      expect(onFilterChange).toHaveBeenCalledWith(
        expect.objectContaining({ brandId: 'brand-1' })
      )

      // Test search keyword
      const searchInput = screen.getByPlaceholderText('Nhập tên sản phẩm, SKU...')
      fireEvent.change(searchInput, { target: { value: 'sữa tươi' } })
      const searchSubmitBtn = screen.getByRole('button', { name: 'Tìm' })
      fireEvent.click(searchSubmitBtn)
      expect(onFilterChange).toHaveBeenCalledWith(
        expect.objectContaining({ search: 'sữa tươi' })
      )
    })

    it('validates minPrice > maxPrice and prevents applying invalid price filter', () => {
      const onFilterChange = vi.fn()
      render(
        <FilterSidebar
          categories={mockCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{}}
          onFilterChange={onFilterChange}
          onReset={vi.fn()}
        />
      )

      const minPriceInput = screen.getByPlaceholderText('Từ')
      const maxPriceInput = screen.getByPlaceholderText('Đến')
      const applyBtn = screen.getByRole('button', { name: 'Áp dụng giá' })

      fireEvent.change(minPriceInput, { target: { value: '5000000' } })
      fireEvent.change(maxPriceInput, { target: { value: '1000000' } })
      fireEvent.click(applyBtn)

      expect(screen.getByText('Giá tối thiểu không được lớn hơn giá tối đa.')).toBeInTheDocument()
      expect(onFilterChange).not.toHaveBeenCalled()
    })

    it('clears price error when a preset chip is selected', () => {
      const onFilterChange = vi.fn()
      render(
        <FilterSidebar
          categories={mockCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{}}
          onFilterChange={onFilterChange}
          onReset={vi.fn()}
        />
      )

      const minPriceInput = screen.getByPlaceholderText('Từ')
      const maxPriceInput = screen.getByPlaceholderText('Đến')
      const applyBtn = screen.getByRole('button', { name: 'Áp dụng giá' })

      fireEvent.change(minPriceInput, { target: { value: '5000000' } })
      fireEvent.change(maxPriceInput, { target: { value: '1000000' } })
      fireEvent.click(applyBtn)

      expect(screen.getByText('Giá tối thiểu không được lớn hơn giá tối đa.')).toBeInTheDocument()

      const presetBtn = screen.getByRole('button', { name: 'Dưới 50.000đ' })
      fireEvent.click(presetBtn)

      expect(screen.queryByText('Giá tối thiểu không được lớn hơn giá tối đa.')).not.toBeInTheDocument()
      expect(onFilterChange).toHaveBeenCalledWith(expect.objectContaining({ maxPrice: 50000 }))
    })

    it('clears price error when reset button is clicked', () => {
      const onReset = vi.fn()
      render(
        <FilterSidebar
          categories={mockCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{ search: 'phone' }}
          onFilterChange={vi.fn()}
          onReset={onReset}
        />
      )

      const minPriceInput = screen.getByPlaceholderText('Từ')
      const maxPriceInput = screen.getByPlaceholderText('Đến')
      const applyBtn = screen.getByRole('button', { name: 'Áp dụng giá' })

      fireEvent.change(minPriceInput, { target: { value: '9000000' } })
      fireEvent.change(maxPriceInput, { target: { value: '1000000' } })
      fireEvent.click(applyBtn)

      expect(screen.getByText('Giá tối thiểu không được lớn hơn giá tối đa.')).toBeInTheDocument()

      const resetBtn = screen.getByRole('button', { name: /Xóa toàn bộ bộ lọc/i })
      fireEvent.click(resetBtn)

      expect(screen.queryByText('Giá tối thiểu không được lớn hơn giá tối đa.')).not.toBeInTheDocument()
      expect(onReset).toHaveBeenCalled()
    })

    it('handles reset button when filters are active', () => {
      const onReset = vi.fn()
      render(
        <FilterSidebar
          categories={mockCategories}
          brands={mockBrands}
          branches={mockBranches}
          filters={{ search: 'test' }}
          onFilterChange={vi.fn()}
          onReset={onReset}
        />
      )

      const resetBtn = screen.getByRole('button', { name: /Xóa toàn bộ bộ lọc/i })
      expect(resetBtn).not.toBeDisabled()
      fireEvent.click(resetBtn)
      expect(onReset).toHaveBeenCalled()
    })
  })

  describe('ProductGrid component', () => {
    it('shows skeleton loading state when loading is true', () => {
      render(<ProductGrid products={[]} loading={true} />)
      const skeletons = screen.getAllByTestId('product-skeleton')
      expect(skeletons.length).toBeGreaterThan(0)
    })

    it('shows empty state when no products and loading is false', () => {
      const onReset = vi.fn()
      render(<ProductGrid products={[]} loading={false} onResetFilters={onReset} />)
      expect(screen.getByText('Không tìm thấy sản phẩm phù hợp')).toBeInTheDocument()
      const resetBtn = screen.getByRole('button', { name: 'Xóa bộ lọc tìm kiếm' })
      fireEvent.click(resetBtn)
      expect(onReset).toHaveBeenCalled()
    })

    it('shows error message and retry button when error occurs', () => {
      const onRetry = vi.fn()
      render(
        <ProductGrid
          products={[]}
          loading={false}
          error="Network Error 500"
          onRetry={onRetry}
        />
      )
      expect(screen.getByText('Không thể tải danh sách sản phẩm')).toBeInTheDocument()
      expect(screen.getByText('Network Error 500')).toBeInTheDocument()
      const retryBtn = screen.getByRole('button', { name: 'Thử lại' })
      fireEvent.click(retryBtn)
      expect(onRetry).toHaveBeenCalled()
    })

    it('renders list of products when available', () => {
      render(
        <MemoryRouter>
          <CompareProvider>
            <ProductGrid products={[mockProduct]} loading={false} />
          </CompareProvider>
        </MemoryRouter>
      )
      expect(screen.getByTestId('product-grid')).toBeInTheDocument()
      expect(screen.getByText('Sữa tươi tiệt trùng ít đường 1L')).toBeInTheDocument()
    })
  })

  describe('Pagination component', () => {
    it('renders page buttons and triggers onPageChange', () => {
      const onPageChange = vi.fn()
      render(
        <Pagination
          meta={{ totalCount: 45, page: 2, pageSize: 20, totalPages: 3 }}
          onPageChange={onPageChange}
        />
      )

      expect(screen.getByText(/Hiển thị/)).toBeInTheDocument()
      expect(screen.getByText('21')).toBeInTheDocument()
      expect(screen.getByText('40')).toBeInTheDocument()
      expect(screen.getByText('45')).toBeInTheDocument()

      const page3Btn = screen.getByRole('button', { name: 'Trang 3' })
      fireEvent.click(page3Btn)
      expect(onPageChange).toHaveBeenCalledWith(3)

      const prevBtn = screen.getByRole('button', { name: 'Trang trước' })
      fireEvent.click(prevBtn)
      expect(onPageChange).toHaveBeenCalledWith(1)
    })
  })

  describe('ProductBrowsePage component', () => {
    it('fetches products, categories, brands, branches and displays them', async () => {
      vi.spyOn(catalogApi, 'getCategories').mockResolvedValue(mockCategories)
      vi.spyOn(catalogApi, 'getBrands').mockResolvedValue(mockBrands)
      vi.spyOn(branchApi, 'getBranches').mockResolvedValue(mockBranches)
      vi.spyOn(catalogApi, 'getProducts').mockResolvedValue({
        items: [mockProduct],
        meta: {
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        },
      })

      render(
        <MemoryRouter initialEntries={['/browse']}>
          <CompareProvider>
            <ProductBrowsePage />
          </CompareProvider>
        </MemoryRouter>
      )

      expect(screen.getByText('Duyệt & Tìm Kiếm Sản Phẩm')).toBeInTheDocument()

      await waitFor(() => {
        expect(screen.getByText('Sữa tươi tiệt trùng ít đường 1L')).toBeInTheDocument()
      })
    })
  })
})
