import { useEffect, useState, useRef, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useCart } from './CartContext'
import { CartItemRow } from './CartItemRow'
import { BranchChangeConfirmDialog } from './BranchChangeConfirmDialog'
import { AuthModal } from '../auth/AuthModal'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { ApiError } from '../../api/httpClient'
import { formatPrice } from '../products/ProductCard'
import './CartPage.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function toItemError(error: unknown) {
  if (
    error instanceof ApiError &&
    error.status === 409 &&
    error.data?.message === 'INSUFFICIENT_STOCK'
  ) {
    return 'Chỉ còn ' + Number(error.data.availableQuantity) + ' sản phẩm'
  }
  if (error instanceof ApiError && error.status === 404) {
    return 'Sản phẩm trong giỏ không còn tồn tại. Hãy tải lại giỏ hàng.'
  }
  return 'Không thể cập nhật số lượng. Vui lòng thử lại.'
}

function CartLoadingSkeleton() {
  return (
    <section className="cart-page" aria-busy="true" aria-label="Đang tải giỏ hàng">
      <div className="cart-page__skeleton shimmer" />
    </section>
  )
}

function CartLoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <section className="cart-page">
      <div className="cart-page__error" role="alert">
        <p>Không thể tải giỏ hàng.</p>
        <button type="button" className="cart-btn" onClick={onRetry}>
          Thử lại
        </button>
      </div>
    </section>
  )
}

function EmptyCart({ branchId }: { branchId?: string }) {
  const browseUrl = branchId
    ? '/browse?branchId=' + encodeURIComponent(branchId)
    : '/browse'
  return (
    <div className="cart-page__empty">
      <h2>Giỏ hàng của bạn đang trống</h2>
      <p>Hãy khám phá các sản phẩm tươi ngon và tiện lợi tại siêu thị.</p>
      <Link to={browseUrl} className="cart-btn">
        Tiếp tục mua sắm
      </Link>
    </div>
  )
}

