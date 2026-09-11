import { render } from '@testing-library/react'
import { MemoryRouter, Routes, Route, Link } from 'react-router-dom'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { ScrollToTop } from './ScrollToTop'

describe('ScrollToTop Component', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    window.scrollTo = vi.fn()
  })

  it('scrolls window to top (0, 0) on initial route render without hash', () => {
    render(
      <MemoryRouter initialEntries={['/product/123']}>
        <ScrollToTop />
      </MemoryRouter>
    )

    expect(window.scrollTo).toHaveBeenCalledWith({
      top: 0,
      left: 0,
      behavior: 'instant',
    })
  })

  it('does not scroll to (0, 0) if a hash anchor is present', () => {
    render(
      <MemoryRouter initialEntries={['/product/123#reviews']}>
        <ScrollToTop />
      </MemoryRouter>
    )

    expect(window.scrollTo).not.toHaveBeenCalled()
  })
})
