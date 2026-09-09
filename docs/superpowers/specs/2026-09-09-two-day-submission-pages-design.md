# Two-Day Submission Pages Design

## Mục tiêu

Hoàn thiện các khoảng trống giao diện có rủi ro cao nhất trước hạn nộp ngày 11/09/2026, trong tối đa hai ngày làm việc tập trung. Bản sửa phải khép kín luồng khôi phục mật khẩu, tránh màn hình trắng khi URL sai, bổ sung màn hình tổng quan cho quản trị viên và cung cấp báo cáo doanh số tối thiểu phù hợp trạng thái `IN_PROGRESS` của FR-207.

## Phạm vi bắt buộc

### 1. Khôi phục mật khẩu

- Thêm hành động “Quên mật khẩu?” trong form đăng nhập.
- Form yêu cầu khôi phục nhận email và luôn hiển thị thông báo trung tính sau khi API thành công để không làm lộ tài khoản tồn tại.
- Thêm route `/reset-password?token=...` với form mật khẩu mới và xác nhận mật khẩu.
- Xử lý rõ token thiếu, token hết hạn/không hợp lệ, đang gửi, thành công và lỗi mạng.
- Tái sử dụng API backend hiện có:
  - `POST /api/auth/password-reset`
  - `POST /api/auth/password-reset/confirm`
- Bản nộp chạy API với `ASPNETCORE_ENVIRONMENT=Development` như `compose.yaml`, dùng `DevEmailSender`; thêm endpoint đọc email reset mới nhất chỉ được map trong Development và yêu cầu policy `AdminOnly` để lấy link demo. E2E dùng bearer token Admin qua API request context riêng, tách khỏi browser context Customer. Không tuyên bố hệ thống có nhà cung cấp email production.
- Kịch bản nghiệm thu bắt buộc: yêu cầu reset → lấy link từ dev mailbox → mở link trên frontend → đổi mật khẩu → đăng nhập bằng mật khẩu mới.

### 2. Trang không tìm thấy

- Thêm wildcard route `*` render `NotFoundPage` bên trong `AppShell`.
- Trang có thông báo ngắn, nút về trang chủ và nút xem sản phẩm.
- Không tự động chuyển hướng vì người dùng cần biết URL không hợp lệ.

### 3. Admin Dashboard tối thiểu

- Thêm `/admin/dashboard` và đổi index `/admin` sang dashboard.
- Thêm mục “Tổng quan” ở đầu sidebar quản trị.
- Dashboard hiển thị bốn vùng: tổng số đơn, đơn cần xử lý, tổng doanh thu đơn hoàn tất (toàn thời gian) và hàng tồn thấp; kèm danh sách đơn gần đây.
- Chỉ dùng dữ liệu/API hiện có hoặc endpoint tổng hợp nhỏ nếu việc ghép API hiện tại gây tải hoặc logic trùng lặp. Không thêm widget tùy biến.
- Có loading, error với nút thử lại và empty state.
- Tất cả route/API tiếp tục được bảo vệ bởi quyền Admin.

### 4. Sales Report tối thiểu

- Thêm `/admin/reports/sales` và mục “Báo cáo doanh số” trong sidebar.
- Hỗ trợ khoảng thời gian với preset 7 ngày, 30 ngày và tháng hiện tại; mặc định 30 ngày.
- Hiển thị tổng doanh thu, số đơn hoàn tất, giá trị đơn trung bình và bảng doanh thu theo ngày.
- Report nhóm đơn theo ngày tạo `CreatedAtUtc`, không phải ngày đơn chuyển sang Completed. Preset frontend được tính theo ngày UTC và giao diện phải ghi rõ quy ước này.
- Nếu endpoint FR-207 chưa tồn tại, thêm một endpoint đọc dữ liệu tối thiểu `GET /api/admin/reports/sales?from=YYYY-MM-DD&to=YYYY-MM-DD`; chỉ tính đơn ở trạng thái hoàn tất theo quy ước domain hiện tại.
- CSV export, biểu đồ nâng cao và phân tích theo thương hiệu nằm ngoài scope hai ngày.

### 5. Trang pháp lý tối thiểu

- Thêm `/privacy` và `/terms` dưới dạng nội dung tĩnh tiếng Việt.
- Footer phải có liên kết tới hai trang này.
- Nội dung mô tả đúng dữ liệu hệ thống đang thu thập và không đưa ra cam kết pháp lý/chính sách hoàn tiền chưa được dự án xác nhận.

## Nếu còn thời gian

