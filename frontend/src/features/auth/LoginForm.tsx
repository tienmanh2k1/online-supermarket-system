import React, { useState } from 'react'
import { useAuth } from './AuthContext'

interface LoginFormProps {
  onSuccess?: () => void
  onSwitchToRegister: () => void
  onForgotPassword?: () => void
}

export function LoginForm({ onSuccess, onSwitchToRegister, onForgotPassword }: LoginFormProps) {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!email.trim() || !password) {
      setError('Vui lòng nhập đầy đủ email và mật khẩu.')
      return
    }

    setIsSubmitting(true)
    try {
      await login({ email: email.trim(), password })
      onSuccess?.()
    } catch (err: any) {
      if (err.status === 401) {
        setError('Email hoặc mật khẩu không chính xác.')
      } else {
        setError(err.message || 'Đăng nhập thất bại. Vui lòng thử lại.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit} noValidate>
      <h3 className="auth-form__title">Đăng nhập tài khoản</h3>
      <p className="auth-form__subtitle">Truy cập để quản lý giỏ hàng và đơn hàng của bạn</p>

      {error && (
        <div className="auth-form__alert auth-form__alert--error" role="alert">
          {error}
        </div>
      )}

      <div className="form-group">
        <label htmlFor="login-email">Địa chỉ Email</label>
        <input
          id="login-email"
          type="email"
          autoComplete="email"
          placeholder="name@example.com"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="form-group">
        <label htmlFor="login-password">Mật khẩu</label>
        <input
          id="login-password"
          type="password"
          autoComplete="current-password"
          placeholder="••••••••"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="auth-form__forgot-row">
        <button
          type="button"
          className="btn-link"
          onClick={onForgotPassword}
          disabled={isSubmitting}
        >
          Quên mật khẩu?
        </button>
      </div>

      <button type="submit" className="btn-primary auth-submit-btn" disabled={isSubmitting}>
        {isSubmitting ? 'Đang xử lý...' : 'Đăng nhập'}
      </button>

      <div className="auth-form__footer">
        <span>Chưa có tài khoản? </span>
        <button
          type="button"
          className="btn-link"
          onClick={onSwitchToRegister}
          disabled={isSubmitting}
        >
          Đăng ký ngay
        </button>
      </div>
    </form>
  )
}
