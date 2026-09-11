import React, { useState, useEffect } from 'react'
import type { AddressDto, CreateAddressRequest, UpdateAddressRequest } from '../../api/addressApi'

interface AddressModalProps {
  isOpen: boolean
  initialData?: AddressDto | null
  onClose: () => void
  onSave: (data: CreateAddressRequest | UpdateAddressRequest) => Promise<void>
}

export function AddressModal({ isOpen, initialData, onClose, onSave }: AddressModalProps) {
  const [recipientName, setRecipientName] = useState('')
  const [phone, setPhone] = useState('')
  const [street, setStreet] = useState('')
  const [ward, setWard] = useState('')
  const [district, setDistrict] = useState('')
  const [city, setCity] = useState('')

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isEdit = !!initialData

  useEffect(() => {
    if (initialData) {
      setRecipientName(initialData.recipientName)
      setPhone(initialData.phone)
      setStreet(initialData.street)
      setWard(initialData.ward)
      setDistrict(initialData.district)
      setCity(initialData.city)
    } else {
      setRecipientName('')
      setPhone('')
      setStreet('')
      setWard('')
      setDistrict('')
      setCity('')
    }
    setError(null)
  }, [initialData, isOpen])

  if (!isOpen) return null

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!recipientName.trim() || !phone.trim() || !street.trim() || !ward.trim() || !district.trim() || !city.trim()) {
      setError('Vui lòng điền đầy đủ tất cả các trường bắt buộc (*).')
      return
    }

    setLoading(true)
    try {
      await onSave({
        recipientName: recipientName.trim(),
        phone: phone.trim(),
        street: street.trim(),
        ward: ward.trim(),
        district: district.trim(),
        city: city.trim(),
      })
      onClose()
    } catch (err: any) {
      setError(err.message || 'Lỗi khi lưu địa chỉ. Vui lòng kiểm tra lại.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-modal-overlay" role="dialog" aria-modal="true" aria-labelledby="address-modal-title">
      <div className="auth-modal address-modal">
        <div className="auth-modal__header">
          <h2 id="address-modal-title">
            {isEdit ? '✏️ Chỉnh Sửa Địa Chỉ Giao Hàng' : '➕ Thêm Địa Chỉ Nhận Hàng Mới'}
          </h2>
          <button
            type="button"
            className="auth-modal__close"
            onClick={onClose}
            aria-label="Đóng"
            data-testid="modal-close-btn"
          >
            &times;
          </button>
        </div>

        {error && (
          <div className="alert alert--error" role="alert">
            <span>⚠</span> {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="account-form" data-testid="address-form">
          <div className="form-row-2">
            <div className="form-group">
              <label htmlFor="addr-recipient">Họ và tên người nhận *</label>
              <input
                id="addr-recipient"
                type="text"
                required
                value={recipientName}
                onChange={(e) => setRecipientName(e.target.value)}
                placeholder="VD: Nguyễn Văn An"
                className="input-field"
                data-testid="input-recipient"
              />
            </div>
            <div className="form-group">
              <label htmlFor="addr-phone">Số điện thoại *</label>
              <input
                id="addr-phone"
                type="tel"
                required
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="VD: 0912345678"
                className="input-field"
                data-testid="input-addr-phone"
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="addr-street">Địa chỉ số nhà, tên đường *</label>
            <input
              id="addr-street"
              type="text"
              required
              value={street}
              onChange={(e) => setStreet(e.target.value)}
              placeholder="VD: 123 Lê Lợi"
              className="input-field"
              data-testid="input-street"
            />
          </div>

          <div className="form-row-2">
            <div className="form-group">
              <label htmlFor="addr-ward">Phường / Xã *</label>
              <input
                id="addr-ward"
                type="text"
                required
                value={ward}
                onChange={(e) => setWard(e.target.value)}
                placeholder="VD: Phường Bến Nghé"
                className="input-field"
                data-testid="input-ward"
              />
            </div>
            <div className="form-group">
              <label htmlFor="addr-district">Quận / Huyện *</label>
              <input
                id="addr-district"
                type="text"
                required
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
                placeholder="VD: Quận 1"
                className="input-field"
                data-testid="input-district"
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="addr-city">Tỉnh / Thành phố *</label>
            <input
              id="addr-city"
              type="text"
              required
              value={city}
              onChange={(e) => setCity(e.target.value)}
              placeholder="VD: TP. Hồ Chí Minh"
              className="input-field"
              data-testid="input-city"
            />
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn-cancel"
              onClick={onClose}
              disabled={loading}
            >
              Hủy Bỏ
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={loading}
              data-testid="btn-save-address"
            >
              {loading ? 'Đang lưu...' : isEdit ? 'Cập Nhật Địa Chỉ' : 'Thêm Địa Chỉ'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
