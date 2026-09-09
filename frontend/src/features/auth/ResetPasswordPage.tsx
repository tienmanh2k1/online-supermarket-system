import React, { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { confirmPasswordResetApi } from '../../api/authApi'
import { ApiError } from '../../api/httpClient'
import './ResetPasswordPage.css'

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isSuccess, setIsSuccess] = useState(false)

  if (!token) {
    return (
      <div className="reset-password-page">
        <h1 className="reset-password-page__title">Đặt lại mật khẩu</h1>
        <div className="auth-form__alert auth-form__alert--error" role="alert">
          Liên kết không hợp lệ hoặc thiếu mã xác thực.
        </div>
        <div className="reset-password-page__actions">
          <Link to="/" className="reset-password-page__back-link">
            Về trang chủ
          </Link>
        </div>
      </div>
    )
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!newPassword || !confirmPassword) {
      setError('Vui lòng nhập đầy đủ mật khẩu mới và xác nhận mật khẩu.')
      return
    }

    if (newPassword !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.')
      return
    }

    setIsSubmitting(true)
    try {
      await confirmPasswordResetApi(token, newPassword)
      setIsSuccess(true)
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 400) {
        setError('Liên kết không hợp lệ hoặc đã hết hạn.')
      } else if (err instanceof Error) {
        setError(err.message || 'Đặt lại mật khẩu thất bại. Vui lòng thử lại.')
      } else {
        setError('Đặt lại mật khẩu thất bại. Vui lòng thử lại.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isSuccess) {
    return (
      <div className="reset-password-page">
        <h1 className="reset-password-page__title">Đặt lại mật khẩu</h1>
        <div className="auth-form__alert auth-form__alert--success" role="status">
          Đặt lại mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới.
        </div>
        <div className="reset-password-page__actions">
          <Link to="/" className="btn-primary auth-submit-btn" style={{ display: 'block', textAlign: 'center', textDecoration: 'none' }}>
            Về trang chủ
          </Link>
        </div>
      </div>
    )
  }

  return (
    <div className="reset-password-page">
      <h1 className="reset-password-page__title">Đặt lại mật khẩu</h1>
      <p className="reset-password-page__subtitle">
        Nhập mật khẩu mới cho tài khoản của bạn để hoàn tất quá trình khôi phục.
      </p>

      {error && (
        <div className="auth-form__alert auth-form__alert--error" role="alert">
          {error}
        </div>
      )}

      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        <div className="form-group">
          <label htmlFor="new-password">Mật khẩu mới</label>
          <input
            id="new-password"
            type="password"
            autoComplete="new-password"
            placeholder="••••••••"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            disabled={isSubmitting}
            required
          />
        </div>

        <div className="form-group">
          <label htmlFor="confirm-password">Xác nhận mật khẩu mới</label>
          <input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            disabled={isSubmitting}
            required
          />
        </div>

        <button type="submit" className="btn-primary auth-submit-btn" disabled={isSubmitting}>
          {isSubmitting ? 'Đang cập nhật...' : 'Đặt lại mật khẩu'}
        </button>

        <div className="reset-password-page__actions">
          <Link to="/" className="reset-password-page__back-link">
            Về trang chủ
          </Link>
        </div>
      </form>
    </div>
  )
}
