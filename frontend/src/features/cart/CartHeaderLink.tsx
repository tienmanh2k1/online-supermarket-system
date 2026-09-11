import { Link } from 'react-router-dom'
import { useCart } from './CartContext'
import './CartHeaderLink.css'

export function CartHeaderLink() {
  const { status, cart } = useCart()
  const count = status === 'ready' ? cart?.totalItems ?? 0 : 0

  return (
    <Link
      to="/shopping/cart"
      className="cart-header-link relative inline-flex items-center gap-2"
      aria-label={`Giỏ hàng${count > 0 ? ` (${count} sản phẩm)` : ''}`}
    >
      <span className="cart-header-link__icon-container relative inline-flex items-center justify-center">
        <span aria-hidden="true" className="cart-header-link__icon">
          🛒
        </span>
        {count > 0 && (
          <span
            className="cart-header-link__badge absolute top-[-6px] right-[-10px] bg-rose-500 text-white text-[11px] font-bold h-5 w-5 rounded-full flex items-center justify-center shadow-sm pointer-events-none"
            aria-label={count + ' sản phẩm trong giỏ'}
          >
            {count}
          </span>
        )}
      </span>
      <span>Giỏ hàng</span>
    </Link>
  )
}
