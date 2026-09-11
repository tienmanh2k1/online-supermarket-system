import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { branchApi, type BranchDto, type BranchProductInventoryDto } from '../../api/branchApi'
import { AdminInventoryModal } from './AdminInventoryModal'
import { AdminInventoryTransactions } from './AdminInventoryTransactions'
import './AdminOrdersPage.css'
import './AdminInventoryPage.css'
import './AdminDesignSystem.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function formatPrice(value: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
}

function isLowStock(item: BranchProductInventoryDto) {
  return item.availableQuantity <= item.reorderLevel
}

export function AdminInventoryPage() {
  const { accessToken } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const branchId = searchParams.get('branchId') || ''

  const [branches, setBranches] = useState<BranchDto[] | null>(null)
  const [inventory, setInventory] = useState<BranchProductInventoryDto[] | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [editing, setEditing] = useState<BranchProductInventoryDto | null>(null)
  const [history, setHistory] = useState<BranchProductInventoryDto | null>(null)

  // Load the branch list once.
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

  // Default to the first branch when none is selected in the URL.
  useEffect(() => {
    if (!branchId && branches && branches.length > 0) {
      const next = new URLSearchParams(searchParams)
      next.set('branchId', branches[0].id)
      setSearchParams(next, { replace: true })
    }
  }, [branchId, branches, searchParams, setSearchParams])

  // Load inventory for the selected branch.
  useEffect(() => {
    if (!branchId) return
    const controller = new AbortController()
    setLoadState('loading')
    branchApi
      .getBranchInventory(branchId, { signal: controller.signal })
      .then((result) => {
        setInventory(result.products)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setInventory(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [branchId, retryKey])

  function selectBranch(nextBranchId: string) {
    const next = new URLSearchParams(searchParams)
    next.set('branchId', nextBranchId)
    setSearchParams(next)
  }

  function onSaved(updated: BranchProductInventoryDto) {
    setInventory((prev) =>
      prev ? prev.map((row) => (row.productId === updated.productId ? updated : row)) : prev,
    )
    setEditing(null)
  }

  const lowStockCount = inventory?.filter(isLowStock).length ?? 0

  return (
    <main className="admin-page admin-inventory" role="main" aria-label="Quản lý kho và giá">
      <header className="admin-page-header">
        <h1>Kho & Giá theo chi nhánh</h1>
        <p className="admin-page-sub">Điều chỉnh giá bán, tồn kho và định mức nhập cho từng chi nhánh.</p>
      </header>

      {/* Container Card */}
      <div className="admin-inventory-card bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        {/* Top Bar / Toolbar */}
        <div className="admin-inventory-toolbar flex flex-wrap items-center justify-between gap-4 p-4 border-b border-slate-100">
          <div className="admin-inventory-branch-select flex items-center gap-3">
            <label htmlFor="admin-inv-branch" className="text-sm font-semibold text-slate-700">Chi nhánh</label>
            <select
              id="admin-inv-branch"
              value={branchId}
              onChange={(event) => selectBranch(event.target.value)}
              disabled={!branches || branches.length === 0}
              className="px-3 py-2 border border-slate-300 rounded-lg text-sm bg-slate-50 font-medium text-slate-800"
            >
              {!branches && <option value="">Đang tải chi nhánh...</option>}
              {branches && branches.length === 0 && <option value="">Không có chi nhánh</option>}
              {branches?.map((branch) => (
                <option key={branch.id} value={branch.id}>{branch.name}</option>
              ))}
            </select>
          </div>

          {loadState === 'ready' && inventory && (
            <div className="admin-inventory-stats flex items-center gap-2.5 flex-wrap">
              <span className="admin-stat-pill admin-stat-total bg-slate-100 text-slate-600 px-3 py-1 rounded-full text-xs font-medium border border-slate-200">
                {inventory.length} sản phẩm
              </span>
              {lowStockCount > 0 && (
                <span className="admin-stat-pill admin-stat-warning bg-rose-50 text-rose-600 px-3 py-1 rounded-full text-xs font-medium border border-rose-200 inline-flex items-center gap-1.5">
                  <span className="admin-lowstock-dot" aria-hidden="true" />
                  {lowStockCount} sản phẩm dưới định mức
                </span>
              )}
            </div>
          )}
        </div>

        {loadState === 'error' && (
          <div style={{ padding: '1.5rem' }}>
            <section className="admin-alert" role="alert">
              <p>Không thể tải tồn kho chi nhánh. Vui lòng thử lại.</p>
              <button type="button" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
            </section>
          </div>
        )}

        {loadState === 'loading' && (
          <div style={{ padding: '2.5rem', textAlign: 'center' }}>
            <p className="admin-loading" aria-busy="true">Đang tải tồn kho...</p>
          </div>
        )}

        {loadState === 'ready' && inventory && inventory.length === 0 && (
          <div className="admin-empty" style={{ padding: '2.5rem', textAlign: 'center' }}>
            <p>Chi nhánh này chưa có sản phẩm nào trong kho.</p>
          </div>
        )}

        {loadState === 'ready' && inventory && inventory.length > 0 && (
          <div className="admin-table-wrap admin-inventory-table-wrap overflow-x-auto w-full">
            <table className="admin-table admin-inventory-table w-full text-left" aria-label="Tồn kho chi nhánh">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th scope="col" className="text-left py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-text">Sản phẩm</th>
                  <th scope="col" className="text-left py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-text">SKU</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-numeric">Giá bán</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-numeric">Tồn thực</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-numeric">Đang giữ</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-numeric">Khả dụng</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-numeric">Định mức</th>
                  <th scope="col" className="text-right py-3.5 px-4 text-xs font-semibold text-slate-600 uppercase tracking-wider col-actions"><span className="sr-only">Hành động</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {inventory.map((item) => {
                  const low = isLowStock(item)
                  return (
                    <tr
                      key={item.productId}
                      className={`hover:bg-slate-50/80 transition-colors ${low ? 'admin-inventory-row--low' : ''}`}
                    >
                      <td className="col-text py-3.5 px-4">
                        <span className="font-medium text-slate-800 admin-product-name">{item.productName}</span>
                      </td>
                      <td className="col-text py-3.5 px-4">
                        <code className="bg-slate-100 text-slate-600 px-2 py-0.5 rounded text-xs font-mono admin-sku-badge">{item.sku}</code>
                      </td>
                      <td className="col-numeric py-3.5 px-4 text-right">
                        <span className="font-semibold text-slate-900 admin-price-text">{formatPrice(item.sellingPrice)}</span>
                      </td>
                      <td className="col-numeric py-3.5 px-4 text-right font-medium text-slate-700">
                        {item.quantityOnHand}
                      </td>
                      <td className="col-numeric py-3.5 px-4 text-right text-slate-500">
                        {item.reservedQuantity}
                      </td>
                      <td className="col-numeric py-3.5 px-4 text-right whitespace-nowrap">
                        <span className={low ? 'text-rose-600 font-bold admin-avail-low' : 'font-medium text-slate-700'}>
                          {item.availableQuantity}
                        </span>
                        {low && (
                          <span
                            className="admin-lowstock-tag bg-rose-100 text-rose-700 text-xs px-2 py-0.5 rounded-full inline-flex items-center gap-1 ml-2 font-medium"
                            role="status"
                          >
                            <span className="admin-lowstock-dot" aria-hidden="true" />
                            Sắp hết
                          </span>
                        )}
                      </td>
                      <td className="col-numeric py-3.5 px-4 text-right text-slate-600">
                        {item.reorderLevel}
                      </td>
                      <td className="col-actions py-3.5 px-4 text-right whitespace-nowrap">
                        <div className="admin-table-actions">
                          <button
                            type="button"
                            className="admin-action-btn"
                            onClick={() => setHistory(item)}
                            aria-label={`Lịch sử kho ${item.productName}`}
                          >
                            Lịch sử kho
                          </button>
                          <button
                            type="button"
                            className="admin-action-btn admin-action-btn--primary"
                            onClick={() => setEditing(item)}
                            aria-label={`Chỉnh sửa ${item.productName}`}
                          >
                            Sửa
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {editing && (
        <AdminInventoryModal
          branchId={branchId}
          item={editing}
          onClose={() => setEditing(null)}
          onSaved={onSaved}
        />
      )}

      {history && (
        <AdminInventoryTransactions
          inventoryId={history.inventoryId}
          productName={history.productName}
          onClose={() => setHistory(null)}
        />
      )}
    </main>
  )
}
