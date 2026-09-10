import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { adminApi } from '../../api/adminApi'
import type { BranchDto } from '../../api/branchApi'
import { AdminBranchModal } from './AdminBranchModal'
import './AdminOrdersPage.css'
import './AdminInventoryPage.css'
import './AdminPromotionsPage.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

type ModalState = { mode: 'create' } | { mode: 'edit'; branch: BranchDto } | null

export function AdminBranchesPage() {
  const { accessToken } = useAuth()
  const [branches, setBranches] = useState<BranchDto[] | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [modal, setModal] = useState<ModalState>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    adminApi
      .listBranches(accessToken, controller.signal)
      .then((result) => {
        setBranches(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setBranches(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [accessToken, retryKey])

  function handleSaved() {
    setModal(null)
    setRetryKey((prev) => prev + 1)
  }

  return (
    <main className="admin-page admin-branches" role="main" aria-label="Quản lý chi nhánh">
      <header className="admin-page-header admin-promotions-header">
        <div>
          <h1>Quản lý chi nhánh</h1>
          <p className="admin-page-sub">Tạo, chỉnh sửa và bật/tắt hoạt động của chi nhánh.</p>
        </div>
        <button type="button" className="btn-primary admin-create-btn" onClick={() => setModal({ mode: 'create' })}>
          + Tạo chi nhánh
        </button>
      </header>

      {loadState === 'error' && (
        <section className="admin-alert" role="alert">
          <p>Không thể tải danh sách chi nhánh. Vui lòng thử lại.</p>
          <button type="button" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
        </section>
      )}

      {loadState === 'loading' && <p className="admin-loading" aria-busy="true">Đang tải chi nhánh...</p>}

      {loadState === 'ready' && branches && branches.length === 0 && (
        <div className="admin-empty"><p>Chưa có chi nhánh nào. Bấm "Tạo chi nhánh" để thêm.</p></div>
      )}

      {loadState === 'ready' && branches && branches.length > 0 && (
        <div className="admin-table-wrap">
          <table className="admin-table" aria-label="Danh sách chi nhánh">
            <thead>
              <tr>
                <th scope="col">Tên chi nhánh</th>
                <th scope="col">Địa chỉ</th>
                <th scope="col">SĐT</th>
                <th scope="col">Trạng thái</th>
                <th scope="col"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody>
              {branches.map((branch) => (
                <tr key={branch.id}>
                  <td>{branch.name}</td>
                  <td>{branch.address}</td>
                  <td>{branch.phone || '—'}</td>
                  <td>
                    <span className={`admin-status admin-status--${branch.isActive ? 'active' : 'inactive'}`}>
                      {branch.isActive ? 'Đang hoạt động' : 'Đã tắt'}
                    </span>
                  </td>
                  <td>
                    <button
                      type="button"
                      className="admin-link admin-link-btn"
                      onClick={() => setModal({ mode: 'edit', branch })}
                      aria-label={`Sửa chi nhánh ${branch.name}`}
                    >
                      Sửa
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modal?.mode === 'create' && (
        <AdminBranchModal mode="create" onClose={() => setModal(null)} onSaved={handleSaved} />
      )}
      {modal?.mode === 'edit' && modal.branch && (
        <AdminBranchModal
          key={modal.branch.id}
          mode="edit"
          branch={modal.branch}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
    </main>
  )
}