- Tạo một trang `/support` gộp Contact và FAQ, chứa hotline/địa chỉ chi nhánh lấy từ dữ liệu hiện có và các câu hỏi phổ biến về đơn hàng, giao hàng, thanh toán.
- Hạng mục này chỉ bắt đầu sau khi toàn bộ phạm vi bắt buộc build và test thành công.

## Ngoài phạm vi

- Customer Dashboard riêng.
- Wishlist/Saved Items (`SD-001` vẫn `DEFERRED`).
- Admin settings, billing, danger zone và audit log tập trung.
- Tùy biến hoặc sắp xếp widget dashboard.
- Báo cáo nâng cao, CSV/PDF export, so sánh kỳ và drill-down theo thương hiệu/danh mục.

## Kiến trúc

Các trang mới tiếp tục dùng React Router trong `frontend/src/App.tsx`, đặt theo feature folder hiện có và gọi API qua module trong `frontend/src/api`. Khôi phục mật khẩu là một feature độc lập vì có cả modal entry point và route từ email. Dashboard và Sales Report dùng chung các DTO báo cáo nhỏ nhưng giữ component trang riêng để có thể hoàn thiện FR-207 sau deadline mà không làm dashboard phình lớn.

Nếu cần endpoint báo cáo mới, backend đặt trong nhóm `/api/admin`, kiểm tra role theo cùng convention với các admin endpoint hiện tại và thực hiện truy vấn chỉ đọc. Không thay đổi schema database trong phạm vi này.

## Trạng thái và lỗi

- Mọi yêu cầu mạng đều có trạng thái loading, thành công, empty và lỗi có thể retry.
- Form khôi phục mật khẩu không phân biệt email có/không tồn tại.
- Form đặt lại mật khẩu kiểm tra mật khẩu khớp nhau ở client nhưng backend vẫn là nguồn xác thực cuối cùng.
- Dashboard/report không hiển thị số 0 giả trong lúc tải hoặc khi API lỗi.
- Khoảng ngày không hợp lệ bị chặn trước khi gửi request; `from` không được sau `to`.
- Report cho phép tối đa 366 ngày tính cả hai đầu; `to=9999-12-31` bị từ chối để tránh overflow khi tạo cận trên loại trừ.
- Lỗi validation report dùng RFC 7807 `ProblemDetails` (`application/problem+json`) thống nhất với khai báo OpenAPI.

## Kiểm thử và nghiệm thu

- Vitest/Testing Library bao phủ route, submit form, loading/error/success/empty state và quyền truy cập Admin.
- Backend test xác nhận role, validation khoảng ngày và phép tổng hợp report nếu endpoint mới được thêm.
- `npm test -- --run` và `npm run build` phải thành công.
- Vitest chỉ thu thập tests trong `frontend/src`; Playwright dùng `frontend/e2e`. Tạo E2E suite trước khi chạy lại cả hai bộ để xác nhận chúng không thu thập nhầm tests của nhau.
- Chạy các test backend bị ảnh hưởng và build solution; không bắt buộc chạy bộ integration MySQL nếu môi trường nộp không có database, nhưng phải ghi rõ phần chưa chạy.
- Kiểm tra thủ công các route ở desktop và viewport mobile cơ bản.
- Chạy một lượt end-to-end với frontend và API/database thật: dashboard → chi tiết đơn, đổi preset report và đối chiếu dữ liệu seed, reset password đầy đủ rồi đăng nhập bằng mật khẩu mới.
- Cập nhật sitemap, functional requirements và ảnh/chỉ dẫn chụp màn hình để tài liệu không tuyên bố khác với code.

Frontend là bên duy nhất cập nhật `docs/architecture/sitemap.md` ở gate cuối. Backend cập nhật OpenAPI, functional requirements và phần phân tích; cách chia này tránh xung đột khi hai task chạy song song.

## Thứ tự giao hàng và timebox

1. Khôi phục mật khẩu và 404: 4–7 giờ.
2. Admin Dashboard: 5–8 giờ.
3. Sales Report tối thiểu: 5–8 giờ; dừng ở bảng số liệu nếu endpoint/report phức tạp vượt timebox.
4. Privacy, Terms, tài liệu, build và smoke test: 3–5 giờ.
5. Support/FAQ chỉ làm khi còn ít nhất 2 giờ trước thời điểm đóng gói bài nộp.

Tổng phạm vi bắt buộc dự kiến 17–28 giờ tập trung. Nếu bị chậm, giữ thứ tự ưu tiên trên; không hy sinh kiểm thử khôi phục mật khẩu, quyền Admin hay build production để thêm trang tùy chọn.