export function CartPage() {
  const { isAuthenticated, isLoading: authLoading } = useAuth()
  const {
    status,
    cart,
    mutatingItemIds,
    isChangingBranch,
    isClearing,
    reloadCart,
    updateItemQuantity,
    removeItem,
    changeBranch,
    clearCart,
  } = useCart()

  const [loginOpen, setLoginOpen] = useState(false)
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchLoadError, setBranchLoadError] = useState(false)
  const [branchRetryKey, setBranchRetryKey] = useState(0)
  const [pendingBranchId, setPendingBranchId] = useState<string | null>(null)
  const [branchChangeError, setBranchChangeError] = useState<string | null>(null)
  const [clearConfirmOpen, setClearConfirmOpen] = useState(false)
  const [clearError, setClearError] = useState<string | null>(null)
  const [itemErrors, setItemErrors] = useState<Record<string, string>>({})

  const clearTriggerRef = useRef<HTMLButtonElement>(null)
  const clearCancelRef = useRef<HTMLButtonElement>(null)
  const branchSelectorRef = useRef<HTMLSelectElement>(null)

  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .getBranches(controller.signal)
      .then((data) => {
        setBranches(data)
        setBranchLoadError(false)
      })
      .catch((error: unknown) => {
        if (!isAbortError(error)) setBranchLoadError(true)
      })
    return () => controller.abort()
  }, [branchRetryKey])

  useEffect(() => {
    if (!clearConfirmOpen) return
    clearCancelRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isClearing) setClearConfirmOpen(false)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('keydown', onKeyDown)
      clearTriggerRef.current?.focus()
    }
  }, [clearConfirmOpen, isClearing])

  const onBranchSelection = async (branchId: string) => {
    if (!cart || branchId === cart.branchId) return
    if (cart.items.length === 0) {
      setBranchChangeError(null)
      try {
        await changeBranch(branchId)
      } catch (error) {
        if (!isAbortError(error)) {
          setBranchChangeError('Không thể đổi kho của giỏ hàng.')
        }
      }
      return
    }
    setPendingBranchId(branchId)
  }

  const confirmBranchChange = async () => {
    const branchId = pendingBranchId
    if (!branchId) return
    setBranchChangeError(null)
    try {
      await changeBranch(branchId)
      setPendingBranchId(null)
    } catch (error) {
      if (!isAbortError(error)) {
        setBranchChangeError('Không thể đổi kho của giỏ hàng.')
      }
    }
  }

  const handleUpdate = useCallback(
    async (itemId: string, quantity: number) => {
      setItemErrors((current) => ({ ...current, [itemId]: '' }))
      try {
        return await updateItemQuantity(itemId, quantity)
      } catch (error) {
        if (!isAbortError(error)) {
          setItemErrors((current) => ({
            ...current,
            [itemId]: toItemError(error),
          }))
        }
        throw error
      }
    },
    [updateItemQuantity]
  )

  const handleRemove = useCallback(
    async (itemId: string) => {
      setItemErrors((current) => ({ ...current, [itemId]: '' }))
      try {
        return await removeItem(itemId)
      } catch (error) {
        if (!isAbortError(error)) {
          setItemErrors((current) => ({
            ...current,
            [itemId]: 'Không thể xóa sản phẩm. Vui lòng thử lại.',
          }))
        }
        throw error
      }
    },
    [removeItem]
  )

  if (authLoading) return <CartLoadingSkeleton />

  if (!isAuthenticated) {
    return (
      <section className="cart-page cart-page--auth-required">
        <h1>Đăng nhập để xem giỏ hàng</h1>
        <p>Vui lòng đăng nhập để truy cập giỏ hàng và đồng bộ sản phẩm.</p>
        <button type="button" className="cart-btn" onClick={() => setLoginOpen(true)}>
          Đăng nhập
        </button>
        <AuthModal
          isOpen={loginOpen}
          initialMode="login"
          onClose={() => setLoginOpen(false)}
        />
      </section>
    )
  }

  if (status === 'loading' || status === 'idle') return <CartLoadingSkeleton />
  if (status === 'error') return <CartLoadError onRetry={() => void reloadCart()} />
  if (!cart) return <CartLoadError onRetry={() => void reloadCart()} />

  const isCheckoutDisabled =
    !cart ||
    cart.items.length === 0 ||
    cart.totalItems <= 0 ||
    cart.subtotal <= 0 ||
    isChangingBranch ||
    isClearing ||
    mutatingItemIds.size > 0

  return (
    <section className="cart-page">
      <h1 className="cart-page__heading">Giỏ hàng của bạn</h1>

      <div className="cart-page__branch-bar">
        <label htmlFor="cart-branch">Kho của giỏ hàng</label>
        <select
          ref={branchSelectorRef}
          id="cart-branch"
          value={cart.branchId}
          disabled={isChangingBranch || branchLoadError}
          onChange={(event) => onBranchSelection(event.target.value)}
        >
          {branches.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </select>
        {branchLoadError && (
          <p role="alert" className="cart-page__branch-error">
            Không thể tải danh sách kho.
            <button
              type="button"
              className="cart-btn-text"
              onClick={() => setBranchRetryKey((key) => key + 1)}
            >
              Tải lại danh sách kho
            </button>
          </p>
        )}
        {branchChangeError && (
          <p role="alert" className="cart-page__alert">
            {branchChangeError}
          </p>
        )}
      </div>

      <BranchChangeConfirmDialog
        isOpen={pendingBranchId !== null}
        isBusy={isChangingBranch}
        returnFocusRef={branchSelectorRef}
        onCancel={() => setPendingBranchId(null)}
        onConfirm={confirmBranchChange}
      />

      {cart.items.length === 0 ? (
        <EmptyCart branchId={cart.branchId} />
      ) : (
        <div className="cart-page__layout">
          <div className="cart-page__items-list">
            <div className="cart-page__items-header">
              <p>Giỏ hàng có {cart.items.length} dòng sản phẩm.</p>
              <button
                ref={clearTriggerRef}
                type="button"
                className="cart-btn-clear"
                disabled={isClearing}
                onClick={() => setClearConfirmOpen(true)}
              >
                Xóa toàn bộ giỏ hàng
              </button>
            </div>

            {clearConfirmOpen && (
              <div className="cart-dialog-overlay">
                <section
                  role="dialog"
                  aria-modal="true"
                  aria-labelledby="clear-cart-title"
                  aria-describedby="clear-cart-description"
                  className="cart-dialog"
                >
                  <h2 id="clear-cart-title">Xóa toàn bộ giỏ hàng?</h2>
                  <p id="clear-cart-description">
                    Thao tác này sẽ xóa tất cả sản phẩm trong giỏ.
                  </p>
                  <div className="cart-dialog__actions">
                    <button
                      ref={clearCancelRef}
                      type="button"
                      className="cart-dialog__cancel-btn"
                      disabled={isClearing}
                      onClick={() => setClearConfirmOpen(false)}
                    >
                      Hủy
                    </button>
                    <button
                      type="button"
                      className="cart-dialog__confirm-btn cart-dialog__confirm-btn--danger"
                      disabled={isClearing}
                      onClick={async () => {
                        try {
                          await clearCart()
                          setClearConfirmOpen(false)
                        } catch (error) {
                          if (!isAbortError(error)) {
                            setClearError('Không thể xóa giỏ hàng.')
                          }
                        }
                      }}
                    >
                      Xác nhận xóa
                    </button>
                  </div>
                </section>
              </div>
            )}

            {clearError && (
              <p role="alert" className="cart-page__alert">
                {clearError}
              </p>
            )}

            <div className="cart-items">
              {cart.items.map((item) => (
                <CartItemRow
                  key={item.id}
                  item={item}
                  isMutating={mutatingItemIds.has(item.id)}
                  errorMessage={itemErrors[item.id]}
                  onUpdate={handleUpdate}
                  onRemove={handleRemove}
                />
              ))}
            </div>
          </div>

          <aside className="cart-summary" aria-label="Tóm tắt giỏ hàng">
            <h2 className="cart-summary__title">Tóm tắt đơn hàng</h2>
            <div className="cart-summary__row">
              <span>Tổng số lượng</span>
              <strong>{cart.totalItems}</strong>
            </div>
            <div className="cart-summary__row">
              <span>Tạm tính</span>
              <strong className="cart-summary__price">{formatPrice(cart.subtotal)}</strong>
            </div>
            {isCheckoutDisabled ? (
              <button
                type="button"
                disabled
                className="cart-btn cart-btn--checkout cart-btn--disabled cursor-not-allowed disabled:cursor-not-allowed"
                aria-disabled="true"
                title={
                  cart.items.length === 0 || cart.totalItems <= 0 || cart.subtotal <= 0
                    ? 'Giỏ hàng không có sản phẩm hoặc số tiền không hợp lệ'
                    : 'Giỏ hàng đang được cập nhật, vui lòng đợi'
                }
              >
                Tiến hành thanh toán
              </button>
            ) : (
              <Link
                to="/shopping/checkout"
                className="cart-btn cart-btn--checkout cursor-pointer"
              >
                Tiến hành thanh toán
              </Link>
            )}
          </aside>
        </div>
      )}
    </section>
  )
}
