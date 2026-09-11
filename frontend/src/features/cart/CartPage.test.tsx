import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom'
import { useCart, type CartContextValue } from './CartContext'
import { useAuth } from '../auth/AuthContext'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { catalogApi } from '../../api/catalogApi'
import type { CartDto, CartItemDto } from '../../api/cartApi'
import { ApiError } from '../../api/httpClient'
import { CartHeaderLink } from './CartHeaderLink'
import { CartPage } from './CartPage'
import { CartItemRow } from './CartItemRow'
import { BranchChangeConfirmDialog } from './BranchChangeConfirmDialog'

vi.mock('./CartContext', () => ({ useCart: vi.fn() }))
vi.mock('../auth/AuthContext', () => ({ useAuth: vi.fn() }))
vi.mock('../../api/branchApi', () => ({
  branchApi: { getBranches: vi.fn() },
}))

const useCartMock = vi.mocked(useCart)
const useAuthMock = vi.mocked(useAuth)

const cartResponse: CartDto = {
  id: 'cart-1',
  userId: 'user-1',
  branchId: 'branch-1',
  items: [],
  totalItems: 0,
  subtotal: 0,
}

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

const branches: BranchDto[] = [
  {
    id: 'branch-1',
    name: 'Kho Quận 1',
    address: '123 Nguyễn Huệ',
    phone: null,
    latitude: null,
    longitude: null,
    isActive: true,
  },
  {
    id: 'branch-2',
    name: 'Kho Quận 3',
    address: '456 Võ Văn Tần',
    phone: null,
    latitude: null,
    longitude: null,
    isActive: true,
  },
]

const reloadCart = vi.fn()
const addItem = vi.fn()
const updateItemQuantity = vi.fn()
const removeItem = vi.fn()
const changeBranch = vi.fn()
const clearCart = vi.fn()

const readyCartContext: CartContextValue = {
  status: 'ready',
  cart: cartResponse,
  errorMessage: null,
  mutatingItemIds: new Set(),
  isAddingItem: false,
  isChangingBranch: false,
  isClearing: false,
  reloadCart,
  addItem,
  updateItemQuantity,
  removeItem,
  changeBranch,
  clearCart,
}

const cartItem: CartItemDto = {
  id: 'item-1',
  productId: 'prod-1',
  productName: 'Điện thoại A',
  sku: 'SKU-A',
  unitPrice: 1000000,
  quantity: 2,
  lineTotal: 2000000,
  availableQuantity: 5,
}

const readyCart: CartDto = {
  ...cartResponse,
  items: [cartItem],
  totalItems: 2,
  subtotal: 2000000,
}

function LocationDisplay() {
  return <output data-testid="location">{useLocation().pathname}</output>
}

function renderCartRoute(entry: string, auth: ReturnType<typeof useAuth>) {
  useAuthMock.mockReturnValue(auth)
  useCartMock.mockReturnValue(readyCartContext)
  vi.mocked(branchApi.getBranches).mockResolvedValue(branches)
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route path="/shopping/cart" element={<CartPage />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  )
}

function renderReadyCart(
  overrides: Partial<CartContextValue> = {},
  branchRequest: Promise<BranchDto[]> = Promise.resolve(branches)
) {
  vi.mocked(branchApi.getBranches).mockReturnValue(branchRequest)
  useAuthMock.mockReturnValue(authenticatedAuth)
  useCartMock.mockReturnValue({
    ...readyCartContext,
    cart: readyCart,
    ...overrides,
  })
  return render(
    <MemoryRouter>
      <CartPage />
    </MemoryRouter>
  )
}

