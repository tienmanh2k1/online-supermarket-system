import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as authApi from '../../api/authApi'
import { ApiError } from '../../api/httpClient'
import { ResetPasswordPage } from './ResetPasswordPage'

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('does not call API and shows invalid link error when token is missing', async () => {
    const confirmSpy = vi.spyOn(authApi, 'confirmPasswordResetApi')

    render(
      <MemoryRouter initialEntries={['/reset-password']}>
        <Routes>
          <Route path="/reset-password" element={<ResetPasswordPage />} />
        </Routes>
      </MemoryRouter>
    )

    expect(
      screen.getByText(/liên kết không hợp lệ hoặc thiếu mã xác thực/i)
    ).toBeInTheDocument()
    expect(screen.queryByLabelText(/mật khẩu mới/i)).not.toBeInTheDocument()
    expect(confirmSpy).not.toHaveBeenCalled()
  })

  it('shows client error when passwords do not match and does not call API', async () => {
    const confirmSpy = vi.spyOn(authApi, 'confirmPasswordResetApi')

    render(
      <MemoryRouter initialEntries={['/reset-password?token=valid-token-123']}>
        <Routes>
          <Route path="/reset-password" element={<ResetPasswordPage />} />
        </Routes>
      </MemoryRouter>
    )

    fireEvent.change(screen.getByLabelText(/^mật khẩu mới/i), {
      target: { value: 'Password@123' },
    })
    fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
      target: { value: 'Password@456' },
    })

    fireEvent.click(screen.getByRole('button', { name: /đặt lại mật khẩu/i }))

    expect(await screen.findByText('Mật khẩu xác nhận không khớp.')).toBeInTheDocument()
    expect(confirmSpy).not.toHaveBeenCalled()
  })

  it('calls confirmPasswordResetApi with token and password, then displays success and back links', async () => {
    const confirmSpy = vi
      .spyOn(authApi, 'confirmPasswordResetApi')
      .mockResolvedValue({ message: 'Password has been reset successfully.' })

    render(
      <MemoryRouter initialEntries={['/reset-password?token=test-reset-token']}>
        <Routes>
          <Route path="/reset-password" element={<ResetPasswordPage />} />
        </Routes>
      </MemoryRouter>
    )

    fireEvent.change(screen.getByLabelText(/^mật khẩu mới/i), {
      target: { value: 'NewSecret@123' },
    })
    fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
      target: { value: 'NewSecret@123' },
    })

    fireEvent.click(screen.getByRole('button', { name: /đặt lại mật khẩu/i }))

    await waitFor(() => {
      expect(confirmSpy).toHaveBeenCalledWith('test-reset-token', 'NewSecret@123')
    })

    expect(
      await screen.findByText(/đặt lại mật khẩu thành công/i)
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /về trang chủ/i })).toBeInTheDocument()
  })

  it('handles 400 error as "Liên kết không hợp lệ hoặc đã hết hạn."', async () => {
    vi.spyOn(authApi, 'confirmPasswordResetApi').mockRejectedValue(
      new ApiError(400, { message: 'INVALID_TOKEN' })
    )

    render(
      <MemoryRouter initialEntries={['/reset-password?token=expired-token']}>
        <Routes>
          <Route path="/reset-password" element={<ResetPasswordPage />} />
        </Routes>
      </MemoryRouter>
    )

    fireEvent.change(screen.getByLabelText(/^mật khẩu mới/i), {
      target: { value: 'NewSecret@123' },
    })
    fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
      target: { value: 'NewSecret@123' },
    })

    fireEvent.click(screen.getByRole('button', { name: /đặt lại mật khẩu/i }))

    expect(
      await screen.findByText('Liên kết không hợp lệ hoặc đã hết hạn.')
    ).toBeInTheDocument()
  })
})
