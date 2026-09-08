import { useCallback, useEffect, useRef, useState } from 'react'
import { recommendationApi, type RecommendationSampleResponse } from '../../api/recommendationApi'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../../api/httpClient'
import './AdminOrdersPage.css'
import './AdminRecommendationsPage.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

type ScopeFilter = '' | 'Global' | 'User' | 'SimilarProduct'

export function AdminRecommendationsPage() {
  const { accessToken } = useAuth()
  const [scope, setScope] = useState<ScopeFilter>('')
  const [limit, setLimit] = useState(20)
  const [sample, setSample] = useState<RecommendationSampleResponse | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [runState, setRunState] = useState<{ running: boolean; message: string; kind: 'ok' | 'err' }>({
    running: false,
    message: '',
    kind: 'ok',
  })
  const [retryKey, setRetryKey] = useState(0)
  const pollAbortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    return () => {
      if (pollAbortRef.current) pollAbortRef.current.abort()
    }
  }, [])

  const load = useCallback(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    recommendationApi
      .getAdminSample({ scope: scope || undefined, limit, token: accessToken, signal: controller.signal })
      .then((response) => {
        setSample(response)
        setLoadState('ready')
      })
      .catch((error) => {
        if (isAbortError(error)) return
        setSample(null)
        setLoadState('error')
      })
    return () => controller.abort()
  }, [accessToken, scope, limit, retryKey])

  useEffect(() => {
    return load()
  }, [load])

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
      if (attempts >= 120) {
        setRunState({ running: false, message: 'Hết thời gian chờ lượt chạy hoàn thành.', kind: 'err' })
        return
      }
      attempts++
      await new Promise((resolve) => setTimeout(resolve, 600))
      if (signal.aborted) return
      try {
        const next = await recommendationApi.getJobRun(runId, { token, signal })
        if (next.status === 'Succeeded') {
          setRunState({
            running: false,
            message: `Lượt chạy ${runId.slice(0, 8)} đã hoàn thành thành công.`,
            kind: 'ok',
          })
          setScope('')
          setLimit(20)
          setRetryKey((key) => key + 1)
          return
        }
        if (next.status === 'Failed') {
          setRunState({
            running: false,
            message: `Lượt chạy thất bại: ${next.errorSummary || 'Lỗi không xác định'}.`,
            kind: 'err',
          })
          return
        }
        void tick()
      } catch {
        setRunState({ running: false, message: 'Không thể kiểm tra trạng thái lượt chạy.', kind: 'err' })
      }
    }

    void tick()
  }

  async function triggerRun() {
    if (!accessToken || runState.running) return
    setRunState({ running: true, message: '', kind: 'ok' })
    const controller = new AbortController()
    try {
      const response = await recommendationApi.triggerRun(accessToken, controller.signal)
      setRunState({
        running: true,
        message: `Đã đưa vào hàng đợi (job ${response.jobRunId.slice(0, 8)}...). Đang xử lý...`,
        kind: 'ok',
      })
      pollUntilTerminal(response.jobRunId)
    } catch (error) {
      if (isAbortError(error)) return
      const message =
        error instanceof ApiError && error.status === 409
          ? 'Một lượt chạy đang hoạt động. Vui lòng chờ lượt hiện tại kết thúc.'
          : 'Không thể kích hoạt lượt chạy. Vui lòng thử lại.'
      setRunState({ running: false, message, kind: 'err' })
    }
  }

  return (
    <main className="admin-page admin-recommendations" role="main" aria-label="Gợi ý sản phẩm">
      <header className="admin-page-header">
        <h1>Gợi ý sản phẩm</h1>
        <p className="admin-page-sub">
          Kích hoạt lại lượt tính gợi ý và duyệt các dòng kết quả gần nhất của thuật toán.
        </p>
      </header>

      <div className="admin-toolbar">
        <div className="admin-field">
          <label htmlFor="admin-rec-scope">Phạm vi</label>
          <select
            id="admin-rec-scope"
            value={scope}
            onChange={(event) => setScope(event.target.value as ScopeFilter)}
          >
            <option value="">Tất cả</option>
            <option value="Global">Toàn hệ thống</option>
            <option value="User">Theo người dùng</option>
            <option value="SimilarProduct">Sản phẩm tương tự</option>
          </select>
        </div>
        <div className="admin-field">
          <label htmlFor="admin-rec-limit">Số dòng</label>
          <select
            id="admin-rec-limit"
            value={limit}
            onChange={(event) => setLimit(Number(event.target.value))}
          >
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
        </div>
        <button type="button" className="admin-btn" onClick={triggerRun} disabled={runState.running}>
          {runState.running ? 'Đang chạy…' : 'Chạy lại lần tính'}
        </button>
      </div>

      {runState.message && (
        <section className={`admin-note admin-note--${runState.kind}`} role="status">
          {runState.message}
        </section>
      )}

      {loadState === 'ready' && sample && (
        <p className="admin-recommendations-meta">
          Lượt #{sample.jobRunId.slice(0, 8)} · thuật toán {sample.algorithmVersion} · tạo lúc{' '}
          {new Date(sample.generatedAtUtc).toLocaleString('vi-VN')} · hết hạn{' '}
          {new Date(sample.expiresAtUtc).toLocaleString('vi-VN')}
        </p>
      )}

      {loadState === 'error' && (
        <section className="admin-alert" role="alert">
          <p>Không thể tải dữ liệu gợi ý. Vui lòng thử lại.</p>
          <button type="button" onClick={refresh}>Thử lại</button>
        </section>
      )}

      {loadState === 'loading' && (
        <p className="admin-loading" aria-busy="true">Đang tải dữ liệu gợi ý...</p>
      )}

      {loadState === 'ready' && sample && sample.items.length === 0 && (
        <div className="admin-empty">
          <p>Chưa có dữ liệu gợi ý. Nhấn &quot;Chạy lại lần tính&quot; để sinh kết quả đầu tiên.</p>
        </div>
      )}

      {loadState === 'ready' && sample && sample.items.length > 0 && (
        <div className="admin-table-wrap">
          <table className="admin-table" aria-label="Kết quả gợi ý sản phẩm">
            <thead>
              <tr>
                <th scope="col">Phạm vi</th>
                <th scope="col">Đối tượng</th>
                <th scope="col">Sản phẩm</th>
                <th scope="col">Điểm</th>
                <th scope="col">Hạng</th>
                <th scope="col">Lý do</th>
              </tr>
            </thead>
            <tbody>
              {sample.items.map((item) => (
                <tr key={`${item.scope}-${item.audienceKey}-${item.productId}-${item.rank}`}>
                  <td>{scopeLabel(item.scope)}</td>
                  <td><code>{item.audienceKey}</code></td>
                  <td><code title={item.productId}>{item.productId.slice(0, 8)}…</code></td>
                  <td>{item.score.toFixed(4)}</td>
                  <td>{item.rank}</td>
                  <td>{item.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  )
}

function scopeLabel(scope: string) {
  switch (scope) {
    case 'Global':
      return 'Toàn hệ thống'
    case 'User':
      return 'Theo người dùng'
    case 'SimilarProduct':
      return 'Sản phẩm tương tự'
    default:
      return scope
  }
}