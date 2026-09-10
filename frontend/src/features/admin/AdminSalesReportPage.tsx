import { useEffect, useState, useCallback, useRef } from 'react'
import { adminApi, type SalesReportDto } from '../../api/adminApi'
import { useAuth } from '../auth/AuthContext'
import './AdminSalesReportPage.css'

function formatVnd(amount: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount)
}

function formatUtcDate(date: Date): string {
  const y = date.getUTCFullYear()
  const m = String(date.getUTCMonth() + 1).padStart(2, '0')
  const d = String(date.getUTCDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

function get30DaysRange() {
  const now = new Date()
  const to = formatUtcDate(now)
  const fromDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - 29))
  const from = formatUtcDate(fromDate)
  return { from, to }
}

function get7DaysRange() {
  const now = new Date()
  const to = formatUtcDate(now)
  const fromDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - 6))
  const from = formatUtcDate(fromDate)
  return { from, to }
}

function getThisMonthRange() {
  const now = new Date()
  const to = formatUtcDate(now)
  const fromDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
  const from = formatUtcDate(fromDate)
  return { from, to }
}

type ReportState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: SalesReportDto }
  | { kind: 'error'; message: string }

export function AdminSalesReportPage() {
  const { accessToken } = useAuth()
  const initialRange = get30DaysRange()
  const [fromInput, setFromInput] = useState(initialRange.from)
  const [toInput, setToInput] = useState(initialRange.to)
  const [activePreset, setActivePreset] = useState<'30' | '7' | 'month' | 'custom'>('30')
  const [validationError, setValidationError] = useState<string | null>(null)

  const [state, setState] = useState<ReportState>({ kind: 'loading' })
  const abortControllerRef = useRef<AbortController | null>(null)

  const fetchReport = useCallback(
    async (from: string, to: string) => {
      if (!accessToken) return

      // Client-side validation
      if (!from || !to) {
        setValidationError('Vui lòng nhập đầy đủ ngày bắt đầu và ngày kết thúc.')
        return
      }

      if (from > to) {
        setValidationError('Ngày bắt đầu không được lớn hơn ngày kết thúc.')
        return
      }

      // Check 366 day limit
      const fromDate = new Date(from + 'T00:00:00Z')
      const toDate = new Date(to + 'T00:00:00Z')
      const dayDiff = Math.floor((toDate.getTime() - fromDate.getTime()) / (1000 * 60 * 60 * 24)) + 1
      if (dayDiff > 366) {
        setValidationError('Khoảng ngày tối đa là 366 ngày.')
        return
      }

      // Abort any in-flight request before starting new one
      if (abortControllerRef.current) {
        abortControllerRef.current.abort()
      }

      setValidationError(null)
      setState({ kind: 'loading' })
      const controller = new AbortController()
      abortControllerRef.current = controller

      try {
        const report = await adminApi.getSalesReport(from, to, accessToken, controller.signal)
        // Ignore stale responses
        if (controller.signal.aborted) return
        setState({ kind: 'ready', data: report })
      } catch (err: any) {
        // Ignore aborted requests
        if (controller.signal.aborted) return
        // Read detail from ProblemDetails if available (ApiError stores body in .data)
        const detail = err?.data?.detail || err?.data?.title || err?.message
        setState({ kind: 'error', message: detail || 'Không thể tải báo cáo doanh số.' })
      }
    },
    [accessToken]
  )

  useEffect(() => {
    fetchReport(fromInput, toInput)
    return () => abortControllerRef.current?.abort()
  }, [fetchReport])

  const handleApplyPreset = (type: '30' | '7' | 'month') => {
    setActivePreset(type)
    let range: { from: string; to: string }
    if (type === '7') {
      range = get7DaysRange()
    } else if (type === 'month') {
      range = getThisMonthRange()
    } else {
      range = get30DaysRange()
    }
    setFromInput(range.from)
    setToInput(range.to)
    fetchReport(range.from, range.to)
  }

  const handleCustomSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setActivePreset('custom')
    fetchReport(fromInput, toInput)
  }

  return (
    <div className="admin-sales-report" role="region" aria-label="Báo cáo doanh số">
      <div className="admin-sales-report__header">
        <div>
          <h1 className="admin-sales-report__title">Báo cáo doanh số</h1>
        </div>
        <div className="admin-sales-report__timezone-note">
          <span>🕒 Nhóm theo ngày tạo đơn (UTC)</span>
        </div>
      </div>

      <div className="admin-sales-report__filters">
        <div className="admin-sales-report__filter-row">
          <div className="admin-sales-report__presets">
            <button
              type="button"
              className={`admin-sales-report__preset-btn ${activePreset === '7' ? 'admin-sales-report__preset-btn--active' : ''}`}
              onClick={() => handleApplyPreset('7')}
            >
              7 ngày
            </button>
            <button
              type="button"
              className={`admin-sales-report__preset-btn ${activePreset === '30' ? 'admin-sales-report__preset-btn--active' : ''}`}
              onClick={() => handleApplyPreset('30')}
            >
              30 ngày
            </button>
            <button
              type="button"
              className={`admin-sales-report__preset-btn ${activePreset === 'month' ? 'admin-sales-report__preset-btn--active' : ''}`}
              onClick={() => handleApplyPreset('month')}
            >
              Tháng này
            </button>
          </div>

          <form className="admin-sales-report__custom-range" onSubmit={handleCustomSubmit}>
            <div className="admin-sales-report__date-field">
              <label htmlFor="sales-from">Từ ngày</label>
              <input
                id="sales-from"
                type="date"
                className="admin-sales-report__date-input"
                value={fromInput}
                onChange={(e) => {
                  setFromInput(e.target.value)
                  setActivePreset('custom')
                  setValidationError(null)
                }}
              />
            </div>

            <div className="admin-sales-report__date-field">
              <label htmlFor="sales-to">Đến ngày</label>
              <input
                id="sales-to"
                type="date"
                className="admin-sales-report__date-input"
                value={toInput}
                onChange={(e) => {
                  setToInput(e.target.value)
                  setActivePreset('custom')
                  setValidationError(null)
                }}
              />
            </div>

            <button type="submit" className="admin-sales-report__submit-btn">
              Xem báo cáo
            </button>
          </form>
        </div>

        {validationError && (
          <div className="auth-form__alert auth-form__alert--error" role="alert">
            {validationError}
          </div>
        )}
      </div>

      {state.kind === 'loading' && (
        <div className="admin-sales-report__loading">
          <span>Đang tải dữ liệu báo cáo...</span>
        </div>
      )}

      {state.kind === 'error' && (
        <div className="admin-sales-report__error" role="alert">
          <span>{state.message}</span>
          <button
            type="button"
            className="admin-sales-report__retry-btn"
            onClick={() => fetchReport(fromInput, toInput)}
          >
            Thử lại
          </button>
        </div>
      )}

      {state.kind === 'ready' && (
        <>
          <div className="admin-sales-report__kpis">
            <div className="admin-sales-report__kpi-card">
              <span className="admin-sales-report__kpi-label">Tổng doanh thu</span>
              <span className="admin-sales-report__kpi-value admin-sales-report__kpi-value--revenue">
                {formatVnd(state.data.totalRevenue)}
              </span>
            </div>

            <div className="admin-sales-report__kpi-card">
              <span className="admin-sales-report__kpi-label">Số đơn hoàn tất</span>
              <span className="admin-sales-report__kpi-value">
                {state.data.completedOrderCount}
              </span>
            </div>

            <div className="admin-sales-report__kpi-card">
              <span className="admin-sales-report__kpi-label">Giá trị đơn trung bình</span>
              <span className="admin-sales-report__kpi-value">
                {formatVnd(state.data.averageOrderValue)}
              </span>
            </div>
          </div>

          <section className="admin-sales-report__section">
            <div className="admin-sales-report__section-header">
              <h2 className="admin-sales-report__section-title">Doanh thu theo ngày</h2>
              <span className="admin-sales-report__count-badge">
                {state.data.completedOrderCount} đơn hàng hoàn tất
              </span>
            </div>

            <table className="admin-sales-report__table">
              <thead>
                <tr>
                  <th>Ngày</th>
                  <th>Số đơn hoàn tất</th>
                  <th>Doanh thu</th>
                </tr>
              </thead>
              <tbody>
                {state.data.daily.map((day) => (
                  <tr key={day.date}>
                    <td>{day.date}</td>
                    <td>{day.orderCount}</td>
                    <td>{formatVnd(day.revenue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </>
      )}
    </div>
  )
}
