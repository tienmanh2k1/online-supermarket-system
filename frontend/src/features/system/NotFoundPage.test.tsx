import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { NotFoundPage } from './NotFoundPage'

describe('NotFoundPage', () => {
  it('renders heading "Không tìm thấy trang", link to / and link to /products', () => {
    render(
      <MemoryRouter initialEntries={['/duong-dan-khong-ton-tai']}>
        <Routes>
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </MemoryRouter>
    )

    expect(
      screen.getByRole('heading', { name: /không tìm thấy trang/i })
    ).toBeInTheDocument()

    const homeLink = screen.getByRole('link', { name: /về trang chủ/i })
    expect(homeLink).toBeInTheDocument()
    expect(homeLink).toHaveAttribute('href', '/')

    const productsLink = screen.getByRole('link', { name: /xem sản phẩm/i })
    expect(productsLink).toBeInTheDocument()
    expect(productsLink).toHaveAttribute('href', '/products')
  })
})
