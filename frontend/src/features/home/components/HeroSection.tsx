import { useState, useEffect, useCallback, useRef, lazy, Suspense } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import gsap from 'gsap'
import { useGSAP } from '@gsap/react'
import type { CategoryMenuItem, HeroBanner, SubBanner } from '../types'

// Register GSAP plugins
gsap.registerPlugin(useGSAP)

// Lazy load 3D Showroom to prevent blocking First Contentful Paint (FCP)
const Hero3DShowcase = lazy(() =>
  import('./Hero3DShowcase').then((m) => ({ default: m.Hero3DShowcase }))
)

export interface HeroSectionProps {
  categories: CategoryMenuItem[]
  banners: HeroBanner[]
  subBanners: [SubBanner, SubBanner]
  autoPlayIntervalMs?: number
}

export function HeroSection({
  categories,
  banners,
  subBanners,
  autoPlayIntervalMs = 5000,
}: HeroSectionProps) {
  const navigate = useNavigate()
  const [currentBannerIndex, setCurrentBannerIndex] = useState(0)
  const [hoveredCategory, setHoveredCategory] = useState<string | null>(null)
  const [isPaused, setIsPaused] = useState(false)
  const [searchTerm, setSearchTerm] = useState('')
  const [show3DMode, setShow3DMode] = useState(true)

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const textOverlayRef = useRef<HTMLDivElement>(null)

  const bannerCount = banners.length

  const handleNext = useCallback(() => {
    if (bannerCount <= 1) return
    setCurrentBannerIndex((prev) => (prev + 1) % bannerCount)
  }, [bannerCount])

  const handlePrev = useCallback(() => {
    if (bannerCount <= 1) return
    setCurrentBannerIndex((prev) => (prev === 0 ? bannerCount - 1 : prev - 1))
  }, [bannerCount])

  const handleSelectDot = useCallback(
    (index: number) => {
      if (bannerCount <= 1) return
      setCurrentBannerIndex(index)
    },
    [bannerCount]
  )

  // Autoplay timer
  useEffect(() => {
    if (bannerCount <= 1 || isPaused) {
      if (timerRef.current) clearInterval(timerRef.current)
      return
    }

    if (timerRef.current) clearInterval(timerRef.current)

    timerRef.current = setInterval(() => {
      setCurrentBannerIndex((prev) => (prev + 1) % bannerCount)
    }, autoPlayIntervalMs)

    return () => {
      if (timerRef.current) clearInterval(timerRef.current)
    }
  }, [bannerCount, isPaused, currentBannerIndex, autoPlayIntervalMs])

  // GSAP Timeline animation on slide text transitions
  useGSAP(
    () => {
      if (!textOverlayRef.current) return

      const tl = gsap.timeline({ defaults: { ease: 'power3.out' } })

      tl.fromTo(
        '.appliance-slider-tagline',
        { opacity: 0, y: -12 },
        { opacity: 1, y: 0, duration: 0.4 }
      )
        .fromTo(
          '.appliance-slider-title',
          { opacity: 0, y: 16, filter: 'blur(4px)' },
          { opacity: 1, y: 0, filter: 'blur(0px)', duration: 0.5 },
          '-=0.2'
        )
        .fromTo(
          '.appliance-slider-subtitle',
          { opacity: 0, y: 10 },
          { opacity: 1, y: 0, duration: 0.4 },
          '-=0.25'
        )
        .fromTo(
          '.appliance-slider-cta',
          { opacity: 0, scale: 0.92, y: 8 },
          { opacity: 1, scale: 1, y: 0, duration: 0.45, ease: 'back.out(1.7)' },
          '-=0.15'
        )
    },
    { dependencies: [currentBannerIndex], scope: textOverlayRef }
  )

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault()
    const term = searchTerm.trim()
    navigate(term ? `/browse?search=${encodeURIComponent(term)}` : '/browse')
  }

  const activeBanner = banners[currentBannerIndex] || banners[0]

  return (
    <section className="appliance-hero" data-testid="home-hero" aria-label="Khám phá siêu thị điện máy">
      {/* Top Search & Highlights Bar */}
      <div className="appliance-hero__top-bar">
        <div className="appliance-hero__brand-tag">
          <span className="appliance-hero__brand-dot" />
          <span className="home-hero__title">Siêu Thị Điện Máy AptechMart — Chính Hãng &amp; Giá Kho</span>
        </div>

        <form className="home-hero__search-form appliance-search-form" onSubmit={handleSearchSubmit} role="search">
          <div className="home-hero__search-input-wrap">
            <span className="home-hero__search-icon" aria-hidden="true">
              🔍
            </span>
            <input
              type="text"
              id="home-search-input"
              className="home-hero__search-input"
              placeholder="Tìm Tivi QLED, Tủ lạnh Side-by-side, Máy giặt Inverter..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              aria-label="Tìm kiếm sản phẩm điện máy"
            />
          </div>
          <button type="submit" className="home-hero__search-submit">
            Tìm kiếm
          </button>
        </form>
      </div>

      {/* 3-Column Standard Retail Grid */}
      <div className="appliance-hero__grid">
        {/* Column 1: Mega-Menu (~22%) */}
        <aside className="appliance-mega-menu" aria-label="Danh mục ngành hàng điện máy">
          <div className="appliance-mega-menu__header">
            <span className="appliance-mega-menu__icon">☰</span>
            <span className="appliance-mega-menu__title">DANH MỤC ĐIỆN MÁY</span>
          </div>

          <ul className="appliance-mega-menu__list" role="menu">
            {categories.map((cat) => (
              <li
                key={cat.id}
                className={`appliance-mega-menu__item ${hoveredCategory === cat.id ? 'active' : ''}`}
                onMouseEnter={() => setHoveredCategory(cat.id)}
                onMouseLeave={() => setHoveredCategory(null)}
                role="none"
              >
                <Link
                  to={`/browse?categoryId=${encodeURIComponent(cat.slug)}`}
                  className="appliance-mega-menu__link"
                  role="menuitem"
                >
                  <span className="appliance-mega-menu__item-icon" aria-hidden="true">
                    {cat.icon}
                  </span>
                  <span className="appliance-mega-menu__item-name">{cat.name}</span>
                  {cat.badgeText && (
                    <span className="appliance-mega-menu__item-badge">{cat.badgeText}</span>
                  )}
                  <span className="appliance-mega-menu__item-arrow" aria-hidden="true">
                    ›
                  </span>
                </Link>

                {/* Subcategory Flyout preview */}
                {cat.subcategories && cat.subcategories.length > 0 && hoveredCategory === cat.id && (
                  <div className="appliance-mega-menu__flyout" role="menu">
                    <div className="appliance-mega-menu__flyout-title">{cat.name} nổi bật:</div>
                    <div className="appliance-mega-menu__flyout-grid">
                      {cat.subcategories.map((sub, idx) => (
                        <Link
                          key={idx}
                          to={`/browse?search=${encodeURIComponent(sub)}`}
                          className="appliance-mega-menu__flyout-item"
                          role="menuitem"
                        >
                          {sub}
                        </Link>
                      ))}
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </aside>

        {/* Column 2: Main Banner Slider (16:9 Aspect Ratio) with Interactive 3D Canvas Showcase */}
        <div
          className="appliance-slider-container"
          data-testid="hero-main-slider"
          onMouseEnter={() => setIsPaused(true)}
          onMouseLeave={() => setIsPaused(false)}
        >
          {/* Mode Switcher Badge (3D Showroom vs 2D Banner) */}
          <div className="hero-mode-toggle-bar">
            <button
              type="button"
              className={`hero-mode-toggle-btn ${show3DMode ? 'active' : ''}`}
              onClick={() => setShow3DMode(!show3DMode)}
              aria-label="Bật tắt chế độ 3D showroom"
              title="Chuyển đổi trải nghiệm 3D / 2D Banner"
            >
              <span className="hero-mode-dot" />
              {show3DMode ? '3D Showroom Kéo Xoay' : 'Ảnh Banner Tiêu Chuẩn'}
            </button>
          </div>

          <div className="appliance-slider-frame">
            {activeBanner && (
              <div className="appliance-slider-slide">
                {/* 2D Backdrop image (Always in DOM for SEO, fallback, and existing tests) */}
                <img
                  src={activeBanner.imageUrl}
                  alt={activeBanner.alt}
                  className="appliance-slider-image"
                  loading="eager"
                  style={{
                    opacity: show3DMode ? 0.35 : 1,
                    filter: show3DMode ? 'brightness(0.6) saturate(0.8)' : 'none',
                    transition: 'opacity 0.4s ease, filter 0.4s ease',
                  }}
                />

                {/* Interactive 3D Model Stage Canvas (Lazy Loaded) */}
                {show3DMode && (
                  <div className="appliance-slider-3d-stage">
                    <Suspense
                      fallback={
                        <div className="hero-3d-skeleton">
                          <span className="hero-3d-spinner" />
                          <small>Đang tải 3D Showcase...</small>
                        </div>
                      }
                    >
                      <Hero3DShowcase />
                    </Suspense>
                  </div>
                )}

                {/* GSAP-Animated Banner Overlay Content */}
                <div ref={textOverlayRef} className="appliance-slider-overlay">
                  {activeBanner.tagline && (
                    <span className="appliance-slider-tagline">{activeBanner.tagline}</span>
                  )}
                  <h2 className="appliance-slider-title">{activeBanner.title}</h2>
                  <p className="appliance-slider-subtitle">{activeBanner.subtitle}</p>
                  <Link to={activeBanner.linkUrl} className="appliance-slider-cta">
                    Khám phá ưu đãi &rarr;
                  </Link>
                </div>
              </div>
            )}
          </div>

          {/* Controls: Hidden if single banner */}
          {bannerCount > 1 && (
            <>
              <button
                type="button"
                className="appliance-slider-btn appliance-slider-btn--prev"
                onClick={handlePrev}
                aria-label="Banner trước"
                data-testid="slider-btn-prev"
              >
                &#10094;
              </button>
              <button
                type="button"
                className="appliance-slider-btn appliance-slider-btn--next"
                onClick={handleNext}
                aria-label="Banner kế tiếp"
                data-testid="slider-btn-next"
              >
                &#10095;
              </button>

              <div className="appliance-slider-dots" data-testid="slider-dots">
                {banners.map((b, idx) => (
                  <button
                    key={b.id}
                    type="button"
                    className={`appliance-slider-dot ${idx === currentBannerIndex ? 'active' : ''}`}
                    onClick={() => handleSelectDot(idx)}
                    aria-label={`Chuyển tới banner ${idx + 1}`}
                  />
                ))}
              </div>
            </>
          )}
        </div>

        {/* Column 3: 2 Fixed Vertical Sub-Banners (4:3 Aspect Ratio) */}
        <aside className="appliance-sub-banners" aria-label="Ưu đãi dịch vụ đặc quyền">
          {subBanners.map((sub) => (
            <Link
              key={sub.id}
              to={sub.linkUrl}
              className="appliance-sub-banner-card"
              title={sub.title}
              data-testid="sub-banner-container"
            >
              <div className="appliance-sub-banner-frame">
                <img
                  src={sub.imageUrl}
                  alt={sub.alt}
                  className="appliance-sub-banner-image"
                  loading="lazy"
                />
                <div className="appliance-sub-banner-overlay">
                  <span className="appliance-sub-banner-badge">{sub.badge}</span>
                  <strong className="appliance-sub-banner-title">{sub.title}</strong>
                  <span className="appliance-sub-banner-highlight">{sub.highlight}</span>
                </div>
              </div>
            </Link>
          ))}
        </aside>
      </div>
    </section>
  )
}
