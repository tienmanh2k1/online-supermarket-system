# AptechMart Homepage Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây lại homepage AptechMart theo hướng retail hiện đại, có hero 3D tiết chế, phân cấp mua hàng rõ và responsive/accessibility hoàn chỉnh mà không đổi backend.

**Architecture:** `HomePage` tiếp tục là composition root và truyền dữ liệu mock xuống các component độc lập. GSAP chỉ điều phối hero/countdown, Motion chỉ xử lý chuyển trạng thái UI, Three.js chỉ chạy trong hero và có fallback; CSS homepage sở hữu toàn bộ token, layout và responsive.

**Tech Stack:** React 19, TypeScript 5.9, React Router 7, Vitest + Testing Library, GSAP 3, Motion 13, Three.js 0.186, CSS.

**Spec:** `docs/superpowers/specs/2026-09-09-aptechmart-homepage-redesign-design.md`

## Global Constraints

- Giữ nguyên thương hiệu AptechMart, dữ liệu sản phẩm, route, API, logic giỏ hàng và hệ thống gợi ý hiện có.
- Không thay backend hoặc hợp đồng API; không thêm package mới.
- Không sao chép nội dung hoặc tài sản hình ảnh từ Kangaroo Việt Nam.
- Màu lõi: `#159447`, `#0B5D37`, `#FF7A1A`, `#F4F7F5`, `#FFFFFF`, `#17211B`.
- Dùng Be Vietnam Pro với fallback hệ thống; nội dung căn trái và hạn chế viết hoa toàn bộ.
- Three.js không nằm trên critical path; phải có CSS/ảnh fallback và cleanup WebGL đầy đủ.
- Tôn trọng `prefers-reduced-motion`; không dùng animation lặp vô hạn ngoài một loop 3D khi hero đang hiển thị.
- Không cập nhật React state trên mỗi animation frame.
- Không sửa `AppShell` trừ khi CSS/component cục bộ không thể đáp ứng header homepage.

---

### Task 1: Khóa hợp đồng bố cục homepage bằng test

**Files:**
- Modify: `frontend/src/features/home/HomePage.test.tsx:29-320`
- Modify: `frontend/src/features/home/HomePage.tsx:35-120`

**Interfaces:**
- Consumes: `HomePage(): JSX.Element`, `renderHomePage()` test helper hiện có.
- Produces: các section có `data-testid`: `home-hero`, `home-quick-categories`, `home-trust-strip`, `home-bestsellers`, `home-category-showcase`, `home-recommendations`, `home-branches`, `home-roadmap`.

- [ ] **Step 1: Viết test thất bại cho thứ tự section**

Thêm vào `HomePage.test.tsx`:

```tsx
it('renders the retail sections in the approved purchase journey order', () => {
  renderHomePage()

  const ids = [
    'home-hero',
    'home-quick-categories',
    'home-trust-strip',
    'home-bestsellers',
    'home-category-showcase',
    'home-recommendations',
    'home-branches',
    'home-roadmap',
  ]
  const sections = ids.map((id) => screen.getByTestId(id))

  sections.slice(1).forEach((section, index) => {
    expect(sections[index].compareDocumentPosition(section) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy()
  })
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: FAIL vì các `data-testid` mới chưa tồn tại.

- [ ] **Step 3: Thêm contract tối thiểu vào composition**

Thêm `data-testid` vào chính section/component root tương ứng. Với wrapper có sẵn trong `HomePage.tsx`, dùng:

```tsx
<section
  className="home-recommendations-section"
  data-testid="home-recommendations"
  aria-label="Gợi ý sản phẩm thông minh"
>
```

Gắn test id còn lại vào root của component thay vì tạo wrapper thừa. Rút ngắn copy roadmap nhưng giữ nguyên ba giai đoạn và `id="roadmap"`.

- [ ] **Step 4: Chạy lại test**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: PASS toàn bộ test homepage.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/home/HomePage.tsx frontend/src/features/home/HomePage.test.tsx
git commit -m "test: lock homepage retail section order"
```

