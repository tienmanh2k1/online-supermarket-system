import { useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { HeroSection } from './components/HeroSection'
import { QuickCategoryStrip } from './components/QuickCategoryStrip'
import { TrustBadges } from './components/TrustBadges'
import { FlashSaleCountdown } from './components/FlashSaleCountdown'
import { CategoryTabs } from './components/CategoryTabs'
import { RecommendationShelfLoader } from '../recommendations/RecommendationShelfLoader'
import {
  MOCK_CATEGORY_MENU,
  MOCK_HERO_BANNERS,
  MOCK_SUB_BANNERS,
  MOCK_TRUST_BADGES,
  MOCK_CATEGORY_TABS,
  MOCK_APPLIANCE_PRODUCTS,
} from './mockData'
import './HomePage.css'

export function HomePage() {
  const location = useLocation()

  // Smooth scroll to #roadmap if hash is present
  useEffect(() => {
    if (location.hash === '#roadmap' || window.location.hash === '#roadmap') {
      const timer = setTimeout(() => {
        const el = document.getElementById('roadmap')
        if (el) {
          el.scrollIntoView({ behavior: 'smooth' })
        }
      }, 100)
      return () => clearTimeout(timer)
    }
  }, [location])

  return (
    <div className="home-page appliance-home-page" data-testid="home-page">
      {/* 1. Standard Retail Grid Hero: Mega-Menu (22%) | 16:9 Main Slider (53%) | 4:3 Sub-Banners (25%) */}
      <HeroSection
        categories={MOCK_CATEGORY_MENU}
        banners={MOCK_HERO_BANNERS}
        subBanners={MOCK_SUB_BANNERS}
      />

      {/* 2. Kangaroo 6-Category Quick Navigation Strip */}
      <QuickCategoryStrip />

      {/* 3. 4 Service Trust Badges (Giao 2h, Lắp đặt miễn phí, Đổi mới 30 ngày, Bảo hành chính hãng) */}
      <TrustBadges badges={MOCK_TRUST_BADGES} />

      {/* 3. Flash Sale Countdown with Heat-progress */}
      <FlashSaleCountdown products={MOCK_APPLIANCE_PRODUCTS} />

      {/* 4. Category Tabs Showcase (Tivi, Tủ lạnh, Máy giặt, Điều hòa, Gia dụng) */}
      <CategoryTabs tabs={MOCK_CATEGORY_TABS} products={MOCK_APPLIANCE_PRODUCTS} />

      {/* 5. AI Recommendations Shelf (Personalized via ML.NET) */}
      <section
        className="home-recommendations-section"
        data-testid="home-recommendations"
        aria-label="Gợi ý sản phẩm thông minh"
      >
        <RecommendationShelfLoader limit={8} title="Gợi ý thiết bị điện máy phù hợp cho bạn" />
      </section>

      {/* 6. Branch Network Teaser */}
      <section
        className="home-branch-teaser"
        data-testid="home-branches"
        aria-label="Hệ thống siêu thị điện máy"
      >
        <div className="home-branch-teaser__inner">
          <div className="home-branch-teaser__badge">HỆ THỐNG ĐA CHI NHÁNH &amp; KHO HÀNG</div>
          <h2 className="home-branch-teaser__title">
            Trải Nghiệm Trực Tiếp Tại Trung Tâm Điện Máy AptechMart
          </h2>
          <p className="home-branch-teaser__desc">
            Hơn 15 chi nhánh phủ sóng toàn quốc. Đến trải nghiệm tận mắt công nghệ màn hình OLED, 
            nghe thử âm thanh Dolby Atmos và nhận tư vấn kỹ thuật chuyên sâu từ các chuyên gia.
          </p>
          <div className="home-branch-teaser__actions">
            <Link to="/branches" className="home-branch-teaser__btn">
              Tìm siêu thị gần bạn
            </Link>
          </div>
        </div>
      </section>

      {/* 7. Technology Roadmap Section (#roadmap) */}
      <section
        id="roadmap"
        className="home-roadmap"
        data-testid="home-roadmap"
        aria-labelledby="roadmap-heading"
      >
        <div className="home-section-header home-section-header--center">
          <h2 id="roadmap-heading" className="home-section-title">
            Lộ Trình Phát Triển Dịch Vụ Điện Máy &amp; Công Nghệ
          </h2>
          <p className="home-section-desc">
            Cam kết chuẩn mực dịch vụ hậu mãi và số hóa toàn diện hệ thống bán lẻ điện máy
          </p>
        </div>

        <div className="home-roadmap__grid">
          <div className="home-roadmap__card home-roadmap__card--done">
            <div className="home-roadmap__status">Giai đoạn 1 • Đã hoàn thành</div>
            <h3 className="home-roadmap__card-title">Số Hóa Bán Lẻ &amp; Đặt Hàng Trực Tuyến</h3>
            <p className="home-roadmap__card-desc">
              Đồng bộ tồn kho đa chi nhánh thời gian thực và hỗ trợ thanh toán trực tuyến an toàn.
            </p>
          </div>

          <div className="home-roadmap__card home-roadmap__card--active">
            <div className="home-roadmap__status">Giai đoạn 2 • Đang vận hành</div>
            <h3 className="home-roadmap__card-title">AI Gợi Ý &amp; Dự Báo Nhu Cầu Tiêu Thụ</h3>
            <p className="home-roadmap__card-desc">
              Tích hợp AI gợi ý thiết bị tương thích diện tích phòng và dự báo nhu cầu tiêu thụ.
            </p>
          </div>

          <div className="home-roadmap__card">
            <div className="home-roadmap__status">Giai đoạn 3 • Đang mở rộng</div>
            <h3 className="home-roadmap__card-title">Giao Lắp Siêu Tốc 60 Phút &amp; Bảo Hành Tận Nhà</h3>
            <p className="home-roadmap__card-desc">
              Điều phối kỹ thuật viên thông minh, giao lắp trong 60 phút và xử lý bảo hành tận nhà.
            </p>
          </div>
        </div>
      </section>
    </div>
  )
}
