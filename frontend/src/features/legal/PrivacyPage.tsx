import './LegalPages.css'

export function PrivacyPage() {
  return (
    <div className="legal-page">
      <header className="legal-page__header">
        <h1 className="legal-page__title">Chính sách bảo mật</h1>
        <div className="legal-page__updated">Cập nhật lần cuối: 09/09/2026</div>
      </header>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">1. Thu thập thông tin tài khoản</h2>
        <p>
          Khi bạn đăng ký tài khoản tại AptechMart, chúng tôi thu thập các thông tin cơ bản bao gồm họ và tên, địa chỉ email, số điện thoại và mật khẩu đăng nhập đã được mã hóa an toàn. Thông tin tài khoản được sử dụng để xác thực danh tính, đăng nhập và bảo vệ trải nghiệm của bạn trên hệ thống.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">2. Thông tin địa chỉ và giao nhận</h2>
        <p>
          Để thực hiện việc giao hàng, chúng tôi thu thập danh sách địa chỉ nhận hàng của bạn bao gồm: tên người nhận, số điện thoại liên hệ, địa chỉ chi tiết và ghi chú giao hàng. Dữ liệu địa chỉ này chỉ được dùng trong phạm vi điều phối và xử lý đơn hàng tại các chi nhánh của chúng tôi.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">3. Quản lý đơn hàng và hoạt động mua sắm</h2>
        <p>
          Hệ thống lưu trữ lịch sử các đơn hàng bạn đã tạo, bao gồm các mặt hàng đã chọn, số lượng, chi nhánh xử lý, hình thức giao nhận và trạng thái đơn hàng. Dữ liệu đơn hàng giúp khách hàng dễ dàng theo dõi tiến độ và đánh giá chất lượng sản phẩm sau khi nhận hàng.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">4. Thanh toán trực tuyến và giới hạn sandbox</h2>
        <p>
          Hệ thống AptechMart tích hợp các phương thức thanh toán điện tử như VNPay và MoMo. Tất cả các cổng thanh toán này đều hoạt động trong môi trường thử nghiệm (sandbox) phục vụ mục đích kiểm thử đồ án tốt nghiệp.
        </p>
        <div className="legal-page__callout">
          <p>
            Lưu ý: Môi trường sandbox chỉ giả lập giao dịch kiểm thử và không thu tiền thật. Hệ thống không lưu trữ bất kỳ thông tin số thẻ tín dụng, tài khoản ngân hàng hoặc mã bảo mật thẻ của người dùng.
          </p>
        </div>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">5. Bảo vệ dữ liệu cá nhân</h2>
        <p>
          Chúng tôi áp dụng các tiêu chuẩn mã hóa mật khẩu và token truy cập chuẩn công nghiệp (JWT) để bảo vệ thông tin của bạn khỏi sự truy cập trái phép. Mọi quyền truy cập dữ liệu quản trị đều được phân quyền nghiêm ngặt theo vai trò hệ thống.
        </p>
      </section>
    </div>
  )
}