---

### Task 2: Chuyển HeroSection thành campaign board hiện đại

**Files:**
- Modify: `frontend/src/features/home/components/HeroSection.tsx:15-340`
- Modify: `frontend/src/features/home/HomePage.css:41-940`
- Test: `frontend/src/features/home/HomePage.test.tsx:35-57,134-178,265-288`

**Interfaces:**
- Consumes: `HeroSectionProps { categories; banners; subBanners; autoPlayIntervalMs? }` để không đổi caller.
- Produces: `HeroSection` root `data-testid="home-hero"`, slider controls hiện có, `aria-live="polite"` cho nội dung slide và navigation ngành hàng rút gọn.

- [ ] **Step 1: Viết test thất bại cho hero mới và fallback rỗng**

```tsx
it('renders a campaign board with one main promotion and two supporting offers', () => {
  renderHomePage()
  expect(screen.getByTestId('home-hero')).toBeInTheDocument()
  expect(screen.getByTestId('hero-main-slider')).toBeInTheDocument()
  expect(screen.getAllByTestId('sub-banner-container')).toHaveLength(2)
  expect(screen.getByRole('navigation', { name: 'Ngành hàng nổi bật' })).toBeInTheDocument()
})

it('keeps useful hero content when no banner is supplied', () => {
  render(
    <MemoryRouter>
      <HeroSection categories={[]} banners={[]} subBanners={MOCK_SUB_BANNERS} />
    </MemoryRouter>,
  )
  expect(screen.getByText('Khám phá thiết bị cho ngôi nhà hiện đại')).toBeInTheDocument()
  expect(screen.queryByTestId('slider-btn-next')).not.toBeInTheDocument()
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run: `npm test -- --run src/features/home/HomePage.test.tsx -t "campaign board|useful hero"`

Expected: FAIL vì root/navigation/fallback chưa tồn tại.

- [ ] **Step 3: Rút hero từ ba cột thành hai cột**

Giữ search ở header ứng dụng; trong hero chỉ giữ navigation ngành hàng ngắn, slider và hai offer phụ. Thay mega-menu bằng:

```tsx
<nav className="appliance-hero__categories" aria-label="Ngành hàng nổi bật">
  {categories.slice(0, 6).map((category) => (
    <Link key={category.id} to={`/browse?categoryId=${encodeURIComponent(category.slug)}`}>
      <span aria-hidden="true">{category.icon}</span>
      <span>{category.name}</span>
    </Link>
  ))}
</nav>
```

Nếu `activeBanner` không tồn tại, render nội dung tĩnh cùng CTA `<Link to="/browse">Xem tất cả sản phẩm</Link>`. Xóa state `hoveredCategory`, `searchTerm`, `handleSearchSubmit` và markup mega-menu/search trùng với `AppShell`.

- [ ] **Step 4: Tiết chế GSAP và hỗ trợ reduced motion**

```tsx
const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

useGSAP(() => {
  if (!textOverlayRef.current || reduceMotion) return
  gsap.fromTo(
    textOverlayRef.current.children,
    { autoAlpha: 0, y: 14 },
    { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.07, ease: 'power2.out' },
  )
}, { dependencies: [currentBannerIndex], scope: textOverlayRef })
```

Giữ autoplay, pause khi hover/focus; thêm `onFocusCapture={() => setIsPaused(true)}` và `onBlurCapture={() => setIsPaused(false)}`. Không dùng blur, bounce hoặc timeline lặp.

- [ ] **Step 5: Viết CSS campaign board**

Định nghĩa token tại `.appliance-home-page`, layout `grid-template-columns: minmax(0, 2fr) minmax(240px, .72fr)`, hero cao ổn định bằng `aspect-ratio`, hai sub-banner xếp dọc. Xóa toàn bộ selector `.appliance-mega-menu*` và style hero cũ không còn caller.

- [ ] **Step 6: Chạy test hero và toàn file**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: PASS; không còn test dựa vào mega-menu/search bên trong hero.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/home/components/HeroSection.tsx frontend/src/features/home/HomePage.css frontend/src/features/home/HomePage.test.tsx
git commit -m "feat: redesign homepage hero campaign board"
```

