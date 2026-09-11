import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { adminApi, type PaginatedPromotionsDto, type PromotionDto } from '../../api/adminApi'
import { formatPrice } from '../products/ProductCard'
import { AdminPromotionModal } from './AdminPromotionModal'
import { AdminCard, AdminBadge, AdminPagination, AdminEmptyState } from './components'
import './AdminOrdersPage.css'
import './AdminInventoryPage.css'
import './AdminPromotionsPage.css'
import './AdminDesignSystem.css'

const PAGE_SIZE = 20

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function discountTypeLabel(type: string) {
  return type === 'Percentage' ? 'Phần trăm' : 'Số tiền cố định'
}

function discountValueLabel(promo: PromotionDto) {
  return promo.discountType === 'Percentage' ? `${promo.discountValue}%` : formatPrice(promo.discountValue)
}

function usageLabel(promo: PromotionDto) {
  return `${promo.usageCount}/${promo.usageLimit ?? '∞'}`
}

type ModalState = { mode: 'create' } | { mode: 'edit'; promotion: PromotionDto } | null

export function AdminPromotionsPage() {
  const { accessToken } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page') ?? '1') || 1)

  const [data, setData] = useState<PaginatedPromotionsDto | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [modal, setModal] = useState<ModalState>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    adminApi
      .listPromotions(page, PAGE_SIZE, accessToken, controller.signal)
      .then((result) => {
        setData(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setData(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [accessToken, page, retryKey])

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams)
    next.set('page', String(nextPage))
    setSearchParams(next)
  }

  function handleSaved() {
    setModal(null)
    setRetryKey((prev) => prev + 1)
  }

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1

  return (
    <main className="admin-page admin-promotions" role="main" aria-label="Quản lý khuyến mãi">
      <header className="admin-page-header admin-promotions-header">
        <div>
          <h1>Quản lý khuyến mãi</h1>
          <p className="admin-page-sub">Tạo, chỉnh sửa và bật/tắt mã giảm giá.</p>
        </div>
      </header>

      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex items-center justify-between w-full">
            <div className="flex items-center gap-3">
              <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Danh sách khuyến mãi</h2>
              {data && (
                <AdminBadge variant="neutral">
                  Tổng: <strong style={{ marginLeft: 4 }}>{data.totalCount}</strong> mã
                </AdminBadge>
              )}
            </div>
            <button type="button" className="btn-primary admin-create-btn" onClick={() => setModal({ mode: 'create' })}>
              + Tạo mã mới
            </button>
          </div>
        }
        footer={
          data ? (
            <AdminPagination
              currentPage={page}
              totalPages={totalPages}
              totalCount={data.totalCount}
              itemName="khuyến mãi"
              onPageChange={setPage}
            />
          ) : undefined
        }
      >
        {loadState === 'error' && (
          <section className="admin-alert p-6" role="alert">
            <p className="text-rose-600 font-medium">Không thể tải danh sách khuyến mãi. Vui lòng thử lại.</p>
            <button type="button" className="btn btn-sm btn-secondary mt-2" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
          </section>
        )}

        {loadState === 'loading' && <p className="admin-loading py-8 text-center text-slate-500" aria-busy="true">Đang tải khuyến mãi...</p>}

        {loadState === 'ready' && data && data.promotions.length === 0 && (
          <AdminEmptyState
            icon="🎟️"
            message="Chưa có mã giảm giá nào."
            description='Bấm "+ Tạo mã mới" để thêm mã khuyến mãi.'
          />
        )}

        {loadState === 'ready' && data && data.promotions.length > 0 && (
          <table className="admin-table admin-ds-table" aria-label="Danh sách khuyến mãi" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Mã</th>
                <th scope="col" className="col-text text-left">Loại</th>
                <th scope="col" className="col-numeric text-right">Giá trị</th>
                <th scope="col" className="col-numeric text-right">Đơn tối thiểu</th>
                <th scope="col" className="col-numeric text-right">Đã dùng / Giới hạn</th>
                <th scope="col" className="col-status text-center">Trạng thái</th>
                <th scope="col" className="col-actions text-right"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody>
              {data.promotions.map((promo) => (
                <tr key={promo.id}>
                  <td className="col-text text-left align-middle">
                    <code className="admin-code-badge">{promo.code}</code>
                  </td>
                  <td className="col-text text-left align-middle text-slate-700">{discountTypeLabel(promo.discountType)}</td>
                  <td className="col-numeric text-right align-middle font-medium text-slate-900">{discountValueLabel(promo)}</td>
                  <td className="col-numeric text-right align-middle text-slate-700">{formatPrice(promo.minOrderAmount)}</td>
                  <td className="col-numeric text-right align-middle text-slate-600 font-mono text-xs">{usageLabel(promo)}</td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={promo.isActive ? 'success' : 'neutral'}
                      dot
                      className={`admin-status admin-status--${promo.isActive ? 'active' : 'inactive'}`}
                    >
                      {promo.isActive ? 'Đang áp dụng' : 'Đã tắt'}
                    </AdminBadge>
                  </td>
                  <td className="col-actions text-right align-middle">
                    <button
                      type="button"
                      className="admin-link admin-link-btn admin-btn-action"
                      onClick={() => setModal({ mode: 'edit', promotion: promo })}
                      aria-label={`Sửa mã ${promo.code}`}
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
        <AdminPromotionModal mode="create" onClose={() => setModal(null)} onSaved={handleSaved} />
      )}
      {modal?.mode === 'edit' && (
        <AdminPromotionModal
          mode="edit"
          promotion={modal.promotion}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
    </main>
  )
}
