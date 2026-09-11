import { useEffect, useRef, useState } from 'react'
import { branchApi, type BranchDto } from '../../api/branchApi'
import {
  inventoryIntelligenceApi,
  type ForecastDto,
  type ForecastJobRunDto,
} from '../../api/inventoryIntelligenceApi'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../../api/httpClient'
import { AdminCard, AdminBadge, AdminEmptyState } from './components'
import './AdminForecastPage.css'
import './AdminDesignSystem.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function qualityLabel(quality: string) {
  switch (quality) {
    case 'Insufficient':
      return 'Không đủ'
    case 'Partial':
      return 'Phần hoàn'
    case 'Sufficient':
      return 'Đủ'
    default:
      return quality
  }
}

export function AdminForecastPage() {
  const { accessToken } = useAuth()
  const [branches, setBranches] = useState<BranchDto[] | null>(null)
  const [branchId, setBranchId] = useState('')
  const [horizon, setHorizon] = useState(7)
  const [rows, setRows] = useState<ForecastDto[] | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [run, setRun] = useState<ForecastJobRunDto | null>(null)
  const [runHistory, setRunHistory] = useState<ForecastJobRunDto[]>([])
  const [runState, setRunState] = useState<{ running: boolean; message: string; kind: 'ok' | 'err' }>({
    running: false,
    message: '',
    kind: 'ok',
  })
  const pollAbortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .getBranches({ signal: controller.signal })
      .then(setBranches)
      .catch((error) => {
        if (!isAbortError(error)) setBranches([])
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!branchId && branches && branches.length > 0) {
      setBranchId(branches[0].id)
    }
  }, [branchId, branches])

  // Load the latest run status and recent history whenever the branch (or a
  // manual refresh) changes.
  useEffect(() => {
    if (!branchId || !accessToken) return
    const controller = new AbortController()
    inventoryIntelligenceApi
      .getForecastRuns(branchId, { token: accessToken, signal: controller.signal })
      .then((data) => {
        setRunHistory(data.items)
        setRun(data.items.length > 0 ? data.items[0] : null)
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setRunHistory([])
          setRun(null)
        }
      })
    return () => controller.abort()
  }, [branchId, accessToken, retryKey])

  useEffect(() => {
    if (pollAbortRef.current) pollAbortRef.current.abort()
    pollAbortRef.current = null
    return () => {
      if (pollAbortRef.current) pollAbortRef.current.abort()
      pollAbortRef.current = null
    }
  }, [branchId])

  useEffect(() => {
    if (!branchId || !accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    inventoryIntelligenceApi
      .getForecast(branchId, horizon, { token: accessToken, signal: controller.signal })
      .then((data) => {
        setRows(data)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setRows(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [branchId, horizon, accessToken, retryKey])

  function refresh() {
    setRetryKey((key) => key + 1)
  }

  function pollUntilTerminal(runId: string) {
    if (!accessToken) return
    const token = accessToken
    if (pollAbortRef.current) pollAbortRef.current.abort()
    const pollAbort = new AbortController()
    pollAbortRef.current = pollAbort
    const signal = pollAbort.signal
    let attempts = 0

    async function tick() {
      if (attempts >= 200) return
      attempts++
      await new Promise((resolve) => setTimeout(resolve, 600))
      if (signal.aborted) return
      try {
        const next = await inventoryIntelligenceApi.getForecastRun(runId, {
          token,
          signal,
        })
        setRun(next)
        if (next.status === 'Succeeded' || next.status === 'Failed') {
          setRetryKey((key) => key + 1)
          return
        }
        tick()
      } catch {
        // Stop on error to bound the poll.
      }
    }

    void tick()
  }

  async function triggerRun() {
    if (!accessToken || runState.running) return
    setRunState({ running: true, message: '', kind: 'ok' })
    const controller = new AbortController()
    try {
      const response = await inventoryIntelligenceApi.triggerForecast(
        branchId,
        accessToken,
        controller.signal,
      )
      setRunState({
        running: false,
        message: `Đã đưa vào hàng đợi (job ${response.jobRunId.slice(0, 8)}...).`,
        kind: 'ok',
      })
      pollUntilTerminal(response.jobRunId)
    } catch (error) {
      if (isAbortError(error)) return
      const message =
        error instanceof ApiError && error.status === 409
          ? 'Dự báo đang chạy. Vui lòng chờ lượt hiện tại kết thúc.'
          : 'Không thể kích hoạt lượt chạy. Vui lòng thử lại.'
      setRunState({ running: false, message, kind: 'err' })
    }
  }

  function getQualityVariant(quality: string): 'success' | 'warning' | 'danger' {
    if (quality === 'Sufficient') return 'success'
    if (quality === 'Partial') return 'warning'
    return 'danger'
  }

  function getRunStatusVariant(status: string): 'success' | 'warning' | 'danger' {
    if (status === 'Succeeded') return 'success'
    if (status === 'Failed') return 'danger'
    return 'warning'
  }

  return (
    <main className="admin-page admin-forecast" role="main" aria-label="Dự báo nhu cầu">
      <header className="admin-page-header">
        <h1>Dự báo nhu cầu</h1>
        <p className="admin-page-sub">
          Moving average 7/14 ngày theo chi nhánh. Chạy lại lượt tính khi nhu cầu thay đổi.
        </p>
      </header>

      {runState.message && (
        <section className={`admin-note admin-note--${runState.kind}`} role="status">
          {runState.message}
        </section>
      )}

      {/* Main Forecast Card */}
      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex flex-col md:flex-row md:items-center md:justify-between gap-4 w-full">
            <div className="admin-toolbar-left flex flex-wrap items-center gap-3">
              <div className="admin-field" style={{ margin: 0 }}>
                <label htmlFor="admin-forecast-branch" className="sr-only">Chi nhánh</label>
                <select
                  id="admin-forecast-branch"
                  className="admin-select-filter"
                  value={branchId}
                  onChange={(event) => setBranchId(event.target.value)}
                  aria-label="Chi nhánh"
                >
                  {(branches ?? []).map((branch) => (
                    <option key={branch.id} value={branch.id}>
                      {branch.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="admin-field" style={{ margin: 0 }}>
                <label htmlFor="admin-forecast-horizon" className="sr-only">Kỳ dự báo</label>
                <select
                  id="admin-forecast-horizon"
                  className="admin-select-filter"
                  value={horizon}
                  onChange={(event) => setHorizon(Number(event.target.value))}
                  aria-label="Kỳ dự báo"
                >
                  <option value={7}>7 ngày</option>
                  <option value={14}>14 ngày</option>
                </select>
              </div>
            </div>

            <div className="admin-toolbar-right flex items-center gap-3">
              {branchId && run && (
                <AdminBadge variant="neutral">
                  Lượt gần nhất #{run.id.slice(0, 8)}
                </AdminBadge>
              )}
              <button
                type="button"
                className="btn-primary admin-btn"
                onClick={triggerRun}
                disabled={runState.running}
              >
                {runState.running ? 'Đang chạy…' : 'Chạy lại dự báo'}
              </button>
            </div>
          </div>
        }
      >
        {loadState === 'error' && (
          <section className="admin-alert p-6" role="alert">
            <p className="text-rose-600 font-medium">Không thể tải dự báo. Vui lòng thử lại.</p>
            <button type="button" className="btn btn-sm btn-secondary mt-2" onClick={refresh}>Thử lại</button>
          </section>
        )}

        {loadState === 'loading' && (
          <p className="admin-loading py-8 text-center text-slate-500" aria-busy="true">Đang tải dự báo nhu cầu...</p>
        )}

        {loadState === 'ready' && rows && rows.length === 0 && (
          <AdminEmptyState
            icon="📈"
            message="Chưa có dự báo."
            description='Nhấn "Chạy lại dự báo" để sinh kết quả đầu tiên.'
          />
        )}

        {loadState === 'ready' && rows && rows.length > 0 && (
          <table className="admin-table admin-ds-table" aria-label="Dự báo nhu cầu" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Sản phẩm</th>
                <th scope="col" className="col-numeric text-right">{horizon} ngày dự báo</th>
                <th scope="col" className="col-numeric text-right">Dữ liệu ngày</th>
                <th scope="col" className="col-status text-center">Chất dữ liệu</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="col-text text-left align-middle font-medium text-slate-800">{row.productName}</td>
                  <td className="col-numeric text-right align-middle font-semibold text-slate-900">{formatQuantity(row.predictedQuantity)}</td>
                  <td className="col-numeric text-right align-middle font-medium text-slate-700">{row.actualDataDays}</td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={getQualityVariant(row.dataQuality)}
                      dot
                      className={`admin-forecast-quality admin-forecast-quality--${qualityClass(row.dataQuality)}`}
                    >
                      {qualityLabel(row.dataQuality)}
                    </AdminBadge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </AdminCard>

      {/* History Card */}
      {branchId && runHistory.length > 0 && (
        <AdminCard
          toolbar={
            <div className="admin-toolbar-wrap flex items-center justify-between w-full">
              <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Lịch sử lượt dự báo</h2>
              <AdminBadge variant="neutral">
                {runHistory.length} lượt chạy
              </AdminBadge>
            </div>
          }
        >
          <table className="admin-table admin-ds-table" aria-label="Lịch sử lượt dự báo" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Lượt</th>
                <th scope="col" className="col-status text-center">Trạng thái</th>
                <th scope="col" className="col-text text-left">Tạo lúc</th>
                <th scope="col" className="col-text text-left">Kết lúc</th>
              </tr>
            </thead>
            <tbody>
              {runHistory.map((item) => (
                <tr key={item.id}>
                  <td className="col-text text-left align-middle">
                    <code className="admin-code-badge">{item.id.slice(0, 8)}…</code>
                  </td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={getRunStatusVariant(item.status)}
                      dot
                      className={`admin-forecast-quality admin-forecast-quality--${runStatusClass(item.status)}`}
                    >
                      {item.status}
                    </AdminBadge>
                  </td>
                  <td className="col-text text-left align-middle text-slate-600 text-xs">{new Date(item.createdAtUtc).toLocaleString('vi-VN')}</td>
                  <td className="col-text text-left align-middle text-slate-600 text-xs">{item.completedAtUtc ? new Date(item.completedAtUtc).toLocaleString('vi-VN') : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </AdminCard>
      )}
    </main>
  )
}

function formatQuantity(value: number) {
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value)
}

function qualityClass(quality: string) {
  switch (quality) {
    case 'Insufficient':
      return 'insufficient'
    case 'Partial':
      return 'partial'
    case 'Sufficient':
      return 'sufficient'
    default:
      return 'partial'
  }
}

function runStatusClass(status: string) {
  switch (status) {
    case 'Succeeded':
      return 'sufficient'
    case 'Failed':
      return 'insufficient'
    default:
      return 'partial'
  }
}