---

### Task 3: Sửa vòng đời và hiệu năng Hero3DShowcase

**Files:**
- Modify: `frontend/src/features/home/components/Hero3DShowcase.tsx:1-332`
- Test: `frontend/src/features/home/components/Hero3DShowcase.test.tsx`

**Interfaces:**
- Consumes: `Hero3DShowcaseProps { className?: string; autoRotateSpeed?: number }`.
- Produces: cùng public component; canvas có `aria-hidden="true"`, fallback có `data-testid="hero-3d-fallback"`; một WebGL context cho mỗi mount.

- [ ] **Step 1: Tạo test vòng đời thất bại**

```tsx
import { render } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Hero3DShowcase } from './Hero3DShowcase'

describe('Hero3DShowcase', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('falls back without WebGL and cancels its animation frame on unmount', () => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null)
    const cancel = vi.spyOn(window, 'cancelAnimationFrame')
    const { unmount } = render(<Hero3DShowcase />)
    unmount()
    expect(cancel).toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại hoặc lộ cleanup không ổn định**

Run: `npm test -- --run src/features/home/components/Hero3DShowcase.test.tsx`

Expected: FAIL cho đến khi animation lifecycle được gom về một effect ổn định.

- [ ] **Step 3: Thay state góc quay bằng refs**

```tsx
const rotationRef = useRef({ x: 6, y: 25 })
const draggingRef = useRef(false)
const frameRef = useRef<number | null>(null)
const visibleRef = useRef(true)
```

Pointer handlers cập nhật `rotationRef.current`; chỉ giữ state nếu cần đổi nhãn “Đang xoay”. Effect khởi tạo Three.js có dependency `[]`, không phụ thuộc `rotationX`/`rotationY`.

- [ ] **Step 4: Thêm visibility gates và cleanup**

Dùng `IntersectionObserver` cho container và `visibilitychange` cho document. Trong `animate`, chỉ gọi `renderer.render` khi cả hai visible. Cleanup phải disconnect observer, bỏ listener, cancel frame, dispose geometry/material và gọi `renderer.dispose()`.

- [ ] **Step 5: Thêm reduced-motion/fallback**

Khi reduce motion, không tự xoay/rung; vẫn cho phép drag chủ động. Khi WebGL không có, render CSS/ảnh fallback tĩnh và không tạo vòng loop nhàn rỗi.

- [ ] **Step 6: Chạy test component và homepage**

Run: `npm test -- --run src/features/home/components/Hero3DShowcase.test.tsx src/features/home/HomePage.test.tsx`

Expected: PASS; test toggle 3D cũ vẫn hoạt động.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/home/components/Hero3DShowcase.tsx frontend/src/features/home/components/Hero3DShowcase.test.tsx
git commit -m "perf: stabilize homepage threejs lifecycle"
```

---

### Task 4: Làm gọn danh mục nhanh, trust strip và product card

**Files:**
- Modify: `frontend/src/features/home/components/QuickCategoryStrip.tsx:1-80`
- Modify: `frontend/src/features/home/components/TrustBadges.tsx:1-25`
- Modify: `frontend/src/features/home/components/ApplianceProductCard.tsx:1-220`
- Modify: `frontend/src/features/home/HomePage.css:942-989,1152-1450`
- Test: `frontend/src/features/home/HomePage.test.tsx:99-132,243-264,290-310`

