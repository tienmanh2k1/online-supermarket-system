import { StrictMode } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { recommendationApi } from '../../api/recommendationApi'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { catalogApi } from '../../api/catalogApi'
import { useAuth } from '../auth/AuthContext'
import { useCart } from '../cart/CartContext'
import { useCompare } from '../compare/CompareContext'
import { ProductDetailPage } from './ProductDetailPage'

vi.mock('../auth/AuthContext', () => ({ useAuth: vi.fn(), useOptionalAuth: vi.fn() }))
vi.mock('../cart/CartContext', () => ({ useCart: vi.fn() }))
vi.mock('../compare/CompareContext', () => ({ useCompare: vi.fn() }))
vi.mock('../auth/AuthModal', () => ({ AuthModal: () => null }))
vi.mock('../reviews/ProductReviews', () => ({ ProductReviews: () => null }))

const useAuthMock = vi.mocked(useAuth)
const useCartMock = vi.mocked(useCart)
const useCompareMock = vi.mocked(useCompare)

let recordView: ReturnType<typeof vi.spyOn>

const product = {
  id: 'prod-view',
  name: 'Sản phẩm capture',
  slug: 'san-pham-capture',
  sku: 'SKU-CAPTURE-1',
  description: null,
  basePrice: 100000,
  unit: 'cái',
  imageUrl: null,
  categoryId: 'cat-1',
  categoryName: 'Danh mục',
  categorySlug: 'danh-muc',
  brandId: 'brand-1',
  brandName: 'Brand',
  branchInventory: null,
}

const branches: BranchDto[] = [
  { id: 'branch-1', name: 'Cửa hàng 1', address: '1 Test Street', phone: null, latitude: null, longitude: null, isActive: true },
]

const useAuthValue = {
  user: null,
  accessToken: null,
  isAuthenticated: false,
  isLoading: false,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  updateUser: vi.fn(),
  refreshUser: vi.fn(),
}

function authenticate() {
  useAuthMock.mockReturnValue({
    ...useAuthValue,
    user: { id: 'user-1', email: 'user@test.com', fullName: 'Người mua', role: 'Customer' },
    accessToken: 'jwt-token',
    isAuthenticated: true,
  })
}

function renderDetail(entry: string) {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route path="/product/:id" element={<ProductDetailPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ProductDetailPage view capture', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAuthMock.mockReturnValue(useAuthValue)
    useCartMock.mockReturnValue({
      status: 'idle',
      cart: null,
      errorMessage: null,
      mutatingItemIds: new Set(),
      isAddingItem: false,
      isChangingBranch: false,
      isClearing: false,
      reloadCart: vi.fn(),
      addItem: vi.fn(),
      updateItemQuantity: vi.fn(),
      removeItem: vi.fn(),
      changeBranch: vi.fn(),
      clearCart: vi.fn(),
    })
    useCompareMock.mockReturnValue({
      compareProducts: [],
      isInCompare: vi.fn(() => false),
      addToCompare: vi.fn(() => true),
      removeFromCompare: vi.fn(),
      clearCompare: vi.fn(),
      canAddMore: true,
      hasProduct: false,
      getDifferentCategoryWarning: vi.fn(() => null),
      openModal: vi.fn(),
      closeModal: vi.fn(),
      isModalOpen: false,
    })
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(product)
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    recordView = vi.spyOn(recommendationApi, 'recordView')
    recordView.mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('records one view for a guest with the anonymous session id', async () => {
    renderDetail('/product/prod-guest-1')

    await screen.findByRole('heading', { name: product.name })

    expect(recordView).toHaveBeenCalledTimes(1)
    expect(recordView).toHaveBeenCalledWith(
      'prod-guest-1',
      expect.objectContaining({ anonymousSessionId: expect.any(String) }),
      undefined,
    )
  })

  it('records the view with the bearer token for an authenticated user', async () => {
    authenticate()
    renderDetail('/product/prod-auth-1')

    await screen.findByRole('heading', { name: product.name })

    expect(recordView).toHaveBeenCalledTimes(1)
    expect(recordView).toHaveBeenCalledWith(
      'prod-auth-1',
      expect.objectContaining({ anonymousSessionId: expect.any(String) }),
      'jwt-token',
    )
  })

  it('records a single view despite StrictMode effect replay', async () => {
    render(
      <StrictMode>
        <MemoryRouter initialEntries={['/product/prod-strict-1']}>
          <Routes>
            <Route path="/product/:id" element={<ProductDetailPage />} />
          </Routes>
        </MemoryRouter>
      </StrictMode>,
    )

    await screen.findByRole('heading', { name: product.name })

    expect(recordView).toHaveBeenCalledTimes(1)
    expect(recordView).toHaveBeenCalledWith(
      'prod-strict-1',
      expect.objectContaining({ anonymousSessionId: expect.any(String) }),
      undefined,
    )
  })

  it('keeps the content rendered when the capture request fails', async () => {
    recordView.mockRejectedValue(new Error('offline'))
    renderDetail('/product/prod-fail-1')

    expect(await screen.findByRole('heading', { name: product.name })).toBeInTheDocument()
  })

  it('does not record a second view when navigating back to the same product', async () => {
    renderDetail('/product/prod-repeat-1')

    await screen.findByRole('heading', { name: product.name })
    renderDetail('/product/prod-repeat-1')

    expect(recordView).toHaveBeenCalledTimes(1)
  })

  it('warns with confirm dialog when switching to a different branch and cart has items', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue([
      { id: 'branch-1', name: 'Cửa hàng 1', address: '1 Test Street', phone: null, latitude: null, longitude: null, isActive: true },
      { id: 'branch-2', name: 'Cửa hàng 2', address: '2 Test Street', phone: null, latitude: null, longitude: null, isActive: true },
    ])

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true)
    useCartMock.mockReturnValue({
      status: 'ready',
      cart: {
        id: 'cart-1',
        branchId: 'branch-1',
        branchName: 'Cửa hàng 1',
        items: [{ id: 'item-1', productId: 'p1', productName: 'P1', quantity: 1, unitPrice: 1000, lineTotal: 1000 }],
        subtotal: 1000,
        totalItems: 1,
      } as any,
      errorMessage: null,
      mutatingItemIds: new Set(),
      isAddingItem: false,
      isChangingBranch: false,
      isClearing: false,
      reloadCart: vi.fn(),
      addItem: vi.fn(),
      updateItemQuantity: vi.fn(),
      removeItem: vi.fn(),
      changeBranch: vi.fn(),
      clearCart: vi.fn(),
    })

    renderDetail('/product/prod-view?branchId=branch-1')
    await screen.findByRole('heading', { name: product.name })

    // Wait for branch-2 option to be loaded into the select
    await screen.findByRole('option', { name: 'Cửa hàng 2' })

    const branchSelect = screen.getByLabelText('Kho hàng')
    fireEvent.change(branchSelect, { target: { value: 'branch-2' } })

    expect(confirmSpy).toHaveBeenCalledWith(
      expect.stringContaining('Giỏ hàng hiện tại có sản phẩm từ chi nhánh khác')
    )
  })
})