describe('CartPage & Header Badge', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(branchApi.getBranches).mockResolvedValue(branches)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows total quantity rather than row count in the header', () => {
    useCartMock.mockReturnValue({
      ...readyCartContext,
      cart: { ...cartResponse, items: [], totalItems: 5 },
    })
    render(
      <MemoryRouter>
        <CartHeaderLink />
      </MemoryRouter>
    )
    expect(screen.getByRole('link', { name: /Giỏ hàng/ })).toHaveAttribute(
      'href',
      '/shopping/cart'
    )
    expect(screen.getByLabelText('5 sản phẩm trong giỏ')).toHaveTextContent('5')
  })

  it('removes the badge immediately when cart context resets on logout', () => {
    useCartMock.mockReturnValue({
      ...readyCartContext,
      cart: { ...cartResponse, totalItems: 3 },
    })
    const view = render(
      <MemoryRouter>
        <CartHeaderLink />
      </MemoryRouter>
    )
    expect(screen.getByLabelText('3 sản phẩm trong giỏ')).toBeInTheDocument()

    useCartMock.mockReturnValue({ ...readyCartContext, status: 'idle', cart: null })
    view.rerender(
      <MemoryRouter>
        <CartHeaderLink />
      </MemoryRouter>
    )
    expect(screen.queryByLabelText(/sản phẩm trong giỏ/)).not.toBeInTheDocument()
  })

  it('keeps a guest on the cart route and opens login', () => {
    renderCartRoute('/shopping/cart', guestAuth)
    expect(
      screen.getByRole('heading', { name: 'Đăng nhập để xem giỏ hàng' })
    ).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/shopping/cart')
    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('renders server totals and a product placeholder without product fetches', () => {
    const productFetch = vi.spyOn(catalogApi, 'getProductById')
    renderReadyCart()
    expect(screen.getByText('Điện thoại A')).toBeInTheDocument()
    expect(screen.getByText('SKU-A')).toBeInTheDocument()
    expect(screen.getAllByText(/2\.000\.000/).length).toBeGreaterThan(0)
    expect(screen.getByLabelText('Ảnh thay thế cho Điện thoại A')).toBeInTheDocument()
    expect(productFetch).not.toHaveBeenCalled()
  })

  it('submits increment, decrement and direct quantity updates', async () => {
    renderReadyCart()
    fireEvent.click(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    )
    expect(updateItemQuantity).toHaveBeenCalledWith('item-1', 3)
    fireEvent.change(screen.getByLabelText('Số lượng Điện thoại A'), {
      target: { value: '4' },
    })
    fireEvent.blur(screen.getByLabelText('Số lượng Điện thoại A'))
    expect(updateItemQuantity).toHaveBeenCalledWith('item-1', 4)
  })

  it('disables +/- buttons while item is mutating', () => {
    renderReadyCart({ mutatingItemIds: new Set(['item-1']) })
    expect(
      screen.getByRole('button', { name: 'Giảm số lượng Điện thoại A' })
    ).toBeDisabled()
    expect(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    ).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Xóa Điện thoại A' })).toBeDisabled()
  })

  it('re-enables buttons after mutation succeeds', () => {
    const view = render(
      <CartItemRow
        item={cartItem}
        isMutating={true}
        onUpdate={updateItemQuantity}
        onRemove={removeItem}
      />
    )
    expect(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    ).toBeDisabled()
    view.rerender(
      <CartItemRow
        item={{ ...cartItem, quantity: 3 }}
        isMutating={false}
        onUpdate={updateItemQuantity}
        onRemove={removeItem}
      />
    )
    expect(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    ).toBeEnabled()
  })

  it('re-enables buttons after mutation fails', () => {
    const view = render(
      <CartItemRow
        item={cartItem}
        isMutating={true}
        onUpdate={updateItemQuantity}
        onRemove={removeItem}
      />
    )
    view.rerender(
      <CartItemRow
        item={cartItem}
        isMutating={false}
        errorMessage="Không thể cập nhật số lượng"
        onUpdate={updateItemQuantity}
        onRemove={removeItem}
      />
    )
    expect(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    ).toBeEnabled()
    expect(screen.getByRole('alert')).toHaveTextContent('Không thể cập nhật số lượng')
  })

  it('shows server stock on a 409 and keeps the displayed item', async () => {
    updateItemQuantity.mockRejectedValue(
      new ApiError(409, {
        message: 'INSUFFICIENT_STOCK',
        availableQuantity: 1,
      })
    )
    renderReadyCart()
    fireEvent.click(
      screen.getByRole('button', { name: 'Tăng số lượng Điện thoại A' })
    )
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Chỉ còn 1 sản phẩm'
    )
    expect(screen.getByText('Điện thoại A')).toBeInTheDocument()
  })

  it('keeps cart items visible when branches fail and retries branches', async () => {
    renderReadyCart({}, Promise.reject(new ApiError(500)))
    expect(await screen.findByText('Điện thoại A')).toBeInTheDocument()
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể tải danh sách kho.'
    )
    vi.mocked(branchApi.getBranches).mockResolvedValue(branches)
    fireEvent.click(screen.getByRole('button', { name: 'Tải lại danh sách kho' }))
    await waitFor(() => expect(branchApi.getBranches).toHaveBeenCalledTimes(2))
    expect(screen.getByLabelText('Kho của giỏ hàng')).toBeEnabled()
  })

  it('requires confirmation before clearing the cart', async () => {
    renderReadyCart()
    fireEvent.click(screen.getByRole('button', { name: 'Xóa toàn bộ giỏ hàng' }))
    expect(
      screen.getByRole('dialog', { name: 'Xóa toàn bộ giỏ hàng?' })
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận xóa' }))
    expect(clearCart).toHaveBeenCalledTimes(1)
  })

  it('changes branch immediately when the cart is empty', async () => {
    renderReadyCart({
      cart: { ...readyCart, items: [], totalItems: 0, subtotal: 0 },
    })
    await screen.findByRole('option', { name: 'Kho Quận 1' })
    fireEvent.change(screen.getByLabelText('Kho của giỏ hàng'), {
      target: { value: 'branch-2' },
    })
    expect(changeBranch).toHaveBeenCalledWith('branch-2')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('displays error when changing branch on an empty cart fails', async () => {
    changeBranch.mockRejectedValue(new ApiError(500))
    renderReadyCart({
      cart: { ...readyCart, items: [], totalItems: 0, subtotal: 0 },
    })
    await screen.findByRole('option', { name: 'Kho Quận 1' })
    fireEvent.change(screen.getByLabelText('Kho của giỏ hàng'), {
      target: { value: 'branch-2' },
    })
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể đổi kho của giỏ hàng.'
    )
  })

  it('requires confirmation to change a non-empty cart branch', async () => {
    renderReadyCart()
    await screen.findByRole('option', { name: 'Kho Quận 1' })
    fireEvent.change(screen.getByLabelText('Kho của giỏ hàng'), {
      target: { value: 'branch-2' },
    })
    expect(
      screen.getByRole('dialog', {
        name: 'Đổi kho và xóa giỏ hiện tại?',
      })
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Giữ giỏ hiện tại' }))
    expect(changeBranch).not.toHaveBeenCalled()
  })

  it('links ready carts to checkout', async () => {
    renderReadyCart()
    const checkoutLink = await screen.findByRole('link', { name: 'Tiến hành thanh toán' })
    expect(checkoutLink).toHaveAttribute('href', '/shopping/checkout')
    expect(checkoutLink).toHaveClass('cursor-pointer')
  })

  it('disables checkout button when cart is mutating or has invalid total', () => {
    renderReadyCart({ mutatingItemIds: new Set(['item-1']) })
    const checkoutBtn = screen.getByRole('button', { name: 'Tiến hành thanh toán' })
    expect(checkoutBtn).toBeDisabled()
    expect(checkoutBtn).toHaveClass('cart-btn--disabled')
    expect(checkoutBtn).toHaveClass('cursor-not-allowed')
  })

  it('exposes accessible cart controls and progress', () => {
    renderReadyCart({ mutatingItemIds: new Set(['item-1']) })
    expect(
      screen.getByRole('heading', { level: 1, name: 'Giỏ hàng của bạn' })
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Số lượng Điện thoại A')).toBeDisabled()
    expect(screen.getByRole('status')).toHaveTextContent(
      'Đang cập nhật Điện thoại A'
    )
  })

  it('exposes a labelled modal branch warning', () => {
    render(
      <BranchChangeConfirmDialog
        isOpen={true}
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
      />
    )
    const dialog = screen.getByRole('dialog', {
      name: 'Đổi kho và xóa giỏ hiện tại?',
    })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAccessibleDescription(/toàn bộ sản phẩm/)
  })
})