**Interfaces:**
- Consumes: `KANGAROO_QUICK_CATEGORIES`, `TrustBadgeItem[]`, `ApplianceProduct`.
- Produces: cùng props; root test ids `home-quick-categories`, `home-trust-strip`; card có hierarchy ảnh → tên → giá → CTA.

- [ ] **Step 1: Viết test thất bại cho contract hiển thị**

```tsx
it('renders six quick categories and four service commitments', () => {
  renderHomePage()
  expect(within(screen.getByTestId('home-quick-categories')).getAllByRole('link')).toHaveLength(6)
  expect(within(screen.getByTestId('home-trust-strip')).getAllByRole('listitem')).toHaveLength(4)
})

it('keeps the product purchase hierarchy concise', () => {
  renderHomePage()
  const card = screen.getAllByTestId('appliance-product-card')[0]
  expect(within(card).getByRole('img')).toHaveAttribute('width')
  expect(within(card).getByRole('link', { name: /xem chi tiết/i })).toBeInTheDocument()
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run: `npm test -- --run src/features/home/HomePage.test.tsx -t "six quick|purchase hierarchy"`

Expected: FAIL vì semantic list/test id/kích thước ảnh chưa đủ.

- [ ] **Step 3: Chuẩn hóa semantic và icon**

Dùng `<ul>`/`<li>` cho hai dải. Giữ icon dữ liệu hiện có trong lần đầu để không thêm package; bọc bằng `<span aria-hidden="true">`. Đổi tên hằng `KANGAROO_QUICK_CATEGORIES` thành `APTECHMART_QUICK_CATEGORIES` và cập nhật mọi import/test.

- [ ] **Step 4: Bỏ tilt/glare nặng khỏi product card**

Xóa `useMotionValue`, `useSpring`, `useTransform`, `handleMouseMove`, `handleMouseLeave` và lớp glare. Giữ một `motion.article` hoặc CSS hover nhỏ; ảnh khai báo `width`, `height`, `loading="lazy"`, `decoding="async"`. Giữ placeholder `onError`, giá, badge giảm giá và CTA.

- [ ] **Step 5: Viết CSS thoáng và mobile scroll**

Danh mục dùng 6 cột ở desktop và `grid-auto-flow: column; grid-auto-columns: minmax(150px, 42vw); overflow-x:auto; scroll-snap-type:x mandatory` dưới 768px. Product card không tilt, chỉ dịch `translateY(-2px)` trên thiết bị có hover.

- [ ] **Step 6: Chạy test**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: PASS sau khi bỏ test “3D tilt glare” cũ và thay bằng hierarchy test.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/home/components/QuickCategoryStrip.tsx frontend/src/features/home/components/TrustBadges.tsx frontend/src/features/home/components/ApplianceProductCard.tsx frontend/src/features/home/HomePage.css frontend/src/features/home/HomePage.test.tsx
git commit -m "feat: simplify homepage discovery and product cards"
```

---

### Task 5: Tiết chế bestsellers/countdown và category tabs

**Files:**
- Modify: `frontend/src/features/home/components/FlashSaleCountdown.tsx:1-174`
- Modify: `frontend/src/features/home/components/CategoryTabs.tsx:1-140`
- Modify: `frontend/src/features/home/HomePage.css:990-1151,1761-1840`
- Test: `frontend/src/features/home/HomePage.test.tsx:58-98,180-241`

**Interfaces:**
- Consumes: `FlashSaleCountdownProps`, `CategoryTabsProps` không đổi.
- Produces: roots `data-testid="home-bestsellers"`, `data-testid="home-category-showcase"`; countdown dừng ở zero; tab semantics hiện có.

- [ ] **Step 1: Viết test thất bại cho reduced motion và roots**

