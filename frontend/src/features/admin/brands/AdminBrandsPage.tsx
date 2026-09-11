import React, { useEffect, useRef, useState } from 'react'
import { adminCatalogApi, AdminBrandDto } from '../../../api/adminCatalogApi'
import { useAuth } from '../../auth/AuthContext'
import { AdminConfirmDialog } from '../AdminConfirmDialog'
import { AdminCard, AdminBadge, AdminEmptyState } from '../components'
import '../AdminCatalog.css'
import '../AdminDesignSystem.css'

export function AdminBrandsPage() {
  const { accessToken } = useAuth()
  const [brands, setBrands] = useState<AdminBrandDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  // Form state
  const [editingId, setEditingId] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Deactivate dialog state
  const [deactivateDialog, setDeactivateDialog] = useState<{
    isOpen: boolean
    brand: AdminBrandDto | null
    isBusy: boolean
  }>({
    isOpen: false,
    brand: null,
    isBusy: false,
  })

  // Action busy state for individual items
  const [actionBusyId, setActionBusyId] = useState<string | null>(null)

  const abortControllerRef = useRef<AbortController | null>(null)

  const loadBrands = async () => {
    if (!accessToken) return
    abortControllerRef.current?.abort()
    const controller = new AbortController()
    abortControllerRef.current = controller

    setLoading(true)
    setError(null)
    try {
      const data = await adminCatalogApi.getBrands(accessToken, controller.signal)
      if (abortControllerRef.current === controller && !controller.signal.aborted) {
        setBrands(data)
        setLoading(false)
      }
    } catch (err: any) {
      if (abortControllerRef.current === controller && !controller.signal.aborted && err.name !== 'AbortError') {
        setError(err.data?.message || err.message || 'Lỗi tải thương hiệu')
        setBrands([])
        setLoading(false)
      }
    }
  }

  useEffect(() => {
    loadBrands()
    return () => {
      abortControllerRef.current?.abort()
    }
  }, [accessToken])

  const handleStartEdit = (brand: AdminBrandDto) => {
    setEditingId(brand.id)
    setName(brand.name)
    setSlug(brand.slug)
    setFormError(null)
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setName('')
    setSlug('')
    setFormError(null)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!accessToken || isSubmitting) return

    const trimmedName = name.trim()
    const trimmedSlug = slug.trim()

    if (!trimmedName || !trimmedSlug) {
      setFormError('Vui lòng nhập đầy đủ tên và slug thương hiệu.')
      return
    }

    setIsSubmitting(true)
    setFormError(null)
    setError(null)

    const payload = {
      name: trimmedName,
      slug: trimmedSlug,
    }

    try {
      if (editingId) {
        await adminCatalogApi.updateBrand(editingId, payload, accessToken)
      } else {
        await adminCatalogApi.createBrand(payload, accessToken)
      }
      handleCancelEdit()
      await loadBrands()
    } catch (err: any) {
      setFormError(err.data?.message || err.message || 'Lỗi lưu thương hiệu')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleOpenDeactivate = (brand: AdminBrandDto) => {
    setDeactivateDialog({
      isOpen: true,
      brand,
      isBusy: false,
    })
  }

  const handleConfirmDeactivate = async () => {
    if (!accessToken || !deactivateDialog.brand) return

    setDeactivateDialog(prev => ({ ...prev, isBusy: true }))
    try {
      await adminCatalogApi.updateBrandStatus(
        deactivateDialog.brand.id,
        { isActive: false },
        accessToken
      )
      setDeactivateDialog({ isOpen: false, brand: null, isBusy: false })
      await loadBrands()
    } catch (err: any) {
      setError(err.data?.message || err.message || 'Lỗi vô hiệu hóa thương hiệu')
      setDeactivateDialog(prev => ({ ...prev, isBusy: false, isOpen: false }))
    }
  }

  const handleRestore = async (brand: AdminBrandDto) => {
    if (!accessToken || actionBusyId) return

    setActionBusyId(brand.id)
    setError(null)
    try {
      await adminCatalogApi.updateBrandStatus(brand.id, { isActive: true }, accessToken)
      await loadBrands()
    } catch (err: any) {
      setError(err.data?.message || err.message || 'Lỗi kích hoạt lại thương hiệu')
    } finally {
      setActionBusyId(null)
    }
  }

  return (
    <div className="admin-page">
      <header className="admin-header">
        <h1>Quản lý thương hiệu</h1>
      </header>

      {error && (
        <div role="alert" className="admin-error-alert">
          <span>{error}</span>
          <button type="button" onClick={loadBrands}>Thử lại</button>
        </div>
      )}

      <div className="admin-card">
        <h2>{editingId ? 'Chỉnh sửa thương hiệu' : 'Thêm thương hiệu mới'}</h2>
        {formError && <div role="alert" className="admin-error-alert">{formError}</div>}

        <form onSubmit={handleSubmit} className="admin-form">
          <div className="form-group">
            <label htmlFor="brand-name">Tên thương hiệu:</label>
            <input
              id="brand-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              disabled={isSubmitting}
              placeholder="VD: Samsung, Sony, Vinamilk..."
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="brand-slug">Slug:</label>
            <input
              id="brand-slug"
              type="text"
              value={slug}
              onChange={e => setSlug(e.target.value)}
              disabled={isSubmitting}
              placeholder="VD: samsung, sony, vinamilk..."
              required
            />
          </div>

          <div className="form-actions">
            <button type="submit" disabled={isSubmitting} className="btn btn-primary">
              {isSubmitting ? 'Đang lưu...' : editingId ? 'Cập nhật' : 'Thêm thương hiệu'}
            </button>
            {editingId && (
              <button
                type="button"
                onClick={handleCancelEdit}
                disabled={isSubmitting}
                className="btn btn-secondary"
              >
                Hủy chỉnh sửa
              </button>
            )}
          </div>
        </form>
      </div>

      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex items-center justify-between w-full">
            <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Danh sách thương hiệu</h2>
            <AdminBadge variant="neutral">
              Tổng: <strong style={{ marginLeft: 4 }}>{brands.length}</strong> thương hiệu
            </AdminBadge>
          </div>
        }
      >
        {loading ? (
          <div className="admin-loading py-8 text-center text-slate-500">Đang tải thương hiệu...</div>
        ) : brands.length === 0 ? (
          <AdminEmptyState icon="🏷️" message="Không có thương hiệu nào." />
        ) : (
          <table className="admin-table admin-ds-table" aria-label="Danh sách thương hiệu" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th className="col-text text-left">Tên thương hiệu</th>
                <th className="col-text text-left">Slug</th>
                <th className="col-status text-center">Trạng thái</th>
                <th className="col-actions text-right" style={{ width: 140 }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {brands.map(b => {
                const isItemBusy = actionBusyId === b.id || isSubmitting
                return (
                  <tr key={b.id} className={!b.isActive ? 'row-inactive' : ''}>
                    <td className="col-text text-left align-middle">
                      <strong className="text-slate-800 font-medium">{b.name}</strong>
                    </td>
                    <td className="col-text text-left align-middle">
                      <code className="admin-code-badge">{b.slug}</code>
                    </td>
                    <td className="col-status text-center align-middle">
                      <AdminBadge variant={b.isActive ? 'success' : 'danger'} dot className={`badge ${b.isActive ? 'badge-active' : 'badge-inactive'}`}>
                        {b.isActive ? 'Hoạt động' : 'Vô hiệu hóa'}
                      </AdminBadge>
                    </td>
                    <td className="col-actions text-right align-middle">
                      <div className="table-actions admin-table-actions">
                        <button
                          type="button"
                          onClick={() => handleStartEdit(b)}
                          disabled={isItemBusy}
                          className="btn btn-sm btn-secondary"
                        >
                          Sửa
                        </button>
                        {b.isActive ? (
                          <button
                            type="button"
                            onClick={() => handleOpenDeactivate(b)}
                            disabled={isItemBusy}
                            className="btn btn-sm btn-danger"
                          >
                            Vô hiệu hóa
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => handleRestore(b)}
                            disabled={isItemBusy}
                            className="btn btn-sm btn-success"
                          >
                            Kích hoạt lại
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </AdminCard>

      <AdminConfirmDialog
        isOpen={deactivateDialog.isOpen}
        title="Vô hiệu hóa thương hiệu"
        message={`Bạn có chắc chắn muốn vô hiệu hóa thương hiệu "${deactivateDialog.brand?.name}"?`}
        confirmLabel="Vô hiệu hóa"
        isBusy={deactivateDialog.isBusy}
        onCancel={() => setDeactivateDialog({ isOpen: false, brand: null, isBusy: false })}
        onConfirm={handleConfirmDeactivate}
      />
    </div>
  )
}
