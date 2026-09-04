import { useEffect, useRef, useState } from 'react'
import { branchApi, type BranchDto } from '../../api/branchApi'
import {
  inventoryIntelligenceApi,
  type ForecastDto,
  type ForecastJobRunDto,
} from '../../api/inventoryIntelligenceApi'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../../api/httpClient'
import './AdminForecastPage.css'

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
      if (attempts >= 12) return
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

  return (
    <main className="admin-page admin-forecast" role="main" aria-label="Dự báo nhu cầu">
      <header className="admin-page-header">
        <h1>Dự báo nhu cầu</h1>
        <p className="admin-page-sub">
          Moving average 7/14 ngày theo chi nhánh. Chạy lại lượt tính khi nhu cầu válto.
        </p>
      </header>

      <div className="admin-toolbar">
        <div className="admin-field">
          <label htmlFor="admin-forecast-branch">Chi nhánh</label>
          <select
            id="admin-forecast-branch"
            value={branchId}
            onChange={(event) => setBranchId(event.target.value)}
          >
            {(branches ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
        </div>
        <div className="admin-field">
          <label htmlFor="admin-forecast-horizon">Kỳ dự báo</label>
          <select
            id="admin-forecast-horizon"
            value={horizon}
            onChange={(event) => setHorizon(Number(event.target.value))}
          >
            <option value={7}>7 ngày</option>
            <option value={14}>14 ngày</option>
          </select>
        </div>
        <button type="button" className="admin-btn" onClick={triggerRun} disabled={runState.running}>
          {runState.running ? 'Đang chạy…' : 'Chạy lại dự báo'}
        </button>
      </div>

      {runState.message && (
        <section className={`admin-note admin-note--${runState.kind}`} role="status">
          {runState.message}
        </section>
      )}

      {branchId && run && (
        <p className="admin-forecast-meta">
          Lượt gần nhất #{run.id.slice(0, 8)} · trạng {run.status}
          {run.completedAtUtc
            ? ` · kết lúc ${new Date(run.completedAtUtc).toLocaleString('vi-VN')}`
            : ` · tạo lúc ${new Date(run.createdAtUtc).toLocaleString('vi-VN')}`}
        </p>
      )}

      {branchId && runHistory.length === 0 && (
        <p className="admin-forecast-meta">Chưa có lượt chạy dự báo cho chi nhánh này.</p>
      )}

      {branchId && runHistory.length > 0 && (
        <div className="admin-table-wrap">
          <table className="admin-table" aria-label="Lịch sử lượt dự báo">
            <thead>
              <tr>
                <th scope="col">Lượt</th>
                <th scope="col">Trạng</th>
                <th scope="col">Tạo lúc</th>
                <th scope="col">Kết lúc</th>
              </tr>
            </thead>
            <tbody>
              {runHistory.map((item) => (
                <tr key={item.id}>
                  <td><code>{item.id.slice(0, 8)}…</code></td>
                  <td className={`admin-forecast-quality admin-forecast-quality--${runStatusClass(item.status)}`}>
                    {item.status}
                  </td>
                  <td>{new Date(item.createdAtUtc).toLocaleString('vi-VN')}</td>
                  <td>{item.completedAtUtc ? new Date(item.completedAtUtc).toLocaleString('vi-VN') : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {loadState === 'error' && (
        <section className="admin-alert" role="alert">
          <p>Không thể tải dự báo. Vui lòng thử lại.</p>
          <button type="button" onClick={refresh}>Thử lại</button>
        </section>
      )}

      {loadState === 'loading' && (
        <p className="admin-loading" aria-busy="true">Đang tải dự báo nhu cầu...</p>
      )}

      {loadState === 'ready' && rows && rows.length === 0 && (
        <div className="admin-empty">
          <p>Chưa có dự báo. Nhấn &quot;Chạy lại dự báo&quot; để sinh kết quà đầu đầu.</p>
        </div>
      )}

      {loadState === 'ready' && rows && rows.length > 0 && (
        <div className="admin-table-wrap">
          <table className="admin-table" aria-label="Dự báo nhu cầu">
            <thead>
              <tr>
                <th scope="col">Sản phẩm</th>
                <th scope="col">{horizon} ngày dự báo</th>
                <th scope="col">Đữ liệu ngày</th>
                <th scope="col">Chất dữ liệu</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td>{row.productName}</td>
                  <td>{formatQuantity(row.predictedQuantity)}</td>
                  <td>{row.actualDataDays}</td>
                  <td className={`admin-forecast-quality admin-forecast-quality--${qualityClass(
                    row.dataQuality,
                  )}`}>
                    {qualityLabel(row.dataQuality)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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