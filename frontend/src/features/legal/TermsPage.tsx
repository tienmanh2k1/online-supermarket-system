import './LegalPages.css'

export function TermsPage() {
  return (
    <div className="legal-page">
      <header className="legal-page__header">
        <h1 className="legal-page__title">Điều khoản dịch vụ</h1>
        <div className="legal-page__updated">Cập nhật lần cuối: 09/09/2026</div>
      </header>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">1. Quy định về tài khoản người dùng</h2>
        <p>
          Người dùng cần cung cấp thông tin chính xác và đầy đủ khi đăng ký tài khoản tại AptechMart. Bạn có trách nhiệm bảo mật mật khẩu và thông tin đăng nhập của mình, đồng thời thông báo ngay cho ban quản trị nếu phát hiện bất kỳ dấu hiệu truy cập trái phép nào.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">2. Quy trình đặt hàng và giao nhận hàng</h2>
        <p>
          Khách hàng có thể tìm kiếm sản phẩm, thêm vào giỏ hàng và chọn chi nhánh phục vụ phù hợp. Hệ thống hỗ trợ hai hình thức nhận hàng chính:
        </p>
        <ul>
          <li><strong>Nhận tại kho (Pickup):</strong> Khách hàng trực tiếp đến chi nhánh đã chọn để nhận đơn hàng.</li>
          <li><strong>Giao hàng tận nơi (Delivery):</strong> Đơn hàng sẽ được nhân viên vận chuyển đến địa chỉ do khách hàng cung cấp.</li>
        </ul>
        <p>
          Việc đặt hàng được hoàn tất khi hệ thống tạo mã đơn hàng thành công và hiển thị trong lịch sử mua sắm của bạn.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">3. Giới hạn môi trường sandbox và phạm vi đồ án</h2>
        <div className="legal-page__callout">
          <p>
            Quan trọng: Hệ thống siêu thị điện tử AptechMart được xây dựng trong khuôn khổ đồ án eProject tốt nghiệp. Toàn bộ tính năng thanh toán trực tuyến qua cổng VNPay và ví điện tử MoMo đều chạy ở chế độ giả lập (sandbox) thử nghiệm, không có giá trị giao dịch tiền tệ thực tế và không phát sinh trách nhiệm tài chính thực tế.
          </p>
        </div>
        <p>
          Các cam kết thương mại về đổi trả, hoàn tiền phát sinh ngoài phạm vi chức năng kỹ thuật của đồ án sẽ không được áp dụng trong bản thử nghiệm này.
        </p>
      </section>

      <section className="legal-page__section">
        <h2 className="legal-page__section-title">4. Quyền sở hữu trí tuệ</h2>
        <p>
          Mọi hình ảnh, nhãn hiệu và nội dung sản phẩm trên hệ thống được sử dụng phục vụ mục đích minh họa và học tập trong khuôn khổ đồ án kỹ thuật phần mềm.
        </p>
      </section>
    </div>
  )
}
