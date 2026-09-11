import React, { useEffect, useRef, useState, useTransition } from 'react'
import { adminCatalogApi, AdminCategoryDto } from '../../../api/adminCatalogApi'
import { useAuth } from '../../auth/AuthContext'
import { AdminConfirmDialog } from '../AdminConfirmDialog'
import { AdminCard, AdminBadge, AdminEmptyState } from '../components'
import '../AdminCatalog.css'
import '../AdminDesignSystem.css'

export function AdminCategoriesPage() {
  const { accessToken } = useAuth()
  const [categories, setCategories] = useState<AdminCategoryDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  // Form state
  const [editingId, setEditingId] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [parentCategoryId, setParentCategoryId] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Deactivate dialog state
  const [deactivateDialog, setDeactivateDialog] = useState<{
    isOpen: boolean
    category: AdminCategoryDto | null
    isBusy: boolean
  }>({
    isOpen: false,
    category: null,
    isBusy: false,
  })

  // Action busy state for individual items
  const [actionBusyId, setActionBusyId] = useState<string | null>(null)

  const abortControllerRef = useRef<AbortController | null>(null)

  const loadCategories = async () => {
    if (!accessToken) return
    abortControllerRef.current?.abort()
    const controller = new AbortController()
    abortControllerRef.current = controller

    setLoading(true)
    setError(null)
    try {
      const data = await adminCatalogApi.getCategories(accessToken, controller.signal)
      if (abortControllerRef.current === controller && !controller.signal.aborted) {
        setCategories(data)
        setLoading(false)
      }
    } catch (err: any) {
      if (abortControllerRef.current === controller && !controller.signal.aborted && err.name !== 'AbortError') {
        setError(err.data?.message || err.message || 'Lỗi tải danh mục')
        setCategories([])
        setLoading(false)
      }
    }
  }

  useEffect(() => {
    loadCategories()
    return () => {
      abortControllerRef.current?.abort()
    }
  }, [accessToken])

  // Helper to compute invalid parent IDs (self and all descendants)
  const getInvalidParentIds = (currentId: string): Set<string> => {
    const invalid = new Set<string>([currentId])
    let added = true
    while (added) {
      added = false
      for (const cat of categories) {
        if (cat.parentCategoryId && invalid.has(cat.parentCategoryId) && !invalid.has(cat.id)) {
          invalid.add(cat.id)
          added = true
        }
      }
    }
    return invalid
  }

  const invalidParentIds = editingId ? getInvalidParentIds(editingId) : new Set<string>()

  const handleStartEdit = (cat: AdminCategoryDto) => {
    setEditingId(cat.id)
    setName(cat.name)
    setSlug(cat.slug)
    setParentCategoryId(cat.parentCategoryId || '')
    setFormError(null)
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setName('')
    setSlug('')
    setParentCategoryId('')
    setFormError(null)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!accessToken || isSubmitting) return

    const trimmedName = name.trim()
    const trimmedSlug = slug.trim()

    if (!trimmedName || !trimmedSlug) {
      setFormError('Vui lòng nhập đầy đủ tên và slug danh mục.')
      return
    }

    setIsSubmitting(true)
    setFormError(null)
    setError(null)

    const payload = {
      name: trimmedName,
      slug: trimmedSlug,
      parentCategoryId: parentCategoryId ? parentCategoryId : null,
    }

    try {
      if (editingId) {
        await adminCatalogApi.updateCategory(editingId, payload, accessToken)
      } else {
        await adminCatalogApi.createCategory(payload, accessToken)
      }
      handleCancelEdit()
      await loadCategories()
    } catch (err: any) {
      setFormError(err.data?.message || err.message || 'Lỗi lưu danh mục')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleOpenDeactivate = (cat: AdminCategoryDto) => {
    setDeactivateDialog({
      isOpen: true,
      category: cat,
      isBusy: false,
    })
  }

  const handleConfirmDeactivate = async () => {
    if (!accessToken || !deactivateDialog.category) return

    setDeactivateDialog(prev => ({ ...prev, isBusy: true }))
    try {
      await adminCatalogApi.updateCategoryStatus(
        deactivateDialog.category.id,
        { isActive: false },
        accessToken
      )
      setDeactivateDialog({ isOpen: false, category: null, isBusy: false })
      await loadCategories()
    } catch (err: any) {
      setError(err.data?.message || err.message || 'Lỗi vô hiệu hóa danh mục')
      setDeactivateDialog(prev => ({ ...prev, isBusy: false, isOpen: false }))
    }
  }

  const handleRestore = async (cat: AdminCategoryDto) => {
    if (!accessToken || actionBusyId) return

    setActionBusyId(cat.id)
    setError(null)
    try {
      await adminCatalogApi.updateCategoryStatus(cat.id, { isActive: true }, accessToken)
      await loadCategories()
    } catch (err: any) {
      setError(err.data?.message || err.message || 'Lỗi kích hoạt lại danh mục')
    } finally {
      setActionBusyId(null)
    }
  }

  // Parent name map for display
  const categoryMap = new Map(categories.map(c => [c.id, c.name]))

  return (
    <div className="admin-page">
      <header className="admin-header">
        <h1>Quản lý danh mục</h1>
      </header>

      {error && (
        <div role="alert" className="admin-error-alert">
          <span>{error}</span>
          <button type="button" onClick={loadCategories}>Thử lại</button>
        </div>
      )}

      <div className="admin-card">
        <h2>{editingId ? 'Chỉnh sửa danh mục' : 'Thêm danh mục mới'}</h2>
        {formError && <div role="alert" className="admin-error-alert">{formError}</div>}

        <form onSubmit={handleSubmit} className="admin-form">
          <div className="form-group">
            <label htmlFor="cat-name">Tên danh mục:</label>
            <input
              id="cat-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              disabled={isSubmitting}
              placeholder="VD: Điện thoại, Rau củ..."
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="cat-slug">Slug:</label>
            <input
              id="cat-slug"
              type="text"
              value={slug}
              onChange={e => setSlug(e.target.value)}
              disabled={isSubmitting}
              placeholder="VD: dien-thoai, rau-cu..."
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="cat-parent">Danh mục cha:</label>
            <select
              id="cat-parent"
              value={parentCategoryId}
              onChange={e => setParentCategoryId(e.target.value)}
              disabled={isSubmitting}
            >
              <option value="">-- Không có (Danh mục gốc) --</option>
              {categories
                .filter(c => !invalidParentIds.has(c.id))
                .map(c => (
                  <option key={c.id} value={c.id}>
                    {c.name} ({c.slug}) {!c.isActive ? '[Vô hiệu]' : ''}
                  </option>
                ))}
            </select>
          </div>

          <div className="form-actions">
            <button type="submit" disabled={isSubmitting} className="btn btn-primary">
              {isSubmitting ? 'Đang lưu...' : editingId ? 'Cập nhật' : 'Thêm danh mục'}
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
            <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Danh sách danh mục</h2>
            <AdminBadge variant="neutral">
              Tổng: <strong style={{ marginLeft: 4 }}>{categories.length}</strong> danh mục
            </AdminBadge>
          </div>
        }
      >
        {loading ? (
          <div className="admin-loading py-8 text-center text-slate-500">Đang tải danh mục...</div>
        ) : categories.length === 0 ? (
          <AdminEmptyState icon="🗂️" message="Không có danh mục nào." />
        ) : (
          <table className="admin-table admin-ds-table" aria-label="Danh sách danh mục" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th className="col-text text-left">Tên danh mục</th>
                <th className="col-text text-left">Slug</th>
                <th className="col-text text-left">Danh mục cha</th>
                <th className="col-status text-center">Trạng thái</th>
                <th className="col-actions text-right" style={{ width: 140 }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {categories.map(c => {
                const isItemBusy = actionBusyId === c.id || isSubmitting
                return (
                  <tr key={c.id} className={!c.isActive ? 'row-inactive' : ''}>
                    <td className="col-text text-left align-middle">
                      <strong className="text-slate-800 font-medium">{c.name}</strong>
                    </td>
                    <td className="col-text text-left align-middle">
                      <code className="admin-code-badge">{c.slug}</code>
                    </td>
                    <td className="col-text text-left align-middle text-slate-600">
                      {c.parentCategoryId ? categoryMap.get(c.parentCategoryId) || c.parentCategoryId : '—'}
                    </td>
                    <td className="col-status text-center align-middle">
                      <AdminBadge variant={c.isActive ? 'success' : 'danger'} dot className={`badge ${c.isActive ? 'badge-active' : 'badge-inactive'}`}>
                        {c.isActive ? 'Hoạt động' : 'Vô hiệu hóa'}
                      </AdminBadge>
                    </td>
                    <td className="col-actions text-right align-middle">
                      <div className="table-actions admin-table-actions">
                        <button
                          type="button"
                          onClick={() => handleStartEdit(c)}
                          disabled={isItemBusy}
                          className="btn btn-sm btn-secondary"
                        >
                          Sửa
                        </button>
                        {c.isActive ? (
                          <button
                            type="button"
                            onClick={() => handleOpenDeactivate(c)}
                            disabled={isItemBusy}
                            className="btn btn-sm btn-danger"
                          >
                            Vô hiệu hóa
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => handleRestore(c)}
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
        title="Vô hiệu hóa danh mục"
        message={`Bạn có chắc chắn muốn vô hiệu hóa danh mục "${deactivateDialog.category?.name}"?`}
        confirmLabel="Vô hiệu hóa"
        isBusy={deactivateDialog.isBusy}
        onCancel={() => setDeactivateDialog({ isOpen: false, category: null, isBusy: false })}
        onConfirm={handleConfirmDeactivate}
      />
    </div>
  )
}
