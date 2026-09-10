# Bao cao chay UI test - 2026-09-10

Pham vi: chay test truc tiep tren giao dien `http://localhost:5173` bang Chrome cho khach hang va in-app browser cho admin.

Moi truong: cac container `online-supermarket-frontend-1`, `online-supermarket-api-1`, `online-supermarket-mysql-1` duoc start lai tu image/container co san. Khong rebuild source trong workspace. HEAD workspace khi bat dau la `42ca5977c2143f9b498e18f968845cbcf2afea5b`, nhung ket qua nay phan anh build dang chay trong container hien tai.

Du lieu QA da tao:
- Customer: `qa.ui.20260910.c1@example.test` / display `QA_UI_20260910_C1`.
- Dia chi giao hang: `QA UI Nguoi nhan`, `0900000910`, `10 Duong Kiem Thu`, `Phuong Test`, `Quan Test`, `TP Ho Chi Minh`.
- Don Delivery COD hoan tat: `93fdd938-52bf-4c63-8afd-3fef94cd7070`.
- Don Pickup COD da huy: `118c7d00-86a6-4969-a6df-1d07c1036ce8`.
- Don Pickup VNPay da huy sau redirect sandbox loi: `f9d528f3-9ae5-45fa-b427-7ee887b1ca21`.

## Ket qua chinh

PASS:
- Homepage/catalog co the mo, API status san sang.
- Tim kiem khong co ket qua hien empty state dung.
- Loc brand Apple tra 7 san pham; xoa loc khoi phuc catalog.
- Phan trang catalog hien 1-20/28 va 21-28/28 dung; nut trang truoc/sau disabled dung o bien.
- Login rong, sai password, login user seed, reload giu auth deu duoc kiem.
- Dang ky customer moi thanh cong; dia chi moi luu duoc va con sau reload.
- PDP chan them gio khi chua chon kho; chon kho hien gia va ton.
- PDP chan so luong vuot ton kho.
- Cart them item, tang so luong, tinh subtotal dung.
- Checkout Delivery tu dien dia chi mac dinh, cong phi 15.000 VND, tong dung.
- Coupon sai bao `Ma giam gia khong ton tai`.
- Checkout COD tao don, xoa gio, hien success.
- Admin login rieng session, danh sach don co don moi, chi tiet don khop tong tien.
- Reservation sau khi tao don Delivery: AirPods Binh Thanh `on-hand 40 / reserved 2 / available 38`.
- Vong doi admin Delivery: Confirmed -> Preparing -> Shipped -> Delivered -> Completed.
- Sau Completed: AirPods Binh Thanh `on-hand 38 / reserved 0 / available 38`.
- Sales report sau Completed co ngay `2026-09-10`, 1 don, doanh thu `11.995.000 VND`, tong `116.965.000 VND`.
- Review sau Completed: link viet danh gia xuat hien, gui 5 sao thanh cong, danh gia hien tren PDP.
- Doi kho trong cart co dialog canh bao; chon giu gio thi giu item, chon doi kho va xoa gio thi gio trong.
- Pickup COD: phi ship 0, tong bang gia san pham, tao don thanh cong.
- Huy don Pickup tu admin: kho Quan 1 khong tru on-hand, reserved ve 0.
- Customer bi chan vao admin route; he thong redirect ve homepage.
- Admin route chinh mo duoc: dashboard, users, branches, categories, brands, products, promotions, inventory, orders, sales report, forecast, recommendations.

FAIL / Finding:
- Loc gia min > max khong bao validation. UI chap nhan chip `5.000.000 VND - 1.000.000 VND` va tra empty state.
- Seed data tieng Viet bi mojibake o branch/category/product/unit/description, vi du `AptechMart B??nh Th???nh`, `c??i`, mo ta AirPods bi hong dau. Du lieu customer moi nhap tieng Viet hien dung.
- Lich su trang thai don hang khong day du. Sau COD auto Confirmed, lich su van la `Pending -> Pending`. Sau moi lan cap nhat admin, lich su chi giu dong tao don va lan cap nhat moi nhat, cac lan trung gian bien mat. Customer cung khong thay lich su Completed/Cancelled.
- Payment COD van `PendingCollection` sau khi don Delivery da `Completed`; can xac minh nghiep vu thu tien, nhung day la diem rui ro.
- Sau khi gui review thanh cong, UI hien alert `Lien ket danh gia khong con hop le` trong khi danh gia da duoc luu.
- Compare chi giu toi da 2 san pham va mat sau reload. Neu requirement la 3-4 san pham va persistence thi khong dat.
- Doi kho tren PDP khi cart dang co item khong canh bao; cart van giu kho cu. Canh bao chi co trong man cart.
- Trang Pickup detail hien `N/A`, `N/A`, `Pickup at branch`; dung ve logic nhung text chua than thien.
- Link san pham tinh tren homepage, vi du `Smart Tivi Neo QLED 4K 55 inch Samsung QA55QN85D`, dan den catalog nhung khong tim thay san pham.
- Homepage noi `Hon 15 chi nhanh`, trong khi UI branch/catalog hien 3 chi nhanh seed.
- VNPay sandbox redirect den `https://sandbox.vnpayment.vn/test?orderId=f9d528f3-9ae5-45fa-b427-7ee887b1ca21&amount=2190000.00` va tra trang loi `404 - File or directory not found`.
- Admin Forecast hien lan chay gan nhat `Succeeded` nhung dong thoi co alert `Khong the tai du bao. Vui long thu lai.`

## Cac muc chi moi quet UI, chua test CRUD sau

