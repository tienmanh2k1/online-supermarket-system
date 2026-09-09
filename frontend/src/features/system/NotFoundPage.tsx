import { Link } from 'react-router-dom'
import './NotFoundPage.css'

export function NotFoundPage() {
  return (
    <div className="not-found-page">
      <div className="not-found-page__code">404</div>
      <h1 className="not-found-page__title">Không tìm thấy trang</h1>
      <p className="not-found-page__desc">
        Đường dẫn bạn yêu cầu không tồn tại hoặc đã được di chuyển. Vui lòng kiểm tra lại URL hoặc quay về trang mua sắm.
      </p>
      <div className="not-found-page__actions">
        <Link to="/" className="not-found-page__btn-home">
          Về trang chủ
        </Link>
        <Link to="/products" className="not-found-page__btn-products">
          Xem sản phẩm
        </Link>
      </div>
    </div>
  )
}
