import { render, screen, fireEvent, within } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { HomePage } from './HomePage'
import { HeroSection } from './components/HeroSection'
import { CategoryTabs } from './components/CategoryTabs'
import { FlashSaleCountdown } from './components/FlashSaleCountdown'
import { ApplianceProductCard } from './components/ApplianceProductCard'
import {
  MOCK_CATEGORY_MENU,
  MOCK_HERO_BANNERS,
  MOCK_SUB_BANNERS,
  MOCK_APPLIANCE_PRODUCTS,
} from './mockData'
import type { CategoryTabItem } from './types'
import { recommendationApi } from '../../api/recommendationApi'
import { CompareProvider } from '../compare/CompareContext'

function renderHomePage() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <CompareProvider>
        <HomePage />
      </CompareProvider>
    </MemoryRouter>
  )
}

describe('HomePage Component — Siêu Thị Điện Máy Spec', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(recommendationApi, 'getRecommendations').mockResolvedValue({ items: [] })
  })

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

  it('labels the bestsellers and category shelves as distinct sections', () => {
    renderHomePage()
    expect(screen.getByTestId('home-bestsellers')).toHaveAccessibleName('Sản phẩm bán chạy')
    expect(screen.getByTestId('home-category-showcase')).toHaveAccessibleName('Sản phẩm theo ngành hàng')
  })

  it('renders hero banner container with correct aspect-ratio structure and sub-banners', () => {
    renderHomePage()

    // Main 16:9 slider container
    const mainSlider = screen.getByTestId('hero-main-slider')
    expect(mainSlider).toBeInTheDocument()

    const sliderImage = mainSlider.querySelector('.appliance-slider-image')
    expect(sliderImage).toBeInTheDocument()
    expect(sliderImage).toHaveClass('appliance-slider-image')

    // 2 Fixed 4:3 sub-banners (Trả góp 0% & Thu cũ đổi mới)
    const subBanners = screen.getAllByTestId('sub-banner-container')
    expect(subBanners).toHaveLength(2)

    expect(screen.getByText('Đặc Quyền Trả Góp 0%')).toBeInTheDocument()
    expect(screen.getByText('Thu Cũ Đổi Mới Lên Đời')).toBeInTheDocument()

    // Verify sub-banner images exist for custom graphic replacement
    const subBannerImgs = screen.getAllByRole('img', { name: /banner ưu đãi/i })
    expect(subBannerImgs.length).toBeGreaterThanOrEqual(1)
  })

  it('displays the live flash sale countdown clock with hours, minutes, and seconds', () => {
    renderHomePage()

    const countdown = screen.getByTestId('flash-sale-countdown')
    expect(countdown).toBeInTheDocument()

    const hoursBox = screen.getByTestId('countdown-hours')
    const minutesBox = screen.getByTestId('countdown-minutes')
    const secondsBox = screen.getByTestId('countdown-seconds')

    expect(hoursBox.textContent).toMatch(/^\d{2}$/)
    expect(minutesBox.textContent).toMatch(/^\d{2}$/)
    expect(secondsBox.textContent).toMatch(/^\d{2}$/)
  })

  it('switches category tabs and filters appliance products accordingly', () => {
    renderHomePage()

    // Default tab "Tất Cả Sản Phẩm" is active
    const allTab = screen.getByRole('tab', { name: 'Tất Cả Sản Phẩm' })
    expect(allTab).toHaveClass('active')

    const grid = screen.getByTestId('category-products-grid')

    // Initial grid shows products across categories
    expect(within(grid).getByText('Smart Tivi Neo QLED 4K 55 inch Samsung QA55QN85D')).toBeInTheDocument()
    expect(within(grid).getByText('Tủ lạnh LG Inverter 635 Lít Side-By-Side GR-D257JS')).toBeInTheDocument()

    // Switch to "Tủ Lạnh" tab
    const fridgeTab = screen.getByRole('tab', { name: 'Tủ Lạnh' })
    fireEvent.click(fridgeTab)

    expect(fridgeTab).toHaveClass('active')
    expect(allTab).not.toHaveClass('active')

    // Grid now only displays refrigerators
    expect(within(grid).getByText('Tủ lạnh LG Inverter 635 Lít Side-By-Side GR-D257JS')).toBeInTheDocument()
    expect(within(grid).queryByText('Smart Tivi Neo QLED 4K 55 inch Samsung QA55QN85D')).not.toBeInTheDocument()
    expect(within(grid).queryByText('Máy giặt lồng ngang Electrolux UltimateCare 900 10kg')).not.toBeInTheDocument()
  })

  it('renders all visual badges, pricing hierarchy, and specs on appliance product cards', () => {
    renderHomePage()

    // Visual Badges: Trả góp 0%, Giảm giá, Tem năng lượng
    const installmentBadges = screen.getAllByText('Trả góp 0%')
    expect(installmentBadges.length).toBeGreaterThan(0)

    const discountBadges = screen.getAllByText(/-26%|-29%/)
    expect(discountBadges.length).toBeGreaterThan(0)

    const energyBadges = screen.getAllByText(/⭐ 5 Sao/)
    expect(energyBadges.length).toBeGreaterThan(0)

    // Gift tag
    const giftTags = screen.getAllByText(/Tặng Loa thanh Soundbar trị giá 3.500.000đ/i)
    expect(giftTags.length).toBeGreaterThan(0)

    // Specs summary chips
    const screenSpecs = screen.getAllByText('55 inch 4K')
    expect(screenSpecs.length).toBeGreaterThan(0)

    const hzSpecs = screen.getAllByText('120 Hz')
    expect(hzSpecs.length).toBeGreaterThan(0)
  })

  it('renders the 4 trust service commitments below hero', () => {
    renderHomePage()

    expect(screen.getByText('Giao siêu tốc 2 giờ')).toBeInTheDocument()
    expect(screen.getByText('Lắp đặt miễn phí tận nhà')).toBeInTheDocument()
    expect(screen.getByText('Lỗi 1 đổi 1 trong 30 ngày')).toBeInTheDocument()
    expect(screen.getByText('Bảo hành chính hãng 100%')).toBeInTheDocument()
  })

  // 1. Slider single banner & wrap-around
  it('handles slider single banner (hides controls) and wrap-around navigation cleanly', () => {
    // 1A. Wrap-around navigation with multiple banners in HomePage
    renderHomePage()

    const nextBtn = screen.getByTestId('slider-btn-next')
    const prevBtn = screen.getByTestId('slider-btn-prev')
    expect(nextBtn).toBeInTheDocument()
    expect(prevBtn).toBeInTheDocument()

    // Slide 0 initial
    expect(screen.getByRole('heading', { name: /Đại Tiệc Tivi QLED 4K Thế Hệ Mới/i })).toBeInTheDocument()

    // Slide 0 -> Slide 1
    fireEvent.click(nextBtn)
    expect(screen.getByRole('heading', { name: /Tủ Lạnh Side-By-Side Siêu Tiết Kiệm Điện/i })).toBeInTheDocument()

    // Slide 1 -> Slide 2 (last slide)
    fireEvent.click(nextBtn)
    expect(screen.getByRole('heading', { name: /Máy Giặt Cửa Ngang AI Khử Khuẩn 99.9%/i })).toBeInTheDocument()

    // Slide 2 -> wraps around back to Slide 0
    fireEvent.click(nextBtn)
    expect(screen.getByRole('heading', { name: /Đại Tiệc Tivi QLED 4K Thế Hệ Mới/i })).toBeInTheDocument()

    // Previous from Slide 0 -> wraps around to last slide (Slide 2)
    fireEvent.click(prevBtn)
    expect(screen.getByRole('heading', { name: /Máy Giặt Cửa Ngang AI Khử Khuẩn 99.9%/i })).toBeInTheDocument()

    // 1B. Single banner test: navigation buttons and dots should be hidden
    const singleBanner = [MOCK_HERO_BANNERS[0]]
    const { container } = render(
      <MemoryRouter>
        <HeroSection
          categories={MOCK_CATEGORY_MENU}
          banners={singleBanner}
          subBanners={MOCK_SUB_BANNERS}
        />
      </MemoryRouter>
    )

    expect(container.querySelector('[data-testid="slider-btn-prev"]')).toBeNull()
    expect(container.querySelector('[data-testid="slider-btn-next"]')).toBeNull()
    expect(container.querySelector('[data-testid="slider-dots"]')).toBeNull()
  })

  // 2. Category Empty State: Giả lập chọn tab rỗng, kiểm tra UI thông báo và nút click reset về tab 'all'
  it('handles category empty state and resets back to "all" on clicking reset button', () => {
    const tabsWithEmpty: CategoryTabItem[] = [
      { id: 'tab-all', name: 'Tất Cả Sản Phẩm', slug: 'all' },
      { id: 'tab-empty', name: 'Điện Thoại - Laptop', slug: 'dien-tu' },
    ]

    render(
      <MemoryRouter>
        <CategoryTabs tabs={tabsWithEmpty} products={MOCK_APPLIANCE_PRODUCTS} />
      </MemoryRouter>
    )

    // Initial state: shows all products
    expect(screen.getByText('Smart Tivi Neo QLED 4K 55 inch Samsung QA55QN85D')).toBeInTheDocument()
    expect(screen.queryByTestId('category-empty-state')).not.toBeInTheDocument()

    // Switch to empty category tab
    const emptyTab = screen.getByRole('tab', { name: 'Điện Thoại - Laptop' })
    fireEvent.click(emptyTab)

    // Empty state container should appear
    const emptyState = screen.getByTestId('category-empty-state')
    expect(emptyState).toBeInTheDocument()
    expect(screen.getByText('Hiện chưa có sản phẩm nào trong danh mục này.')).toBeInTheDocument()

    // Click "Xem tất cả sản phẩm" button to reset to 'all'
    const resetBtn = screen.getByTestId('category-empty-reset-btn')
    expect(resetBtn).toBeInTheDocument()
    fireEvent.click(resetBtn)

    // Should return to 'all' and display products again
    expect(screen.queryByTestId('category-empty-state')).not.toBeInTheDocument()
    expect(screen.getByText('Smart Tivi Neo QLED 4K 55 inch Samsung QA55QN85D')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Tất Cả Sản Phẩm' })).toHaveClass('active')
  })

  // 3. Timer Stop: Giả lập thời gian hết hạn (diff <= 0), kiểm tra hiển thị đúng 00:00:00
  it('stops countdown at exactly 00:00:00 without negative numbers when expired', () => {
    // Simulate expired countdown by passing initialTargetTime in the past
    const expiredTimestamp = Date.now() - 5000

    render(
      <MemoryRouter>
        <FlashSaleCountdown
          products={MOCK_APPLIANCE_PRODUCTS}
          initialTargetTime={expiredTimestamp}
        />
      </MemoryRouter>
    )

    const hours = screen.getByTestId('countdown-hours')
    const minutes = screen.getByTestId('countdown-minutes')
    const seconds = screen.getByTestId('countdown-seconds')
    const clock = screen.getByTestId('flash-sale-clock')

    expect(hours.textContent).toBe('00')
    expect(minutes.textContent).toBe('00')
    expect(seconds.textContent).toBe('00')
    expect(clock.textContent).toBe('00:00:00')
    expect(screen.getByText('Đã kết thúc')).toBeInTheDocument()
  })

  // 4. Image Error Fallback: Giả lập sự kiện fireEvent.error trên ảnh sản phẩm và kiểm tra fallback UI xuất hiện
  it('displays fallback placeholder when product image triggers onError event', () => {
    render(
      <MemoryRouter>
        <ApplianceProductCard product={MOCK_APPLIANCE_PRODUCTS[0]} />
      </MemoryRouter>
    )

    const img = screen.getByRole('img', { name: MOCK_APPLIANCE_PRODUCTS[0].name })
    expect(img).toBeInTheDocument()
    expect(screen.queryByTestId('appliance-card-placeholder')).not.toBeInTheDocument()

    // Simulate 404 or image loading error
    fireEvent.error(img)

    // Image is removed and fallback placeholder appears
    expect(screen.queryByRole('img', { name: MOCK_APPLIANCE_PRODUCTS[0].name })).not.toBeInTheDocument()
    const placeholder = screen.getByTestId('appliance-card-placeholder')
    expect(placeholder).toBeInTheDocument()
    expect(within(placeholder).getByText('Hình ảnh thiết bị')).toBeInTheDocument()
  })

  // 5. Interactive 3D Showcase & Mode Switcher
  it('renders interactive 3D showroom toggle and handles mode switching', async () => {
    renderHomePage()

    // Mode toggle button exists
    const toggleBtn = screen.getByRole('button', { name: /bật tắt chế độ 3d showroom/i })
    expect(toggleBtn).toBeInTheDocument()
    expect(toggleBtn).toHaveClass('active')

    // 3D showroom container is lazy loaded with Suspense
    const showcase = await screen.findByTestId('hero-3d-showcase', {}, { timeout: 5000 })
    expect(showcase).toBeInTheDocument()

    // Toggle to 2D standard banner mode
    fireEvent.click(toggleBtn)
    expect(toggleBtn).not.toHaveClass('active')
    expect(screen.getByText('Ảnh Banner Tiêu Chuẩn')).toBeInTheDocument()
    expect(screen.queryByTestId('hero-3d-showcase')).not.toBeInTheDocument()

    // Toggle back to 3D mode
    fireEvent.click(toggleBtn)
    expect(toggleBtn).toHaveClass('active')
    expect(await screen.findByTestId('hero-3d-showcase', {}, { timeout: 5000 })).toBeInTheDocument()
  })
})

