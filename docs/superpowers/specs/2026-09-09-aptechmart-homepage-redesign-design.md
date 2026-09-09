# AptechMart Homepage Redesign

## Mục tiêu

Thiết kế lại homepage AptechMart theo tinh thần bán lẻ của Kangaroo Việt Nam nhưng hiện đại, thoáng và dễ quét hơn. Giữ nguyên thương hiệu AptechMart, dữ liệu sản phẩm, route, API, logic giỏ hàng và hệ thống gợi ý hiện có.

Thành công được đo bằng các tiêu chí:

- Người dùng nhận ra ngay chiến dịch chính, danh mục và sản phẩm bán chạy.
- Mật độ nội dung thấp hơn trang tham khảo nhưng vẫn giữ đủ lối vào mua hàng.
- Homepage hoạt động tốt trên desktop và mobile, không tràn ngang ngoài các vùng cuộn chủ đích.
- Điều hướng bàn phím, độ tương phản và reduced motion được hỗ trợ.
- Không thêm thư viện mới và không thay đổi backend.

## Hướng thiết kế

Chọn hướng **Retail hiện đại**: hero chính rộng kết hợp hai banner phụ, theo sau bởi danh mục nhanh, cam kết dịch vụ và các kệ sản phẩm. Trang tham khảo được dùng để học cấu trúc thương mại; không sao chép nguyên giao diện, nội dung hay tài sản hình ảnh.

Điểm nhấn duy nhất là hero dạng campaign board. Những thành phần còn lại sử dụng bề mặt sáng, phân cấp chữ rõ và chuyển động tiết chế.

## Hệ thống hình ảnh

### Màu sắc

- Xanh thương hiệu: `#159447`
- Xanh đậm cho chữ và CTA: `#0B5D37`
- Cam khuyến mãi: `#FF7A1A`
- Nền trang: `#F4F7F5`
- Bề mặt sản phẩm: `#FFFFFF`
- Chữ chính: `#17211B`

Màu cam chỉ dùng cho giá trị khuyến mãi hoặc tín hiệu khẩn cấp. Màu xanh giữ vai trò nhận diện và hành động chính. Các viền, nền phụ và chữ thứ cấp dùng biến thể trung tính có độ tương phản đạt chuẩn.

### Typography

Sử dụng **Be Vietnam Pro** cho toàn homepage. Tiêu đề dùng trọng lượng 600–700; nội dung dùng 400–500. Hạn chế viết hoa toàn bộ, giữ dòng nội dung dưới khoảng 80 ký tự và ưu tiên căn trái.

### Nguyên tắc

- Mỗi màn hình chỉ có một điểm nhấn chính.
- Khoảng trắng phân nhóm nội dung thay cho nhiều khung và đổ bóng.
- Icon đồng bộ thay cho emoji.
- Nội dung sản phẩm ưu tiên theo thứ tự: ảnh, tên, giá bán, giá cũ/giảm giá, hành động.
- Chuyển động chỉ giải thích thay đổi trạng thái, không tạo hiệu ứng liên tục để gây chú ý.

## Cấu trúc trang

Thứ tự nội dung desktop:

1. Header hiện có: logo, thanh tìm kiếm lớn, tài khoản và giỏ hàng.
2. Điều hướng ngành hàng ngang.
3. Hero campaign board: banner chính chiếm phần lớn chiều rộng; hai banner phụ xếp dọc bên phải.
4. Sáu danh mục nhanh.
5. Dải cam kết dịch vụ.
6. Kệ sản phẩm bán chạy và đồng hồ ưu đãi.
7. Kệ sản phẩm theo ngành hàng có tab lọc.
8. Kệ gợi ý cá nhân hóa hiện có.
9. Banner hệ thống chi nhánh với CTA tìm siêu thị.
10. Lộ trình công nghệ được rút gọn ở cuối trang.

Loại bỏ sidebar danh mục dài, ticker tin tức và các đoạn quảng cáo dày của trang tham khảo. Các thành phần này không phù hợp với mục tiêu giảm mật độ của AptechMart.

