import { useState, useEffect } from 'react'
import { adminApi } from '../../api/adminApi'
import { ApiError } from '../../api/httpClient'
import type { BranchDto } from '../../api/branchApi'
import { useAuth } from '../auth/AuthContext'
import './AdminInventoryPage.css'

interface Props {
  mode: 'create' | 'edit'
  branch?: BranchDto
  onClose: () => void
  onSaved: () => void
}

export function AdminBranchModal({ mode, branch, onClose, onSaved }: Props) {
  const { accessToken } = useAuth()
  const [name, setName] = useState(branch?.name ?? '')
  const [address, setAddress] = useState(branch?.address ?? '')
  const [phone, setPhone] = useState(branch?.phone ?? '')
  const [isActive, setIsActive] = useState(branch?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Sync form state when branch changes (handles React StrictMode double-render)
  useEffect(() => {
    setName(branch?.name ?? '')
    setAddress(branch?.address ?? '')
    setPhone(branch?.phone ?? '')
    setIsActive(branch?.isActive ?? true)
  }, [branch?.id])

  function validate(): string | null {
    if (!name.trim()) return 'Vui lòng nhập tên chi nhánh.'
    if (!address.trim()) return 'Vui lòng nhập địa chỉ.'
    return null
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const validationError = validate()
    if (validationError) {
      setError(validationError)
      return
    }
    if (!accessToken) return
    setSubmitting(true)
    setError(null)

    try {
      if (mode === 'create') {
        await adminApi.createBranch(
          { name: name.trim(), address: address.trim(), phone: phone.trim() || null },
          accessToken,
        )
      } else {
        await adminApi.updateBranch(
          branch!.id,
          { name: name.trim(), address: address.trim(), phone: phone.trim() || null, isActive },
          accessToken,
        )
      }
      onSaved()
    } catch (err) {
      if (err instanceof ApiError && err.data?.message) {
        setError(err.data.message)
      } else {
        setError('Không thể lưu. Vui lòng thử lại.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="admin-modal-overlay" onClick={onClose}>
      <div
        className="admin-modal"
        role="dialog"
        aria-modal="true"
        aria-label={mode === 'create' ? 'Tạo chi nhánh' : `Sửa chi nhánh ${branch?.name}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="admin-modal-header">
          <h2>{mode === 'create' ? 'Tạo chi nhánh' : `Sửa: ${branch?.name}`}</h2>
          <button type="button" className="admin-modal-close" onClick={onClose} aria-label="Đóng">×</button>
        </header>
        <form onSubmit={submit}>
          <div className="admin-field">
            <label htmlFor="branch-name">Tên chi nhánh</label>
            <input id="branch-name" type="text" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="admin-field">
            <label htmlFor="branch-address">Địa chỉ</label>
            <input id="branch-address" type="text" value={address} onChange={(e) => setAddress(e.target.value)} />
          </div>
          <div className="admin-field">
            <label htmlFor="branch-phone">Số điện thoại</label>
            <input id="branch-phone" type="text" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </div>
          {mode === 'edit' && (
            <div className="admin-field">
              <label htmlFor="branch-active">
                <input
                  id="branch-active"
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                />
                {' '}Đang hoạt động
              </label>
            </div>
          )}
          {error && <p className="admin-error" role="alert">{error}</p>}
          <div className="admin-modal-actions">
            <button type="button" className="admin-btn-secondary" onClick={onClose} disabled={submitting}>Hủy</button>
            <button type="submit" className="btn-primary" disabled={submitting}>
              {submitting ? 'Đang lưu...' : mode === 'create' ? 'Tạo chi nhánh' : 'Lưu thay đổi'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
