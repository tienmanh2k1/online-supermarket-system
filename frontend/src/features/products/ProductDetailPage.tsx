import { useEffect, useState, useCallback, useRef } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { catalogApi, type ProductDetailDto } from '../../api/catalogApi'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { ApiError } from '../../api/httpClient'
import { useAuth } from '../auth/AuthContext'
import { useCart } from '../cart/CartContext'
import { useCompare } from '../compare/CompareContext'
import { AuthModal } from '../auth/AuthModal'
import { BranchChangeConfirmDialog } from '../cart/BranchChangeConfirmDialog'
import { formatPrice } from './ProductCard'
import './ProductDetailPage.css'

type ProductState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: ProductDetailDto }
  | { kind: 'not-found' }
  | { kind: 'error'; message: string }

type BranchState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: BranchDto[] }
  | { kind: 'error' }

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function toCartActionMessage(error: unknown, fallback: string) {
  if (
    error instanceof ApiError &&
    error.status === 409 &&
    error.data?.message === 'INSUFFICIENT_STOCK'
  ) {
    return 'Chỉ còn ' + Number(error.data.availableQuantity) + ' sản phẩm'
  }
  if (error instanceof ApiError && error.status === 404) {
    return 'Sản phẩm hoặc kho không còn khả dụng.'
  }
  return fallback
}

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const branchId = searchParams.get('branchId') || undefined

  const { isAuthenticated } = useAuth()
  const {
    status: cartStatus,
    cart,
    isAddingItem,
    isChangingBranch,
    addItem,
    changeBranch,
  } = useCart()
  const {
    isInCompare,
    addToCompare,
    removeFromCompare,
    openModal,
    getDifferentCategoryWarning,
    hasProduct,
  } = useCompare()

  const [productState, setProductState] = useState<ProductState>({ kind: 'loading' })
  const [branchState, setBranchState] = useState<BranchState>({ kind: 'loading' })
  const [productRetryKey, setProductRetryKey] = useState(0)
  const [branchRetryKey, setBranchRetryKey] = useState(0)
  const [imageFailed, setImageFailed] = useState(false)

  const [quantity, setQuantity] = useState(1)
  const [loginOpen, setLoginOpen] = useState(false)
  const [confirmBranchChange, setConfirmBranchChange] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)
  const [retryAdd, setRetryAdd] = useState<{ productId: string; quantity: number } | null>(null)
  const addButtonRef = useRef<HTMLButtonElement>(null)

  // Reset quantity and feedback when product or branch changes
  useEffect(() => {
    setQuantity(1)
    setAddError(null)
    setRetryAdd(null)
  }, [id, branchId])

  // Product fetch
  const fetchProduct = useCallback(() => {
    if (!id) {
      setProductState({ kind: 'not-found' })
      return
    }
    const controller = new AbortController()
    setProductState({ kind: 'loading' })
    setImageFailed(false)
    catalogApi
      .getProductById(id, branchId, controller.signal)
      .then((data) => {
        setProductState({ kind: 'ready', data })
        setImageFailed(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        if (error instanceof ApiError && error.status === 404) {
          setProductState({ kind: 'not-found' })
          return
        }
        setProductState({ kind: 'error', message: 'Không thể tải chi tiết sản phẩm.' })
      })
    return () => controller.abort()
  }, [id, branchId, productRetryKey])

  // Branch fetch
  const fetchBranches = useCallback(() => {
    const controller = new AbortController()
    setBranchState({ kind: 'loading' })
    branchApi
      .getBranches(controller.signal)
      .then((data) => setBranchState({ kind: 'ready', data }))
      .catch(() => {
        if (controller.signal.aborted) return
        setBranchState({ kind: 'error' })
      })
    return () => controller.abort()
  }, [branchRetryKey])

  useEffect(() => {
    return fetchProduct()
  }, [fetchProduct])

  useEffect(() => {
    return fetchBranches()
  }, [fetchBranches])

  // Normalize invalid branchId
  useEffect(() => {
    if (!branchId || branchState.kind !== 'ready') return
    if (branchState.data.some((branch) => branch.id === branchId)) return
    const next = new URLSearchParams(searchParams)
    next.delete('branchId')
    setSearchParams(next, { replace: true })
  }, [branchId, branchState, searchParams, setSearchParams])

  function handleBranchChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const next = new URLSearchParams(searchParams)
    const nextBranchId = e.target.value
    if (nextBranchId) next.set('branchId', nextBranchId)
    else next.delete('branchId')
    setSearchParams(next)
  }

  async function performAdd(productId: string, requestedQuantity: number) {
    try {
      await addItem(productId, requestedQuantity)
      setAddError(null)
      setRetryAdd(null)
    } catch (error) {
      if (isAbortError(error)) return
      setAddError(toCartActionMessage(error, 'Không thể thêm sản phẩm vào giỏ.'))
      setRetryAdd({ productId, quantity: requestedQuantity })
    }
  }

  async function continueAddAfterBranchCheck() {
    if (!branchId || !id || !cart) return
    try {
      if (cart.branchId !== branchId) {
        await changeBranch(branchId)
      }
    } catch (error) {
      if (isAbortError(error)) return
      setAddError(toCartActionMessage(error, 'Không thể đổi kho của giỏ hàng.'))
      setRetryAdd(null)
      return
    }
    await performAdd(id, quantity)
  }

  function handleAddClick() {
    if (!isAuthenticated) {
      setLoginOpen(true)
      return
    }
    if (!cart || !branchId || !id) return
    if (cart.branchId !== branchId && cart.items.length > 0) {
      setConfirmBranchChange(true)
      return
    }
    void continueAddAfterBranchCheck()
  }

  function renderLoading() {
    return (
      <div className="product-detail-page">
        <div
          className="product-detail__loading"
          aria-label="Đang tải chi tiết sản phẩm"
          aria-busy="true"
        >
          <div className="product-detail__skeleton-image shimmer" />
          <div className="product-detail__skeleton-body">
            <div className="skeleton-title shimmer" />
            <div className="skeleton-meta shimmer" />
            <div className="skeleton-desc shimmer" />
          </div>
        </div>
      </div>
    )
  }

  function renderNotFound() {
    return (
      <div className="product-detail-page">
        <div className="product-detail__not-found">
          <h1>Không tìm thấy sản phẩm</h1>
          <p>Sản phẩm bạn đang tìm kiếm không tồn tại hoặc đã bị xóa.</p>
          <Link to="/browse" className="product-detail__back-link">
            Quay lại danh sách sản phẩm
          </Link>
        </div>
      </div>
    )
  }

  function renderError(message: string) {
    return (
      <div className="product-detail-page">
        <div className="product-detail__error" role="alert">
          <p>{message}</p>
          <button type="button" onClick={() => setProductRetryKey((k) => k + 1)}>
            Thử lại
          </button>
        </div>
      </div>
    )
  }

  function renderReady(product: ProductDetailDto) {
    const { availableQuantity } = product.branchInventory ?? {}
    const isAvailable =
      availableQuantity !== undefined && availableQuantity !== null && availableQuantity > 0
    const isOutOfStock =
      availableQuantity !== undefined && availableQuantity !== null && availableQuantity === 0
    const isUnavailable = product.branchInventory === null && Boolean(branchId)
    const isNoBranch = !branchId

    let price = product.basePrice
    let message = ''
    let priceClass = ''

    if (isNoBranch) {
      message = 'Chọn kho để xem giá và tồn kho'
      priceClass = 'product-detail__availability--neutral'
    } else if (isUnavailable) {
      message = 'Sản phẩm không có tại kho này'
      priceClass = 'product-detail__availability--unavailable'
    } else if (isOutOfStock) {
      message = 'Tạm hết hàng tại kho này'
      priceClass = 'product-detail__availability--unavailable'
      price = product.branchInventory!.sellingPrice
    } else if (isAvailable) {
      message = `Còn ${availableQuantity} sản phẩm tại kho`
      priceClass = 'product-detail__availability--available'
      price = product.branchInventory!.sellingPrice
    }

    const effectiveImageUrl =
      product.imageUrl && !imageFailed ? product.imageUrl : null

    const available = product.branchInventory?.availableQuantity ?? 0
    const cartReadyForCurrentVisitor = !isAuthenticated || cartStatus === 'ready'
    const canAdd = Boolean(
      branchId &&
        product.branchInventory &&
        available > 0 &&
        Number.isInteger(quantity) &&
        quantity > 0 &&
        quantity <= available &&
        cartReadyForCurrentVisitor &&
        !isAddingItem &&
        !isChangingBranch
    )

    return (
      <div className="product-detail-page">
        <nav className="product-detail__breadcrumb" aria-label="Breadcrumb">
          <Link to="/browse">Danh sách sản phẩm</Link>
          <span aria-hidden="true"> / </span>
          <span>{product.categoryName}</span>
        </nav>

        <div className="product-detail__layout">
          <div className="product-detail__image-frame">
            {effectiveImageUrl ? (
              <img
                src={effectiveImageUrl}
                alt={product.name}
                className="product-detail__image"
                onError={() => setImageFailed(true)}
              />
            ) : (
              <div className="product-detail__image-placeholder">
                <svg
                  className="product-detail__placeholder-icon"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M2.25 15.75l5.159-5.159a2.25 2.25 0 013.182 0l5.159 5.159m-1.5-1.5l1.409-1.409a2.25 2.25 0 013.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 001.5-1.5V6a1.5 1.5 0 00-1.5-1.5H3.75A1.5 1.5 0 002.25 6v12a1.5 1.5 0 001.5 1.5zm10.5-11.25h.008v.008h-.008V8.25zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z"
                  />
                </svg>
                <span>Hình ảnh sản phẩm</span>
              </div>
            )}
          </div>

          <div className="product-detail__content">
            <h1 className="product-detail__title">{product.name}</h1>

            <div className="product-detail__meta">
              <span className="product-detail__brand">{product.brandName}</span>
              <span className="product-detail__sku">SKU: {product.sku}</span>
              <span className="product-detail__category">{product.categoryName}</span>
              <span className="product-detail__unit">Đơn vị: {product.unit}</span>
            </div>

            <div className="product-detail__description">
              <p>{product.description || 'Không có mô tả.'}</p>
            </div>

            <div className="product-detail__purchase-panel">
              <div className={`product-detail__price ${priceClass}`}>
                <span className="product-detail__price-label">Giá:</span>
                <span className="product-detail__price-value">{formatPrice(price)}</span>
              </div>

              <div className="product-detail__availability">
                {message && (
                  <span className={`product-detail__availability-msg ${priceClass}`}>
                    {message}
                  </span>
                )}
              </div>

              <div className="product-detail__branch-selector">
                {branchState.kind === 'error' && (
                  <div className="product-detail__branch-error" role="alert">
                    <span>Không thể tải danh sách kho.</span>
                    <button
                      type="button"
                      onClick={() => setBranchRetryKey((k) => k + 1)}
                    >
                      Tải lại danh sách kho
                    </button>
                  </div>
                )}
                <label htmlFor="product-detail-branch">Kho hàng</label>
                <select
                  id="product-detail-branch"
                  value={branchId ?? ''}
                  onChange={handleBranchChange}
                  disabled={branchState.kind !== 'ready' || productState.kind === 'loading'}
                >
                  <option value="">Chọn kho</option>
                  {branchState.kind === 'ready' &&
                    branchState.data.map((branch) => (
                      <option key={branch.id} value={branch.id}>
                        {branch.name}
                      </option>
                    ))}
                </select>
              </div>

              <div className="product-detail__add-to-cart">
                <div className="product-detail__quantity-box">
                  <label htmlFor="product-quantity">Số lượng</label>
                  <input
                    id="product-quantity"
                    type="number"
                    step={1}
                    min={1}
                    max={available || 1}
                    value={quantity}
                    disabled={!product.branchInventory || available === 0}
                    onChange={(event) => setQuantity(Number(event.target.value))}
                  />
                </div>
                <button
                  ref={addButtonRef}
                  type="button"
                  className="product-detail__add-btn"
                  disabled={!canAdd}
                  onClick={handleAddClick}
                >
                  {isAddingItem || isChangingBranch ? 'Đang xử lý…' : 'Thêm vào giỏ'}
                </button>
              </div>

              {addError && (
                <p role="alert" className="product-detail__alert">
                  {addError}
                </p>
              )}

              {retryAdd && (
                <button
                  type="button"
                  className="product-detail__retry-add-btn"
                  onClick={() => void performAdd(retryAdd.productId, retryAdd.quantity)}
                >
                  Thử lại thêm vào giỏ
                </button>
              )}

              {/* Compare Button */}
              <div className="product-detail__compare-section">
                {id && isInCompare(id) ? (
                  <>
                    <span className="product-detail__compare-added">✓ Đã thêm vào so sánh</span>
                    <button
                      type="button"
                      className="product-detail__compare-remove-btn"
                      onClick={() => {
                        if (id) removeFromCompare(id)
                      }}
                    >
                      Hủy so sánh
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    className="product-detail__compare-btn"
                    onClick={() => {
                      const warning = getDifferentCategoryWarning({
                        id: id!,
                        categoryId: product.categoryId,
                        categoryName: product.categoryName,
                        categorySlug: product.categorySlug,
                      })
                      if (warning) {
                        alert(warning)
                        return
                      }
                      const added = addToCompare({
                        id: id!,
                        categoryId: product.categoryId,
                        categoryName: product.categoryName,
                        categorySlug: product.categorySlug,
                      })
                      if (added || hasProduct) {
                        openModal()
                      }
                    }}
                  >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M7.5 21L3 16.5m0 0L7.5 12M3 16.5h13.5m0-13.5L21 7.5m0 0L16.5 12M21 7.5H7.5" />
                    </svg>
                    So sánh sản phẩm
                  </button>
                )}
              </div>

              <AuthModal
                isOpen={loginOpen}
                initialMode="login"
                onClose={() => setLoginOpen(false)}
              />

              <BranchChangeConfirmDialog
                isOpen={confirmBranchChange}
                isBusy={isChangingBranch}
                returnFocusRef={addButtonRef}
                onCancel={() => setConfirmBranchChange(false)}
                onConfirm={async () => {
                  setConfirmBranchChange(false)
                  await continueAddAfterBranchCheck()
                }}
              />
            </div>
          </div>
        </div>
      </div>
    )
  }

  if (productState.kind === 'loading') return renderLoading()
  if (productState.kind === 'not-found') return renderNotFound()
  if (productState.kind === 'error') return renderError(productState.message)
  return renderReady(productState.data)
}