Trên mobile, hero và banner phụ xếp dọc. Danh mục nhanh và kệ sản phẩm có thể cuộn ngang trong vùng riêng; phần còn lại xếp một cột. Không dùng kích thước chữ hoặc thẻ quá nhỏ để nhồi nhiều nội dung.

## Thành phần và trách nhiệm

### `HomePage`

Giữ vai trò điều phối thứ tự các section và dữ liệu mock hiện có. Không đưa logic trình bày chi tiết vào component này.

### `HeroSection`

Hiển thị một banner chính và hai banner phụ, điều hướng trước/sau và trạng thái slide. Tự chuyển chậm; tạm dừng khi người dùng tương tác hoặc rê chuột. Nội dung đầu tiên phải hữu ích ngay cả khi animation không chạy.

### `QuickCategoryStrip`

Hiển thị sáu ngành hàng chính bằng icon nhất quán, tên ngắn và một mô tả phụ khi còn đủ không gian. Trên mobile dùng vùng cuộn ngang có điểm dừng rõ.

### `TrustBadges`

Hiển thị bốn cam kết dịch vụ thành một dải nhẹ: giao nhanh, lắp đặt, đổi trả và bảo hành. Không dùng bốn card nặng có cùng độ nổi.

### `FlashSaleCountdown`

Đổi thành kệ “Sản phẩm bán chạy”. Đồng hồ ưu đãi gọn, không phát sáng hoặc nhấp nháy liên tục. Hiển thị tối đa bốn sản phẩm nổi bật ở desktop.

### `CategoryTabs`

Giữ logic lọc hiện tại. Tab có trạng thái active rõ và điều khiển được bằng bàn phím. Chuyển đổi sản phẩm dùng animation nhẹ và tắt theo `prefers-reduced-motion`.

### `ApplianceProductCard`

Chuẩn hóa tỷ lệ ảnh và thứ tự thông tin. Không thay logic điều hướng hoặc giỏ hàng. Ảnh lỗi có nền dự phòng và không làm thay đổi kích thước card.

### Gợi ý, chi nhánh và lộ trình

`RecommendationShelfLoader` tiếp tục dùng luồng dữ liệu và xử lý lỗi hiện tại nhưng được đồng bộ skin với các kệ khác. Banner chi nhánh dùng một CTA rõ. Lộ trình công nghệ giữ ba giai đoạn nhưng rút gọn nội dung để không cạnh tranh với sản phẩm.

## Dữ liệu và luồng trạng thái

Homepage tiếp tục nhận dữ liệu tĩnh từ `mockData` cho hero, danh mục, badge và sản phẩm mẫu. Gợi ý cá nhân hóa giữ nguyên luồng API hiện có. Không tạo API, schema hoặc state toàn cục mới.

Các trạng thái cần hỗ trợ:

- Hero không có banner: ẩn điều hướng và hiển thị nội dung dự phòng ngắn.
- Ảnh sản phẩm lỗi: giữ khung ảnh cố định và hiện placeholder.
- Tab không có sản phẩm: thông báo rõ cùng nút quay về “Tất cả”.
- Kệ gợi ý đang tải, lỗi hoặc rỗng: tái sử dụng hành vi hiện có; đảm bảo layout không bị nhảy lớn.
- Đồng hồ hết hạn: hiển thị “Đã kết thúc”, không tự tạo một chiến dịch giả mới.

## Tương tác và accessibility

- Focus bàn phím luôn nhìn thấy.
- Vùng bấm quan trọng trên mobile tối thiểu khoảng 44px.
- Hero có nhãn truy cập, nút trước/sau và trạng thái slide có ý nghĩa.
- Tab giữ đúng vai trò ARIA và liên kết với tabpanel.
- Tôn trọng `prefers-reduced-motion`; không phụ thuộc animation để truyền tải nội dung.
- Ảnh có `alt` phù hợp và kích thước được giữ chỗ trước khi tải.
- CTA dùng tên hành động cụ thể, nhất quán với trang đích.

## Chiến lược chuyển động và 3D

