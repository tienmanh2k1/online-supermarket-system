import React, { useState } from 'react'
import { useAuth } from './AuthContext'

interface RegisterFormProps {
  onSuccess?: () => void
  onSwitchToLogin: () => void
}

export function RegisterForm({ onSuccess, onSwitchToLogin }: RegisterFormProps) {
  const { register } = useAuth()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!fullName.trim() || !email.trim() || !phone.trim() || !password) {
      setError('Vui lòng điền đầy đủ các thông tin bắt buộc.')
      return
    }

    const phoneRegex = /^(0[3|5|7|8|9])[0-9]{8}$/
    if (!phoneRegex.test(phone.trim())) {
      setError('Số điện thoại không hợp lệ. Vui lòng nhập 10 số bắt đầu bằng 0.')
      return
    }

    if (password.length < 6) {
      setError('Mật khẩu phải có ít nhất 6 ký tự.')
      return
    }

    if (password !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.')
      return
    }

    setIsSubmitting(true)
    try {
      await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        password,
      })
      onSuccess?.()
    } catch (err: any) {
      if (err.status === 409) {
        setError('Email này đã được đăng ký tài khoản. Vui lòng đăng nhập hoặc dùng email khác.')
      } else {
        setError(err.message || 'Đăng ký thất bại. Vui lòng thử lại.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit} noValidate>
      <h3 className="auth-form__title">Đăng ký tài khoản</h3>
      <p className="auth-form__subtitle">Tạo tài khoản để mua sắm dễ dàng tại mọi chi nhánh</p>

      {error && (
        <div className="auth-form__alert auth-form__alert--error" role="alert">
          {error}
        </div>
      )}

      <div className="form-group">
        <label htmlFor="reg-fullname">Họ và tên *</label>
        <input
          id="reg-fullname"
          type="text"
          autoComplete="name"
          placeholder="Nguyễn Văn A"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="form-group">
        <label htmlFor="reg-email">Địa chỉ Email *</label>
        <input
          id="reg-email"
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
        <label htmlFor="reg-phone">Số điện thoại *</label>
        <input
          id="reg-phone"
          type="tel"
          autoComplete="tel"
          placeholder="0901234567"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="form-group">
        <label htmlFor="reg-password">Mật khẩu *</label>
        <input
          id="reg-password"
          type="password"
          autoComplete="new-password"
          placeholder="Tối thiểu 6 ký tự"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="form-group">
        <label htmlFor="reg-confirm-password">Xác nhận mật khẩu *</label>
        <input
          id="reg-confirm-password"
          type="password"
          autoComplete="new-password"
          placeholder="Nhập lại mật khẩu"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          disabled={isSubmitting}
          required
        />
      </div>

      <button type="submit" className="btn-primary auth-submit-btn" disabled={isSubmitting}>
        {isSubmitting ? 'Đang tạo tài khoản...' : 'Đăng ký'}
      </button>

      <div className="auth-form__footer">
        <span>Đã có tài khoản? </span>
        <button
          type="button"
          className="btn-link"
          onClick={onSwitchToLogin}
          disabled={isSubmitting}
        >
          Đăng nhập ngay
        </button>
      </div>
    </form>
  )
}
