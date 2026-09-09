import { render, screen, waitFor, act, fireEvent } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Routes, Route, useLocation, useNavigate, type NavigateFunction } from 'react-router-dom'
import { catalogApi, type ProductDetailDto } from '../../api/catalogApi'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { ApiError } from '../../api/httpClient'
import { useAuth } from '../auth/AuthContext'
import { useCart } from '../cart/CartContext'
import { useCompare } from '../compare/CompareContext'
import type { CartDto, CartItemDto } from '../../api/cartApi'
import { ProductDetailPage } from './ProductDetailPage'

vi.mock('../auth/AuthContext', () => ({ useAuth: vi.fn(), useOptionalAuth: vi.fn() }))
vi.mock('../cart/CartContext', () => ({ useCart: vi.fn() }))
vi.mock('../compare/CompareContext', () => ({ useCompare: vi.fn() }))
vi.mock('../../api/reviewApi', () => ({
  reviewApi: {
    getProductReviews: vi.fn().mockResolvedValue({
      averageRating: 0,
      reviewCount: 0,
      data: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
    }),
    getEligibility: vi.fn().mockResolvedValue({
      canReview: false,
      orderItemId: null,
      reviewId: null,
    }),
    createReview: vi.fn(),
    updateReview: vi.fn(),
  },
}))
vi.mock('../auth/AuthModal', () => ({
  AuthModal: ({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) =>
    isOpen ? (
      <div role="dialog" aria-label="Đăng nhập">
        <button onClick={onClose}>Hoàn tất đăng nhập</button>
      </div>
    ) : null,
}))

const useAuthMock = vi.mocked(useAuth)
const useCartMock = vi.mocked(useCart)
const useCompareMock = vi.mocked(useCompare)
const addItem = vi.fn()
const changeBranch = vi.fn()

const mockDetail: ProductDetailDto = {
  id: 'prod-1',
  name: 'iPhone 15 128GB',
  slug: 'iphone-15-128gb',
  sku: 'DT-APP-002',
  description: 'Màn hình Super Retina XDR.',
  basePrice: 22990000,
  unit: 'cái',
  imageUrl: null,
  categoryId: 'cat-1',
  categoryName: 'Điện thoại & Tablet',
  categorySlug: 'dien-thoai',
  brandId: 'brand-1',
  brandName: 'Apple',
  branchInventory: null,
}

const branches: BranchDto[] = [
  { id: 'branch-1', name: 'AptechMart Quận 1', address: '123 Nguyễn Huệ', phone: null, latitude: null, longitude: null, isActive: true },
  { id: 'branch-2', name: 'AptechMart Quận 3', address: '456 Đường 3 Tháng 2', phone: null, latitude: null, longitude: null, isActive: true },
]

const guestAuth: ReturnType<typeof useAuth> = {
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

const authenticatedAuth: ReturnType<typeof useAuth> = {
  ...guestAuth,
  user: {
    id: 'user-1',
    email: 'user@example.com',
    fullName: 'Người mua',
    role: 'Customer',
  },
  accessToken: 'jwt-token',
  isAuthenticated: true,
}

const existingItem: CartItemDto = {
  id: 'item-existing',
  productId: 'prod-existing',
  productName: 'Sản phẩm cũ',
  sku: 'SKU-OLD',
  unitPrice: 100000,
  quantity: 1,
  lineTotal: 100000,
  availableQuantity: 3,
}

const baseCart: CartDto = {
  id: 'cart-1',
  userId: 'user-1',
  branchId: 'branch-2',
  items: [existingItem],
  totalItems: 1,
  subtotal: 100000,
}

const newBranchCart: CartDto = {
  ...baseCart,
  branchId: 'branch-1',
  items: [],
  totalItems: 0,
  subtotal: 0,
}

const cartWithProduct: CartDto = {
  ...newBranchCart,
  items: [
    {
      id: 'item-new',
      productId: 'prod-1',
      productName: mockDetail.name,
      sku: mockDetail.sku,
      unitPrice: 21990000,
      quantity: 1,
      lineTotal: 21990000,
      availableQuantity: 7,
    },
  ],
  totalItems: 1,
  subtotal: 21990000,
}

function mockProductDetail(overrides: Partial<ProductDetailDto> = {}) {
  vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
  vi.spyOn(catalogApi, 'getProductById').mockResolvedValue({
    ...mockDetail,
    ...overrides,
  })
}

function mockInStockProduct() {
  mockProductDetail({
    branchInventory: {
      branchId: 'branch-1',
      sellingPrice: 21990000,
      availableQuantity: 7,
      onHand: 9,
    },
  })
}

function mockCart(cart: CartDto | null) {
  useCartMock.mockReturnValue({
    status: cart ? 'ready' : 'idle',
    cart,
    errorMessage: null,
    mutatingItemIds: new Set(),
    isAddingItem: false,
    isChangingBranch: false,
    isClearing: false,
    reloadCart: vi.fn(),
    addItem,
    updateItemQuantity: vi.fn(),
    removeItem: vi.fn(),
    changeBranch,
    clearCart: vi.fn(),
  })
}

function renderBranchMismatchDetail({ cartItems }: { cartItems: CartItemDto[] }) {
  useAuthMock.mockReturnValue(authenticatedAuth)
  mockCart({
    ...baseCart,
    items: cartItems,
    totalItems: cartItems.reduce((sum, item) => sum + item.quantity, 0),
    subtotal: cartItems.reduce((sum, item) => sum + item.lineTotal, 0),
  })
  mockInStockProduct()
  return renderDetail('/product/prod-1?branchId=branch-1')
}

function renderSameBranchDetail() {
  useAuthMock.mockReturnValue(authenticatedAuth)
  mockCart(newBranchCart)
  mockInStockProduct()
  return renderDetail('/product/prod-1?branchId=branch-1')
}

function renderGuestDetail() {
  useAuthMock.mockReturnValue(guestAuth)
  mockCart(null)
  mockInStockProduct()
  return renderDetail('/product/prod-1?branchId=branch-1')
}

function LocationDisplay() {
  const location = useLocation()
  return <div data-testid="detail-location">{location.search}</div>
}

// Exposes navigate so tests can change URL programmatically
interface RenderDetailResult extends ReturnType<typeof render> {
  navigate: NavigateFunction
}

function renderDetail(entry = '/product/prod-1'): RenderDetailResult {
  let navigateRef: NavigateFunction | undefined
  function CaptureNavigate() {
    navigateRef = useNavigate()
    return null
  }
  const result = render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route path="/product/:id" element={<ProductDetailPage />} />
        <Route path="/browse" element={<div>Browse</div>} />
      </Routes>
      <LocationDisplay />
      <CaptureNavigate />
    </MemoryRouter>
  )
  return { ...result, navigate: navigateRef! }
}

describe('ProductDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAuthMock.mockReturnValue(guestAuth)
    useCartMock.mockReturnValue({
      status: 'idle',
      cart: null,
      errorMessage: null,
      mutatingItemIds: new Set(),
      isAddingItem: false,
      isChangingBranch: false,
      isClearing: false,
      reloadCart: vi.fn(),
      addItem,
      updateItemQuantity: vi.fn(),
      removeItem: vi.fn(),
      changeBranch,
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
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('loads and renders base product detail without a branch', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue([])
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(mockDetail)
    renderDetail()
    expect(screen.getByLabelText('Đang tải chi tiết sản phẩm')).toHaveAttribute('aria-busy', 'true')
    expect(await screen.findByRole('heading', { name: 'iPhone 15 128GB' })).toBeInTheDocument()
    expect(screen.getByText('Màn hình Super Retina XDR.')).toBeInTheDocument()
    expect(screen.getByText(/22\.990\.000/)).toBeInTheDocument()
    expect(screen.getByText('Chọn kho để xem giá và tồn kho')).toBeInTheDocument()
    expect(catalogApi.getProductById).toHaveBeenCalledWith('prod-1', undefined, expect.any(AbortSignal))
  })

  it('loads the branch from a deep link', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue({
      ...mockDetail,
      branchInventory: { branchId: 'branch-1', sellingPrice: 21990000, availableQuantity: 7, onHand: 9 },
    })
    renderDetail('/product/prod-1?branchId=branch-1')
    expect(await screen.findByLabelText('Kho hàng')).toHaveValue('branch-1')
    expect(catalogApi.getProductById).toHaveBeenCalledWith('prod-1', 'branch-1', expect.any(AbortSignal))
  })

  it('updates branchId, preserves query keys, and refetches detail', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    const detailSpy = vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(mockDetail)
    renderDetail('/product/prod-1?ref=browse')
    const select = await screen.findByLabelText('Kho hàng')
    fireEvent.change(select, { target: { value: 'branch-2' } })
    await waitFor(() => expect(detailSpy).toHaveBeenLastCalledWith(
      'prod-1', 'branch-2', expect.any(AbortSignal)
    ))
    expect(screen.getByTestId('detail-location')).toHaveTextContent('?ref=browse&branchId=branch-2')
  })

  it.each([
    [undefined, null, 'Chọn kho để xem giá và tồn kho', /22\.990\.000/],
    ['branch-1', { branchId: 'branch-1', sellingPrice: 21990000, availableQuantity: 7, onHand: 9 }, 'Còn 7 sản phẩm tại kho', /21\.990\.000/],
    ['branch-1', { branchId: 'branch-1', sellingPrice: 21990000, availableQuantity: 0, onHand: 2 }, 'Tạm hết hàng tại kho này', /21\.990\.000/],
    ['branch-1', null, 'Sản phẩm không có tại kho này', /22\.990\.000/],
  ])('renders price and inventory for branch %s', async (branchId, inventory, message, price) => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue({ ...mockDetail, branchInventory: inventory })
    renderDetail(`/product/prod-1${branchId ? `?branchId=${branchId}` : ''}`)
    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(screen.getByText(price)).toBeInTheDocument()
  })

  it('falls back when the product image cannot load', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue([])
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue({
      ...mockDetail,
      imageUrl: 'https://example.com/broken.jpg',
    })
    renderDetail()
    fireEvent.error(await screen.findByRole('img', { name: 'iPhone 15 128GB' }))
    expect(screen.getByText('Hình ảnh sản phẩm')).toBeInTheDocument()
  })

  it('renders a dedicated 404 state', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue([])
    vi.spyOn(catalogApi, 'getProductById').mockRejectedValue(new ApiError(404))
    renderDetail()
    expect(await screen.findByRole('heading', { name: 'Không tìm thấy sản phẩm' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Quay lại danh sách sản phẩm' }))
      .toHaveAttribute('href', '/browse')
  })

  it('retries a generic product error', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue([])
    const detailSpy = vi.spyOn(catalogApi, 'getProductById')
      .mockRejectedValueOnce(new ApiError(500))
      .mockResolvedValueOnce(mockDetail)
    renderDetail()
    fireEvent.click(await screen.findByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByRole('heading', { name: 'iPhone 15 128GB' })).toBeInTheDocument()
    expect(detailSpy).toHaveBeenCalledTimes(2)
  })

  it('keeps product usable when branches fail and retries branches', async () => {
    const branchSpy = vi.spyOn(branchApi, 'getBranches')
      .mockRejectedValueOnce(new ApiError(500))
      .mockResolvedValueOnce(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(mockDetail)
    renderDetail()
    expect(await screen.findByRole('heading', { name: 'iPhone 15 128GB' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Không thể tải danh sách kho')
    fireEvent.click(screen.getByRole('button', { name: 'Tải lại danh sách kho' }))
    expect(await screen.findByLabelText('Kho hàng')).toBeEnabled()
    expect(branchSpy).toHaveBeenCalledTimes(2)
  })

  it('exposes accessible detail semantics', async () => {
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(mockDetail)
    renderDetail()
    expect(await screen.findByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument()
    expect(screen.getByLabelText('Kho hàng')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'iPhone 15 128GB' })).toBeInTheDocument()
  })

  describe('Stale-request protection', () => {
    it('clears branchId from URL when "Chọn kho" is selected', async () => {
      vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
      vi.spyOn(catalogApi, 'getProductById').mockResolvedValue({
        ...mockDetail,
        branchInventory: { branchId: 'branch-1', sellingPrice: 21990000, availableQuantity: 7, onHand: 9 },
      })
      renderDetail('/product/prod-1?branchId=branch-1')
      await screen.findByText('Còn 7 sản phẩm tại kho')

      const select = screen.getByLabelText('Kho hàng')
      fireEvent.change(select, { target: { value: '' } })

      await waitFor(() => {
        expect(screen.getByTestId('detail-location')).toHaveTextContent('')
      })
      expect(screen.getByText('Chọn kho để xem giá và tồn kho')).toBeInTheDocument()
    })

    it('normalizes invalid branchId from URL', async () => {
      vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
      vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(mockDetail)
      renderDetail('/product/prod-1?branchId=invalid-branch')
      await screen.findByText('Chọn kho để xem giá và tồn kho')
      expect(screen.getByTestId('detail-location')).toHaveTextContent('')
    })

    it('aborts stale product request when branchId changes before response', async () => {
      vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)

      let resolveFirst: (value: ProductDetailDto) => void
      const slowPromise = new Promise<ProductDetailDto>((resolve) => { resolveFirst = resolve })

      const detailSpy = vi.spyOn(catalogApi, 'getProductById')
        .mockReturnValueOnce(slowPromise as Promise<ProductDetailDto>)
        .mockResolvedValueOnce({
          ...mockDetail,
          branchInventory: { branchId: 'branch-2', sellingPrice: 20990000, availableQuantity: 3, onHand: 3 },
        })

      const { navigate } = renderDetail('/product/prod-1?branchId=branch-1')

      await waitFor(() => {
        expect(detailSpy).toHaveBeenCalledWith('prod-1', 'branch-1', expect.any(AbortSignal))
      })
      const firstSignal = detailSpy.mock.calls[0][2] as AbortSignal

      await act(async () => {
        navigate('/product/prod-1?branchId=branch-2')
      })

      expect(firstSignal.aborted).toBe(true)

      resolveFirst!({ ...mockDetail, branchInventory: { branchId: 'branch-1', sellingPrice: 999, availableQuantity: 99, onHand: 99 } })

      await waitFor(() => {
        expect(screen.getByText('Còn 3 sản phẩm tại kho')).toBeInTheDocument()
      })
    })
  })

  describe('Add to Cart Flow', () => {
    it.each([
      ['/product/prod-1', null],
      ['/product/prod-1?branchId=branch-1', null],
      ['/product/prod-1?branchId=branch-1', { branchId: 'branch-1', sellingPrice: 10, availableQuantity: 0, onHand: 0 }],
    ])('disables add when branch inventory is not purchasable', async (entry, inventory) => {
      mockProductDetail({ branchInventory: inventory })
      renderDetail(entry)
      expect(await screen.findByRole('button', { name: 'Thêm vào giỏ' })).toBeDisabled()
    })

    it('opens login for a guest without adding', async () => {
      renderGuestDetail()
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      expect(screen.getByRole('dialog')).toBeInTheDocument()
      expect(addItem).not.toHaveBeenCalled()
    })

    it('changes branch then adds after confirmation', async () => {
      const calls: string[] = []
      changeBranch.mockImplementation(async () => {
        calls.push('change')
        return newBranchCart
      })
      addItem.mockImplementation(async () => {
        calls.push('add')
        return cartWithProduct
      })
      renderBranchMismatchDetail({ cartItems: [existingItem] })
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      fireEvent.click(screen.getByRole('button', { name: 'Đổi kho và xóa giỏ' }))
      await waitFor(() => expect(calls).toEqual(['change', 'add']))
    })

    it('retry after changeBranch→addItem fails calls only addItem, not changeBranch again', async () => {
      changeBranch.mockResolvedValue(newBranchCart)
      addItem
        .mockRejectedValueOnce(new ApiError(500))
        .mockResolvedValueOnce(cartWithProduct)
      renderBranchMismatchDetail({ cartItems: [existingItem] })
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      fireEvent.click(screen.getByRole('button', { name: 'Đổi kho và xóa giỏ' }))
      fireEvent.click(await screen.findByRole('button', { name: 'Thử lại thêm vào giỏ' }))
      await waitFor(() => expect(addItem).toHaveBeenCalledTimes(2))
      expect(changeBranch).toHaveBeenCalledTimes(1)
    })

    it('stops before add when branch change fails', async () => {
      changeBranch.mockRejectedValue(new ApiError(500))
      renderBranchMismatchDetail({ cartItems: [existingItem] })
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      fireEvent.click(screen.getByRole('button', { name: 'Đổi kho và xóa giỏ' }))
      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Không thể đổi kho của giỏ hàng.'
      )
      expect(addItem).not.toHaveBeenCalled()
    })

    it('shows authoritative stock from a 409 response', async () => {
      addItem.mockRejectedValue(
        new ApiError(409, {
          message: 'INSUFFICIENT_STOCK',
          availableQuantity: 2,
        })
      )
      renderSameBranchDetail()
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      expect(await screen.findByRole('alert')).toHaveTextContent('Chỉ còn 2 sản phẩm')
    })

    it('does not replay add after login succeeds', async () => {
      renderGuestDetail()
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      fireEvent.click(screen.getByRole('button', { name: 'Hoàn tất đăng nhập' }))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(addItem).not.toHaveBeenCalled()
    })

    it('cancels branch change without a cart mutation', async () => {
      renderBranchMismatchDetail({ cartItems: [existingItem] })
      fireEvent.click(await screen.findByRole('button', { name: 'Thêm vào giỏ' }))
      fireEvent.click(screen.getByRole('button', { name: 'Giữ giỏ hiện tại' }))
      expect(changeBranch).not.toHaveBeenCalled()
      expect(addItem).not.toHaveBeenCalled()
    })

    it('disables add button when quantity is not an integer or less than 1', async () => {
      renderSameBranchDetail()
      const input = await screen.findByLabelText('Số lượng')
      const addButton = screen.getByRole('button', { name: 'Thêm vào giỏ' })
      expect(addButton).toBeEnabled()

      fireEvent.change(input, { target: { value: '1.5' } })
      expect(addButton).toBeDisabled()

      fireEvent.change(input, { target: { value: '0' } })
      expect(addButton).toBeDisabled()

      fireEvent.change(input, { target: { value: '2' } })
      expect(addButton).toBeEnabled()
    })

    it('focuses reviews section when loaded with #reviews hash', async () => {
      window.location.hash = '#reviews'
      const scrollIntoViewMock = vi.fn()
      window.HTMLElement.prototype.scrollIntoView = scrollIntoViewMock

      renderSameBranchDetail()
      const reviewsSection = await screen.findByLabelText('Đánh giá sản phẩm')
      expect(reviewsSection).toBeInTheDocument()

      await waitFor(() => {
        expect(scrollIntoViewMock).toHaveBeenCalled()
      })
      window.location.hash = ''
    })
  })
})
