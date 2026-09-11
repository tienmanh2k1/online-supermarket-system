import { useState, useEffect, useRef, useMemo } from 'react'
import gsap from 'gsap'
import type { ApplianceProduct } from '../types'
import { ApplianceProductCard } from './ApplianceProductCard'

export interface FlashSaleCountdownProps {
  products: ApplianceProduct[]
  targetHoursAhead?: number
  initialTargetTime?: number
}

interface TimeLeft {
  hours: number
  minutes: number
  seconds: number
  isExpired: boolean
}

function calculateTimeLeft(target: number): TimeLeft {
  const diff = target - Date.now()
  if (diff <= 0) {
    return { hours: 0, minutes: 0, seconds: 0, isExpired: true }
  }
  const hours = Math.floor(diff / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))
  const seconds = Math.floor((diff % (1000 * 60)) / 1000)
  return { hours, minutes, seconds, isExpired: false }
}

export function FlashSaleCountdown({
  products,
  targetHoursAhead = 6,
  initialTargetTime,
}: FlashSaleCountdownProps) {
  const targetTimeRef = useRef<number>(
    initialTargetTime ?? Date.now() + targetHoursAhead * 60 * 60 * 1000
  )

  const [timeLeft, setTimeLeft] = useState<TimeLeft>(() =>
    calculateTimeLeft(targetTimeRef.current)
  )

  const sectionRef = useRef<HTMLElement>(null)
  const hoursRef = useRef<HTMLSpanElement>(null)
  const minutesRef = useRef<HTMLSpanElement>(null)
  const secondsRef = useRef<HTMLSpanElement>(null)
  const prevSeconds = useRef<number>(timeLeft.seconds)

  useEffect(() => {
    if (calculateTimeLeft(targetTimeRef.current).isExpired) {
      setTimeLeft({ hours: 0, minutes: 0, seconds: 0, isExpired: true })
      return
    }

    const interval = setInterval(() => {
      const remaining = calculateTimeLeft(targetTimeRef.current)
      setTimeLeft(remaining)
      if (remaining.isExpired) {
        clearInterval(interval)
      }
    }, 1000)

    return () => clearInterval(interval)
  }, [])

  // Minimal GSAP subtle tick without infinite loop or flame flickering
  const isTestEnv = import.meta.env.MODE === 'test'
  const reduceMotion =
    isTestEnv ||
    (typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches)

  useEffect(() => {
    if (!reduceMotion && prevSeconds.current !== timeLeft.seconds && secondsRef.current) {
      prevSeconds.current = timeLeft.seconds
      gsap.fromTo(secondsRef.current, { y: -4 }, { y: 0, duration: 0.18, ease: 'power1.out' })
    }
  }, [timeLeft.seconds, reduceMotion])

  const pad = (n: number) => String(n).padStart(2, '0')

  const hotDeals = useMemo(
    () => products.filter((p) => p.isHotDeal),
    [products]
  )

  return (
    <section
      ref={sectionRef}
      className="flash-sale-section bg-white rounded-2xl border border-slate-200 shadow-sm"
      data-testid="home-bestsellers"
      aria-label="Sản phẩm bán chạy"
    >
      <div className="flash-sale-header">
        <div className="flash-sale-header__left">
          <div className="flash-sale-header__badge">
            <span aria-hidden="true">⚡</span>
            <span>SẢN PHẨM BÁN CHẠY</span>
          </div>
          <h2 className="flash-sale-header__title">Top Thiết Bị Điện Máy Bán Chạy Nhất</h2>
          <p className="flash-sale-header__subtitle">
            Cam kết 100% chính hãng AptechMart • Giao siêu tốc 2h • Lắp đặt &amp; Bảo hành tận nhà
          </p>
        </div>

        {/* Live Countdown Clock */}
        <div className="flash-sale-countdown kg-countdown-wrap" data-testid="flash-sale-countdown">
          <span className="flash-sale-countdown__label">
            {timeLeft.isExpired ? 'Đã kết thúc' : 'Ưu đãi giờ vàng kết thúc sau:'}
          </span>
          <div
            className="flash-sale-clock kg-countdown-boxes"
            data-testid="flash-sale-clock"
            aria-label="Thời gian còn lại"
          >
            <span
              ref={hoursRef}
              className="flash-sale-box"
              data-testid="countdown-hours"
            >
              {pad(timeLeft.hours)}
            </span>
            <span className="flash-sale-colon">:</span>
            <span
              ref={minutesRef}
              className="flash-sale-box"
              data-testid="countdown-minutes"
            >
              {pad(timeLeft.minutes)}
            </span>
            <span className="flash-sale-colon">:</span>
            <span
              ref={secondsRef}
              className="flash-sale-box"
              data-testid="countdown-seconds"
            >
              {pad(timeLeft.seconds)}
            </span>
          </div>
        </div>
      </div>

      <div className="flash-sale-grid">
        {hotDeals.slice(0, 4).map((product) => (
          <ApplianceProductCard key={product.id} product={product} />
        ))}
      </div>
    </section>
  )
}
