import { useState, useEffect, useCallback, useRef, lazy, Suspense } from 'react'
import { Link } from 'react-router-dom'
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
  const [currentBannerIndex, setCurrentBannerIndex] = useState(0)
  const [isPaused, setIsPaused] = useState(false)
  const [show3DMode, setShow3DMode] = useState(true)

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const textOverlayRef = useRef<HTMLDivElement>(null)

  const bannerCount = banners.length
  const activeBanner = bannerCount > 0 ? (banners[currentBannerIndex] || banners[0]) : null

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

  // Autoplay timer with pause on hover/focus
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

  // GSAP animation for slide text transitions with reduced motion and test environment check
  const isTestEnv = import.meta.env.MODE === 'test'
  const reduceMotion =
    isTestEnv ||
    (typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches)

  useGSAP(
    () => {
      if (!textOverlayRef.current || reduceMotion) return
      gsap.fromTo(
        textOverlayRef.current.children,
        { autoAlpha: 0, y: 14 },
        { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.07, ease: 'power2.out' }
      )
    },
    { dependencies: [currentBannerIndex], scope: textOverlayRef }
  )

  return (
    <section className="appliance-hero" data-testid="home-hero" aria-label="Khám phá siêu thị điện máy">
      {/* Category Navigation Bar (Compact) */}
      <nav className="appliance-hero__categories" aria-label="Ngành hàng nổi bật">
        {categories.slice(0, 6).map((category) => (
          <Link
            key={category.id}
            to={`/browse?categoryId=${encodeURIComponent(category.slug)}`}
            className="appliance-hero__category-link"
          >
            <span aria-hidden="true">{category.icon}</span>
            <span>{category.name}</span>
          </Link>
        ))}
      </nav>

      {/* 2-Column Campaign Board Grid: Main Promotion (2fr) | 2 Sub-Banners (0.72fr) */}
      <div className="appliance-hero__grid">
        {/* Column 1: Main Promotion Slider */}
        <div
          className="appliance-slider-container"
          data-testid="hero-main-slider"
          onMouseEnter={() => setIsPaused(true)}
          onMouseLeave={() => setIsPaused(false)}
          onFocusCapture={() => setIsPaused(true)}
          onBlurCapture={() => setIsPaused(false)}
        >
          {/* Mode Switcher Badge (3D Showroom vs 2D Banner) */}
          {activeBanner && (
            <div className="hero-mode-toggle-bar">
              <button
                type="button"
                className={`hero-mode-toggle-btn ${show3DMode ? 'active' : ''}`}
                onClick={() => setShow3DMode(!show3DMode)}
                aria-label="Bật tắt chế độ 3d showroom"
                title="Chuyển đổi trải nghiệm 3D / 2D Banner"
              >
                <span className="hero-mode-dot" />
                {show3DMode ? '3D Showroom Kéo Xoay' : 'Ảnh Banner Tiêu Chuẩn'}
              </button>
            </div>
          )}

          <div className="appliance-slider-frame" aria-live="polite">
            {activeBanner ? (
              <div className="appliance-slider-slide">
                {/* 2D Backdrop image */}
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
            ) : (
              <div className="appliance-slider-slide appliance-slider-slide--fallback">
                <div className="appliance-slider-overlay appliance-slider-overlay--fallback">
                  <span className="appliance-slider-tagline">AptechMart Siêu Thị Điện Máy</span>
                  <h2 className="appliance-slider-title">Khám phá thiết bị cho ngôi nhà hiện đại</h2>
                  <p className="appliance-slider-subtitle">
                    Trải nghiệm mua sắm thiết bị điện máy chính hãng với ngàn ưu đãi hấp dẫn.
                  </p>
                  <Link to="/browse" className="appliance-slider-cta">
                    Xem tất cả sản phẩm &rarr;
                  </Link>
                </div>
              </div>
            )}
          </div>

          {/* Controls: Hidden if single banner or no banners */}
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

        {/* Column 2: 2 Fixed Vertical Sub-Banners (4:3 Aspect Ratio) */}
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
