import { useState, useEffect } from 'react'
import { LoginForm } from './LoginForm'
import { RegisterForm } from './RegisterForm'
import { ForgotPasswordForm } from './ForgotPasswordForm'

interface AuthModalProps {
  isOpen: boolean
  initialMode?: 'login' | 'register' | 'forgot'
  onClose: () => void
}

export function AuthModal({ isOpen, initialMode = 'login', onClose }: AuthModalProps) {
  const [mode, setMode] = useState<'login' | 'register' | 'forgot'>(initialMode)

  useEffect(() => {
    setMode(initialMode)
  }, [initialMode, isOpen])

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose()
      }
    }
    if (isOpen) {
      window.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }
    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'unset'
    }
  }, [isOpen, onClose])

  if (!isOpen) return null

  return (
    <div className="auth-modal-overlay" onClick={onClose} role="dialog" aria-modal="true">
      <div className="auth-modal" onClick={(e) => e.stopPropagation()}>
        <button
          type="button"
          className="auth-modal__close-btn"
          onClick={onClose}
          aria-label="Đóng cửa sổ xác thực"
        >
          ×
        </button>

        <div className="auth-modal__tabs">
          <button
            type="button"
            className={`auth-modal__tab ${mode === 'login' ? 'auth-modal__tab--active' : ''}`}
            onClick={() => setMode('login')}
          >
            Đăng nhập
          </button>
          <button
            type="button"
            className={`auth-modal__tab ${mode === 'register' ? 'auth-modal__tab--active' : ''}`}
            onClick={() => setMode('register')}
          >
            Đăng ký
          </button>
          {mode === 'forgot' && (
            <button
              type="button"
              className="auth-modal__tab auth-modal__tab--active"
            >
              Khôi phục mật khẩu
            </button>
          )}
        </div>

        <div className="auth-modal__content">
          {mode === 'login' ? (
            <LoginForm
              onSuccess={onClose}
              onSwitchToRegister={() => setMode('register')}
              onForgotPassword={() => setMode('forgot')}
            />
          ) : mode === 'register' ? (
            <RegisterForm
              onSuccess={onClose}
              onSwitchToLogin={() => setMode('login')}
            />
          ) : (
            <ForgotPasswordForm
              onBackToLogin={() => setMode('login')}
            />
          )}
        </div>
      </div>
    </div>
  )
}
