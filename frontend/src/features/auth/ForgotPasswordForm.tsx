import React, { useState } from 'react'
import { requestPasswordResetApi } from '../../api/authApi'

interface ForgotPasswordFormProps {
  onBackToLogin: () => void
}

export function ForgotPasswordForm({ onBackToLogin }: ForgotPasswordFormProps) {
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!email.trim()) {
      setError('Vui lòng nhập địa chỉ email.')
      return
    }

    setIsSubmitting(true)
    try {
      await requestPasswordResetApi(email.trim())
      setSubmitted(true)
    } catch (err: any) {
      setError(err?.message || 'Có lỗi xảy ra. Vui lòng thử lại sau.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit} noValidate>
      <h3 className="auth-form__title">Khôi phục mật khẩu</h3>
      <p className="auth-form__subtitle">
        Nhập địa chỉ email của bạn để nhận liên kết đặt lại mật khẩu
      </p>

      {error && (
        <div className="auth-form__alert auth-form__alert--error" role="alert">
          {error}
        </div>
      )}

      {submitted ? (
        <div className="auth-form__alert auth-form__alert--success" role="status">
          Nếu email tồn tại, chúng tôi đã gửi liên kết đặt lại mật khẩu.
        </div>
      ) : (
        <>
          <div className="form-group">
            <label htmlFor="forgot-email">Địa chỉ Email</label>
            <input
              id="forgot-email"
              type="email"
              autoComplete="email"
              placeholder="name@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
              required
            />
          </div>

          <button type="submit" className="btn-primary auth-submit-btn" disabled={isSubmitting}>
            {isSubmitting ? 'Đang gửi yêu cầu...' : 'Gửi liên kết đặt lại mật khẩu'}
          </button>
        </>
      )}

      <div className="auth-form__footer">
        <button
          type="button"
          className="btn-link"
          onClick={onBackToLogin}
          disabled={isSubmitting}
        >
          Quay lại đăng nhập
        </button>
      </div>
    </form>
  )
}