- Admin users: da mo danh sach va thay combobox doi trang thai, chua thay doi trang thai user.
- Admin branches: da mo danh sach va thay tao/sua, chua tao/sua branch.
- Admin categories/brands/products: da mo form tao, danh sach, nut sua/vo hieu hoa; chua tao/sua/vo hieu hoa ban ghi.
- Admin promotions: da mo danh sach va form entry point; chua tao/sua ma moi.
- MoMo sandbox: chua chay; VNPay da chay va loi redirect.
- Thanh toan thanh cong/that bai tu gateway ben ngoai: chua xac thuc do sandbox URL tra 404.

## Bang chung

Thu muc artifact: `C:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/ui-full-test/2026-09-10-run01`

Mot so file chinh:
- `sales-report-after-completed.png`
- `inventory-reserved.png`
- `inventory-after-completed.png`
- `admin-order-completed.png`
- `review-after-submit.png`
- `compare-after-three-adds.png`
- `homepage-static-product-link-result.png`
- `checkout-vnpay-after-place.png`
- `customer-pickup-order-cancelled.png`

## Dot chay bo sung theo yeu cau "test not nhung gi co the"

Thoi diem: tiep tuc cung ngay 2026-09-10, mo lai tab customer/admin moi vi tab Chrome test cu da dong. Session dang nhap van con hop le.

PASS bo sung:
- Promotion validation: tao coupon phan tram `150%` bi chan voi alert `Giam theo phan tram khong vuot qua 100.`
- Promotion CRUD: tao coupon co dinh `QA_UI_0910_25K_64888`, gia tri 25.000 VND, gioi han 1 luot.
- Checkout coupon: ap coupon tren gio `Apple Magic Mouse` 2.190.000 VND, giam 25.000 VND, tong con 2.165.000 VND.
- Coupon usage limit: sau khi dat don, admin promotion tang len `1/1`; dung lai ma thi bi chan voi alert `Ma giam gia da het luot su dung.`
- Cleanup payment/coupon orders: da huy `b769a6ee-9842-4530-8783-9fdf97e97bd8` va `cbe19c33-3b2f-4e0d-ac2e-dca464c3934c`; kho Magic Mouse Quan 1 ve `on-hand 30 / reserved 0 / available 30`.
- Catalog admin CRUD: tao brand QA, category QA, product QA khong anh thanh cong; danh sach san pham tang len 29.
- Product khong anh: catalog/PDP hien placeholder thay anh; PDP hien dung brand/category/SKU/gia/don vi moi nhap.
- Product khong co inventory: chon kho tren PDP hien `San pham khong co tai kho nay`, nut them gio va so luong bi disable.
- Product deactivate: vo hieu hoa product QA qua dialog; admin hien trang thai `Vo hieu hoa`; catalog khach khong con hien product QA.
- Branch CRUD: tao branch QA, sua ten va tat hoat dong thanh cong; branch da tat khong hien tren trang `/branches` cua khach.
- Recommendation job: bam `Chay lai lan tinh`, job moi `23da442e` hoan thanh thanh cong va bang ket qua duoc cap nhat.
- Forecast job: bam `Chay lai du bao`, job moi `4659346f` duoc enqueue va hien `Succeeded` trong lich su.
- Mobile 390x844: homepage va cart tai duoc; catalog dung nut `Bo loc & Tim kiem` de mo drawer filter; drawer co input search, branch/category/brand/price.
- UX/XSS review: sua review voi chuoi `<b>QA_UI_XSS_SAFE</b>`; UI hien nhu text, khong render thanh the HTML. Da khoi phuc comment review ve noi dung QA ban dau.

FAIL / Finding bo sung:
- MoMo sandbox redirect den `https://www.momo.vn/test?orderId=cbe19c33-3b2f-4e0d-ac2e-dca464c3934c&amount=2190000.00` va trang MoMo tra `404 Page Not Found`.
- Admin product search theo SKU loc dung trong admin, nhung public catalog search theo SKU `QA-SKU-44762` van hien nhieu san pham khac thay vi chi product khop.
- Branch edit form: truong `So dien thoai` trong dialog edit hien rong du bang danh sach co so dien thoai; khi luu khong sua phone thi bang van giu so cu. Day la rui ro binding/form UX.
- Sales report date range: thao tac doi `from/to` qua UI/locator khong lam gia tri hien thi thay doi; report giu mac dinh `2026-08-12 -> 2026-09-10`, khong co alert validation cho `from > to`.
- Forecast van hien alert `Khong the tai du bao. Vui long thu lai.` du job moi da `Succeeded`.

Van chua the ket luan het 100% plan:
- Reset/forgot password va email test: thieu hop thu/link reset quan sat duoc trong UI.
- Gateway success/cancel/fail that cho VNPay/MoMo: ca hai sandbox URL hien tai tra 404, chi kiem duoc den buoc tao don/redirect va cleanup.
- Tranh ton C1/C2 bang hai customer context doc lap: chua tao/duy tri duoc C2 rieng va fixture P2 ton cuoi theo yeu cau plan.
- Access/refresh token het han, thu hoi token, khoa user dang co phien: can dieu kien thoi gian/fixture hoac chap nhan tac dong len user rieng; chua chay sau.
- Network offline/throttling va loi job Failed: can cong cu dieu khien network/fixture loi rieng; chua tai tao qua UI hien co.
- CRUD day du moi bien the cho category/brand/promotion pagination vuot trang, ngay hieu luc coupon, coupon het han/chua hieu luc: UI promotion hien tai khong co truong ngay, va chua tao hang loat du lieu vuot trang.