```tsx
it('labels the bestsellers and category shelves as distinct sections', () => {
  renderHomePage()
  expect(screen.getByTestId('home-bestsellers')).toHaveAccessibleName('Sản phẩm bán chạy')
  expect(screen.getByTestId('home-category-showcase')).toHaveAccessibleName('Sản phẩm theo ngành hàng')
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run: `npm test -- --run src/features/home/HomePage.test.tsx -t "distinct sections"`

Expected: FAIL do test id/accessibility name chưa tồn tại.

- [ ] **Step 3: Gỡ GSAP loop khỏi countdown**

Xóa flame timeline và glow lặp. Chỉ animate số giây khi thay đổi nếu không reduce motion:

```tsx
if (!reduceMotion && prevSeconds.current !== timeLeft.seconds && secondsRef.current) {
  gsap.fromTo(secondsRef.current, { y: -4 }, { y: 0, duration: 0.18, ease: 'power1.out' })
}
```

Giữ `calculateTimeLeft`, interval cleanup và hành vi `00:00:00` hiện có.

- [ ] **Step 4: Giảm Motion trong CategoryTabs**

Giữ active indicator và `AnimatePresence`; bỏ scale mạnh, icon lắc vô hạn và spring bounce. Khi reduce motion hoặc test, dùng `initial={false}` và transition duration `0`.

- [ ] **Step 5: Cập nhật CSS section header, tabs và grid**

Header bestsellers dùng nền trắng/xám xanh, cam chỉ ở discount/countdown. Grid 4 cột desktop, 2 cột tablet, mobile cuộn ngang với card tối thiểu 76vw. Tab bar cuộn ngang nhưng giữ focus outline.

- [ ] **Step 6: Chạy test**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: PASS cho countdown hết hạn, tab filtering và empty reset.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/home/components/FlashSaleCountdown.tsx frontend/src/features/home/components/CategoryTabs.tsx frontend/src/features/home/HomePage.css frontend/src/features/home/HomePage.test.tsx
git commit -m "feat: refine homepage product shelves"
```

---

### Task 6: Hoàn thiện lower homepage, responsive và accessibility

**Files:**
- Modify: `frontend/src/features/home/HomePage.tsx:56-120`
- Modify: `frontend/src/features/home/HomePage.css:1841-2050`
- Modify if strictly required: `frontend/src/app/AppShell.tsx`
- Test: `frontend/src/features/home/HomePage.test.tsx`
- Modify: `frontend/e2e/submission-pages.spec.ts`

**Interfaces:**
- Consumes: `RecommendationShelfLoader`, route `/branches`, anchor `#roadmap`.
- Produces: branch banner CTA “Tìm siêu thị gần bạn”; compact three-stage roadmap; responsive homepage with no unintended horizontal page overflow.

- [ ] **Step 1: Viết test thất bại cho nội dung lower homepage**

```tsx
it('keeps recommendations, branch discovery and the compact roadmap', () => {
  renderHomePage()
  expect(screen.getByTestId('home-recommendations')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Tìm siêu thị gần bạn' })).toHaveAttribute('href', '/branches')
  expect(within(screen.getByTestId('home-roadmap')).getAllByRole('heading', { level: 3 })).toHaveLength(3)
})
```

- [ ] **Step 2: Chạy test để xác nhận thất bại**

Run: `npm test -- --run src/features/home/HomePage.test.tsx -t "compact roadmap"`

Expected: FAIL vì CTA copy/test ids chưa khớp.

- [ ] **Step 3: Rút gọn branch banner và roadmap**

Đổi CTA chính xác thành “Tìm siêu thị gần bạn”. Mỗi roadmap card chỉ giữ status, heading và một câu mô tả; không thay logic scroll hash hiện có.

- [ ] **Step 4: Thêm reduced motion và focus CSS toàn trang**

