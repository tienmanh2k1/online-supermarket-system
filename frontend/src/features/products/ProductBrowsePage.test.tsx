import React from 'react'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { ProductBrowsePage } from './ProductBrowsePage'
import { catalogApi } from '../../api/catalogApi'
import { branchApi } from '../../api/branchApi'
import { recommendationApi } from '../../api/recommendationApi'
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

describe('ProductBrowsePage Lifecycle & Error Handling', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(catalogApi, 'getProducts').mockResolvedValue({
      items: [mockProduct],
      meta: { totalCount: 1, page: 1, pageSize: 20, totalPages: 1 },
    })
    vi.spyOn(recommendationApi, 'getRecommendations').mockResolvedValue({ items: [] })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders correctly under StrictMode with abortable promises and prevents race condition', async () => {
    vi.spyOn(catalogApi, 'getCategories').mockResolvedValue(mockCategories)
    vi.spyOn(catalogApi, 'getBrands').mockResolvedValue(mockBrands)

    let resolveFirstCall: (data: typeof mockBranches) => void
    let callCount = 0

    vi.spyOn(branchApi, 'getBranches').mockImplementation(() => {
      callCount++
      if (callCount === 1) {
        return new Promise((resolve) => {
          resolveFirstCall = resolve
        })
      }
      return Promise.resolve(mockBranches)
    })

    render(
      <React.StrictMode>
        <MemoryRouter initialEntries={['/browse?branchId=branch-1']}>
          <CompareProvider>
            <ProductBrowsePage />
          </CompareProvider>
        </MemoryRouter>
      </React.StrictMode>
    )

    if (callCount === 1) {
      resolveFirstCall!(mockBranches)
    }

    await waitFor(() => {
      expect(screen.getByText('123 Lê Lợi, Q1, TP.HCM')).toBeInTheDocument()
    })

    // If an earlier promise resolves late with empty/stale data after cleanup
    if (resolveFirstCall!) {
      resolveFirstCall!([])
    }

    // Confirm that state retains the valid branch data and is not cleared
    await waitFor(() => {
      expect(screen.getByText('123 Lê Lợi, Q1, TP.HCM')).toBeInTheDocument()
      expect(screen.getAllByText('Chi nhánh Quận 1').length).toBeGreaterThanOrEqual(1)
    })
  })

  it('displays error banner and retry button when branch API returns HTTP 500 error', async () => {
    vi.spyOn(catalogApi, 'getCategories').mockResolvedValue(mockCategories)
    vi.spyOn(catalogApi, 'getBrands').mockResolvedValue(mockBrands)
    vi.spyOn(branchApi, 'getBranches').mockRejectedValue(new Error('Internal Server Error 500'))

    const { container } = render(
      <MemoryRouter initialEntries={['/browse?branchId=branch-1']}>
        <CompareProvider>
          <ProductBrowsePage />
        </CompareProvider>
      </MemoryRouter>
    )

    await waitFor(() => {
      const metaErrorEl = container.querySelector('.product-browse-meta-error')
      expect(metaErrorEl).toBeInTheDocument()
      expect(metaErrorEl).toHaveTextContent('Không thể tải thông tin hệ thống')
    })

    // When user clicks retry after branchApi recovers
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(mockBranches)

    const retryBtn = container.querySelector('.product-browse-retry-btn') as HTMLButtonElement
    expect(retryBtn).toBeInTheDocument()
    fireEvent.click(retryBtn)

    await waitFor(() => {
      expect(container.querySelector('.product-browse-meta-error')).not.toBeInTheDocument()
      expect(screen.getByText('123 Lê Lợi, Q1, TP.HCM')).toBeInTheDocument()
      expect(screen.getAllByText('Chi nhánh Quận 1').length).toBeGreaterThanOrEqual(1)
    })
  })
})
