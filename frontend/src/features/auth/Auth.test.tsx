import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as authApi from '../../api/authApi'
import { AuthProvider } from './AuthContext'
import { LoginForm } from './LoginForm'
import { RegisterForm } from './RegisterForm'
import { AuthModal } from './AuthModal'
import { UserMenu } from './UserMenu'

describe('Auth Feature', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  describe('LoginForm', () => {
    it('shows validation error when submitted empty', async () => {
      render(
        <AuthProvider>
          <LoginForm onSwitchToRegister={() => {}} />
        </AuthProvider>
      )

      const submitButton = screen.getByRole('button', { name: 'Đăng nhập' })
      fireEvent.click(submitButton)

      expect(await screen.findByText('Vui lòng nhập đầy đủ email và mật khẩu.')).toBeInTheDocument()
    })

    it('submits credentials and calls login on valid input', async () => {
      const mockAuthResponse: authApi.AuthResponse = {
        accessToken: 'mock_jwt_token',
        refreshToken: 'mock_refresh_token',
        expiresInSeconds: 900,
        user: {
          id: 'user-1',
          email: 'test@example.com',
          fullName: 'Nguyen Van A',
          role: 'Customer',
        },
      }

      vi.spyOn(authApi, 'loginApi').mockResolvedValue(mockAuthResponse)
      const onSuccess = vi.fn()

      render(
        <AuthProvider>
          <LoginForm onSuccess={onSuccess} onSwitchToRegister={() => {}} />
        </AuthProvider>
      )

      fireEvent.change(screen.getByLabelText('Địa chỉ Email'), {
        target: { value: 'test@example.com' },
      })
      fireEvent.change(screen.getByLabelText('Mật khẩu'), {
        target: { value: 'Password@123' },
      })

      fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

      await waitFor(() => {
        expect(authApi.loginApi).toHaveBeenCalledWith({
          email: 'test@example.com',
          password: 'Password@123',
        })
        expect(onSuccess).toHaveBeenCalled()
      })
    })

    it('triggers onForgotPassword when "Quên mật khẩu?" button is clicked', () => {
      const onForgotPassword = vi.fn()
      render(
        <AuthProvider>
          <LoginForm onSwitchToRegister={() => {}} onForgotPassword={onForgotPassword} />
        </AuthProvider>
      )

      const forgotBtn = screen.getByRole('button', { name: /quên mật khẩu/i })
      fireEvent.click(forgotBtn)
      expect(onForgotPassword).toHaveBeenCalledTimes(1)
    })
  })

  describe('ForgotPasswordFlow in AuthModal', () => {
    it('switches to forgot password mode, submits email and shows neutral message', async () => {
      vi.spyOn(authApi, 'requestPasswordResetApi').mockResolvedValue({
        message: 'If the email exists, a password reset link has been sent.',
      })

      render(
        <AuthProvider>
          <AuthModal isOpen={true} onClose={() => {}} />
        </AuthProvider>
      )

      // Click "Quên mật khẩu?" inside login form
      const forgotBtn = screen.getByRole('button', { name: /quên mật khẩu/i })
      fireEvent.click(forgotBtn)

      // Heading or title for forgot password
      expect(await screen.findByRole('heading', { name: /khôi phục mật khẩu/i })).toBeInTheDocument()

      // Submit empty first to check validation
      const submitBtn = screen.getByRole('button', { name: /gửi/i })
      fireEvent.click(submitBtn)
      expect(await screen.findByText(/vui lòng nhập/i)).toBeInTheDocument()

      // Fill email and submit
      const emailInput = screen.getByLabelText(/địa chỉ email/i)
      fireEvent.change(emailInput, { target: { value: 'user@example.com' } })
      fireEvent.click(submitBtn)

      await waitFor(() => {
        expect(authApi.requestPasswordResetApi).toHaveBeenCalledWith('user@example.com')
      })

      // Check neutral confirmation message
      const successMessage = await screen.findByText('Nếu email tồn tại, chúng tôi đã gửi liên kết đặt lại mật khẩu.')
      expect(successMessage).toBeInTheDocument()
      // Verify message does not disclose account existence
      expect(screen.queryByText(/tài khoản không tồn tại/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/tìm thấy tài khoản/i)).not.toBeInTheDocument()
    })
  })

  describe('RegisterForm', () => {
    it('shows error when passwords do not match', async () => {
      render(
        <AuthProvider>
          <RegisterForm onSwitchToLogin={() => {}} />
        </AuthProvider>
      )

      fireEvent.change(screen.getByLabelText('Họ và tên *'), {
        target: { value: 'Nguyen Van B' },
      })
      fireEvent.change(screen.getByLabelText('Địa chỉ Email *'), {
        target: { value: 'test2@example.com' },
      })
      fireEvent.change(screen.getByLabelText('Mật khẩu *'), {
        target: { value: 'Password@123' },
      })
      fireEvent.change(screen.getByLabelText('Xác nhận mật khẩu *'), {
        target: { value: 'DifferentPassword' },
      })

      fireEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))

      expect(await screen.findByText('Mật khẩu xác nhận không khớp.')).toBeInTheDocument()
    })

    it('submits registration on valid input', async () => {
      const mockRegisterResponse: authApi.RegisterResponse = {
        id: 'user-2',
        email: 'test2@example.com',
        fullName: 'Nguyen Van B',
        role: 'Customer',
      }
      const mockAuthResponse: authApi.AuthResponse = {
        accessToken: 'token',
        refreshToken: 'refresh',
        expiresInSeconds: 900,
        user: mockRegisterResponse,
      }

      vi.spyOn(authApi, 'registerApi').mockResolvedValue(mockRegisterResponse)
      vi.spyOn(authApi, 'loginApi').mockResolvedValue(mockAuthResponse)
      const onSuccess = vi.fn()

      render(
        <AuthProvider>
          <RegisterForm onSuccess={onSuccess} onSwitchToLogin={() => {}} />
        </AuthProvider>
      )

      fireEvent.change(screen.getByLabelText('Họ và tên *'), {
        target: { value: 'Nguyen Van B' },
      })
      fireEvent.change(screen.getByLabelText('Địa chỉ Email *'), {
        target: { value: 'test2@example.com' },
      })
      fireEvent.change(screen.getByLabelText('Mật khẩu *'), {
        target: { value: 'Password@123' },
      })
      fireEvent.change(screen.getByLabelText('Xác nhận mật khẩu *'), {
        target: { value: 'Password@123' },
      })

      fireEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))

      await waitFor(() => {
        expect(authApi.registerApi).toHaveBeenCalled()
        expect(onSuccess).toHaveBeenCalled()
      })
    })
  })

  describe('UserMenu', () => {
    it('renders guest login/register buttons when not authenticated', async () => {
      render(
        <MemoryRouter>
          <AuthProvider>
            <UserMenu />
          </AuthProvider>
        </MemoryRouter>
      )

      expect(await screen.findByRole('button', { name: 'Đăng nhập' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Đăng ký' })).toBeInTheDocument()
    })

    it('renders user details and logout button when authenticated', async () => {
      localStorage.setItem('os_access_token', 'valid_token')
      vi.spyOn(authApi, 'getMeApi').mockResolvedValue({
        id: 'user-1',
        email: 'admin@example.com',
        fullName: 'Admin User',
        role: 'Admin',
      })

      render(
        <MemoryRouter>
          <AuthProvider>
            <UserMenu />
          </AuthProvider>
        </MemoryRouter>
      )

      expect(await screen.findByText('Admin User')).toBeInTheDocument()
      expect(screen.getByText('Quản trị viên')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Đăng xuất' })).toBeInTheDocument()
    })

    it('shows an order history link for authenticated users', async () => {
      localStorage.setItem('os_access_token', 'valid_token')
      vi.spyOn(authApi, 'getMeApi').mockResolvedValue({
        id: 'user-1',
        email: 'admin@example.com',
        fullName: 'Admin User',
        role: 'Admin',
      })

      render(
        <MemoryRouter>
          <AuthProvider>
            <UserMenu />
          </AuthProvider>
        </MemoryRouter>
      )

      expect(await screen.findByRole('link', { name: 'Đơn hàng' })).toHaveAttribute('href', '/orders/history')
    })
  })
})