Ba công cụ hiện có được phân vai rõ để tạo một điểm nhấn cao cấp mà không phủ hiệu ứng lên toàn trang:

- **Three.js** chỉ dùng trong hero cho mô hình TV tương tác. Scene khởi tạo một lần; góc xoay và animation loop dùng `ref` thay vì React state theo từng frame. Dừng render khi hero ngoài viewport hoặc tab trình duyệt bị ẩn, giới hạn pixel ratio và giải phóng toàn bộ geometry, material, renderer khi unmount.
- **GSAP** điều phối một sequence ngắn khi hero xuất hiện và chuyển số countdown. Không dùng hiệu ứng phát sáng, rung hoặc timeline lặp vô hạn.
- **Motion** xử lý chuyển tab sản phẩm, menu mobile và phản hồi hover/tap có ý nghĩa. Không dùng animation xuất hiện giống nhau cho mọi section hoặc mọi card.
- **CSS native** đảm nhiệm hover, focus, responsive và các chuyển tiếp đơn giản.

Khi `prefers-reduced-motion: reduce`, hero không tự xoay, GSAP bỏ sequence và Motion không dùng chuyển động dịch chuyển/phóng to. Nếu WebGL không khả dụng, hero dùng ảnh hoặc CSS fallback giữ nguyên nội dung, CTA và kích thước bố cục. Three.js không được nằm trên critical path của nội dung hoặc thao tác mua hàng.

Ngân sách chuyển động:

- Chỉ một animation loop liên tục và chỉ khi hero đang nhìn thấy.
- Không cập nhật React state trên mỗi animation frame.
- Không tải model 3D hoặc texture từ nguồn ngoài trong phạm vi redesign này.
- Không thêm package mới; tái sử dụng GSAP, Motion và Three.js đã có trong dự án.

## Phạm vi thay đổi

Phạm vi chính nằm trong `frontend/src/features/home/`, gồm `HomePage.tsx`, `HomePage.css`, các component trực tiếp và test liên quan. Chỉ chỉnh `AppShell` hoặc stylesheet dùng chung nếu cần để khớp header với homepage mới; mọi thay đổi dùng chung phải nhỏ và không làm đổi giao diện các route khác.

Không thực hiện:

- Thay backend hoặc hợp đồng API.
- Thêm thư viện UI, icon hoặc animation mới.
- Đổi route, quy trình checkout hoặc logic giỏ hàng.
- Sao chép tài sản hình ảnh hoặc nội dung từ Kangaroo Việt Nam.
- Thiết kế lại toàn bộ website ngoài homepage.

## Kiểm thử và xác minh

- Cập nhật test `HomePage` cho thứ tự section, nội dung chính, hero controls, countdown và tab sản phẩm.
- Kiểm tra trạng thái rỗng và tương tác tab bằng keyboard.
- Chạy unit test liên quan, lint, type-check và production build.
- Kiểm tra trực quan ở desktop và mobile cho overflow, kích thước vùng bấm, thứ tự nội dung, focus và reduced motion.
- Kiểm tra console không có lỗi mới và các CTA quan trọng dẫn đúng route.

## Rủi ro và kiểm soát

- CSS homepage hiện có lớn và có thể chồng selector: ưu tiên namespace hiện tại và xóa rule không còn dùng thay vì ghi đè nhiều lớp.
- Component animation hiện dùng cả GSAP và Motion: tái sử dụng dependency đã cài, nhưng giảm animation liên tục; không bổ sung thư viện.
- `Hero3DShowcase` hiện gắn vòng lặp nhàn rỗi với state góc xoay và effect khởi tạo renderer: tách state tương tác khỏi animation frame, giữ renderer ổn định và kiểm thử cleanup để tránh tạo lại WebGL context.
- Thay đổi `AppShell` có blast radius lớn: chỉ thực hiện sau khi xác minh homepage không thể đạt thiết kế bằng style/component cục bộ.
- Dữ liệu mock có chất lượng ảnh khác nhau: dùng `aspect-ratio`, `object-fit` và placeholder để giữ layout ổn định.
