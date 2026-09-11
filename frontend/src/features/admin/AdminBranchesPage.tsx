import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { adminApi } from '../../api/adminApi'
import type { BranchDto } from '../../api/branchApi'
import { AdminBranchModal } from './AdminBranchModal'
import { AdminCard, AdminBadge, AdminEmptyState } from './components'
import './AdminOrdersPage.css'
import './AdminInventoryPage.css'
import './AdminPromotionsPage.css'
import './AdminDesignSystem.css'

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
      </header>

      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex items-center justify-between w-full">
            <div className="flex items-center gap-3">
              <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Danh sách chi nhánh</h2>
              {branches && (
                <AdminBadge variant="neutral">
                  Tổng: <strong style={{ marginLeft: 4 }}>{branches.length}</strong> chi nhánh
                </AdminBadge>
              )}
            </div>
            <button
              type="button"
              className="btn-primary admin-create-btn"
              onClick={() => setModal({ mode: 'create' })}
            >
              + Tạo chi nhánh
            </button>
          </div>
        }
      >
        {loadState === 'error' && (
          <section className="admin-alert p-6" role="alert">
            <p className="text-rose-600 font-medium">Không thể tải danh sách chi nhánh. Vui lòng thử lại.</p>
            <button type="button" className="btn btn-sm btn-secondary mt-2" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
          </section>
        )}

        {loadState === 'loading' && <p className="admin-loading py-8 text-center text-slate-500" aria-busy="true">Đang tải chi nhánh...</p>}

        {loadState === 'ready' && branches && branches.length === 0 && (
          <AdminEmptyState
            icon="🏬"
            message="Chưa có chi nhánh nào."
            description='Bấm "+ Tạo chi nhánh" để thêm mới.'
          />
        )}

        {loadState === 'ready' && branches && branches.length > 0 && (
          <table className="admin-table admin-ds-table" aria-label="Danh sách chi nhánh" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Tên chi nhánh</th>
                <th scope="col" className="col-text text-left">Địa chỉ</th>
                <th scope="col" className="col-text text-left">SĐT</th>
                <th scope="col" className="col-status text-center">Trạng thái</th>
                <th scope="col" className="col-actions text-right"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody>
              {branches.map((branch) => (
                <tr key={branch.id}>
                  <td className="col-text text-left align-middle font-medium text-slate-800">{branch.name}</td>
                  <td className="col-text text-left align-middle text-slate-700">{branch.address}</td>
                  <td className="col-text text-left align-middle text-slate-600 font-mono text-xs">{branch.phone || '—'}</td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={branch.isActive ? 'success' : 'neutral'}
                      dot
                      className={`admin-status admin-status--${branch.isActive ? 'active' : 'inactive'}`}
                    >
                      {branch.isActive ? 'Đang hoạt động' : 'Đã tắt'}
                    </AdminBadge>
                  </td>
                  <td className="col-actions text-right align-middle">
                    <button
                      type="button"
                      className="admin-link admin-link-btn admin-btn-action"
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
        )}
      </AdminCard>

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
