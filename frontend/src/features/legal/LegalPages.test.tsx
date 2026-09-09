import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { PrivacyPage } from './PrivacyPage'
import { TermsPage } from './TermsPage'
import { AppShell } from '../../app/AppShell'

vi.mock('../compare/CompareHeaderLink', () => ({ CompareHeaderLink: () => null }))
vi.mock('../cart/CartHeaderLink', () => ({ CartHeaderLink: () => null }))
vi.mock('../auth/UserMenu', () => ({ UserMenu: () => null }))
vi.mock('../system/ApiStatus', () => ({ ApiStatus: () => null }))
vi.mock('../products/BranchNavMenu', () => ({ BranchNavMenu: () => null }))

describe('Legal Pages', () => {
  it('renders PrivacyPage describing account, address, orders, and sandbox payment', () => {
    render(
      <MemoryRouter>
        <PrivacyPage />
      </MemoryRouter>
    )

    expect(
      screen.getByRole('heading', { name: /chính sách bảo mật/i })
    ).toBeInTheDocument()

    // Assert coverage of required areas
    expect(screen.getByRole('heading', { name: /tài khoản/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /địa chỉ/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /đơn hàng/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /thanh toán/i })).toBeInTheDocument()
    expect(screen.getAllByText(/sandbox/i).length).toBeGreaterThan(0)
  })

  it('renders TermsPage describing account, ordering, and sandbox limitations', () => {
    render(
      <MemoryRouter>
        <TermsPage />
      </MemoryRouter>
    )

    expect(
      screen.getByRole('heading', { name: /điều khoản dịch vụ/i })
    ).toBeInTheDocument()

    // Assert coverage of required areas
    expect(screen.getByRole('heading', { name: /tài khoản/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /đặt hàng/i })).toBeInTheDocument()
    expect(screen.getAllByText(/sandbox/i).length).toBeGreaterThan(0)
  })

  it('renders footer links to internal /privacy and /terms routes in AppShell', () => {
    render(
      <MemoryRouter>
        <AppShell>
          <div>Main Content</div>
        </AppShell>
      </MemoryRouter>
    )

    const privacyLink = screen.getByRole('link', { name: /chính sách bảo mật/i })
    expect(privacyLink).toBeInTheDocument()
    expect(privacyLink).toHaveAttribute('href', '/privacy')

    const termsLink = screen.getByRole('link', { name: /điều khoản/i })
    expect(termsLink).toBeInTheDocument()
    expect(termsLink).toHaveAttribute('href', '/terms')
  })
})