```css
.appliance-home-page :where(a, button, input):focus-visible {
  outline: 3px solid #ff7a1a;
  outline-offset: 3px;
}

@media (prefers-reduced-motion: reduce) {
  .appliance-home-page *,
  .appliance-home-page *::before,
  .appliance-home-page *::after {
    scroll-behavior: auto !important;
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

Đảm bảo chỉ các track được chỉ định có `overflow-x:auto`; `.appliance-home-page` không che overflow để tránh giấu lỗi layout.

- [ ] **Step 5: Thêm smoke test Playwright desktop/mobile**

Trong `submission-pages.spec.ts`, thêm test homepage cho viewport 1440×900 và 390×844:

```ts
for (const viewport of [{ width: 1440, height: 900 }, { width: 390, height: 844 }]) {
  test(`homepage fits ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport)
    await page.goto('/')
    await expect(page.getByTestId('home-hero')).toBeVisible()
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)
    expect(overflow).toBe(false)
  })
}
```

- [ ] **Step 6: Chạy unit test và e2e homepage**

Run: `npm test -- --run src/features/home/HomePage.test.tsx`

Expected: PASS.

Run: `npx playwright test e2e/submission-pages.spec.ts --grep "homepage fits"`

Expected: PASS ở cả desktop và mobile. Nếu e2e yêu cầu web server, chạy bằng command/project config hiện có thay vì tạo config mới.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/home/HomePage.tsx frontend/src/features/home/HomePage.css frontend/src/features/home/HomePage.test.tsx frontend/e2e/submission-pages.spec.ts
git commit -m "feat: finish responsive accessible homepage"
```

---

### Task 7: Xác minh tích hợp và dọn CSS chết

**Files:**
- Modify if needed: `frontend/src/features/home/HomePage.css`
- Modify if needed: files touched in Tasks 1–6 only.

**Interfaces:**
- Consumes: toàn bộ homepage implementation từ Tasks 1–6.
- Produces: production build không lỗi, test pass, không selector/component cũ bị bỏ quên.

- [ ] **Step 1: Tìm symbol/style không còn dùng**

Run:

```bash
rg -n "appliance-mega-menu|home-hero__search|appliance-card__glare|flash-sale-flame|kg-bestseller" frontend/src/features/home
```

Expected: không có kết quả ngoài test migration note nếu còn chủ đích. Xóa selector/import/comment chết, không tạo alias CSS để giữ code cũ.

- [ ] **Step 2: Chạy toàn bộ unit test frontend**

Run: `npm test -- --run`

Expected: tất cả test PASS; không có unhandled rejection hoặc timer leak.

- [ ] **Step 3: Chạy TypeScript và production build**

Run: `npm run build`

Expected: exit code 0; `tsc --noEmit` và Vite build thành công.

- [ ] **Step 4: Chạy e2e liên quan**

Run: `npx playwright test e2e/submission-pages.spec.ts`

Expected: PASS. Nếu có failure không liên quan do dữ liệu môi trường, ghi lại exact test và log; không sửa ngoài scope homepage.

- [ ] **Step 5: Kiểm tra trực quan có mục tiêu**

Khởi động app bằng `npm run dev`, kiểm tra `/` ở 1440×900 và 390×844:

- Hero chỉ có một điểm nhấn, CTA đọc được trên ảnh.
- Three.js không block nội dung; 2D fallback vẫn dùng được.
- Không có horizontal page overflow; chỉ category/product track được cuộn.
- Tab, slider, CTA và menu dùng được bằng bàn phím.
- Bật reduced motion không còn autoplay/translate/scale đáng kể.
- Console không có WebGL context leak hoặc React warning.

- [ ] **Step 6: Review diff giới hạn phạm vi**

Run: `git diff --stat HEAD~6..HEAD` và `git status --short`

Expected: chỉ các file homepage/test/e2e dự kiến thay đổi; giữ nguyên mọi thay đổi có sẵn của người dùng ngoài phạm vi.

- [ ] **Step 7: Commit cleanup nếu có**

Nếu Step 1–6 tạo thay đổi:

```bash
git add frontend/src/features/home frontend/e2e/submission-pages.spec.ts
git commit -m "chore: verify and clean homepage redesign"
```

Nếu không có diff, không tạo commit rỗng.
