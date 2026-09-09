import { render } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Hero3DShowcase } from './Hero3DShowcase'

describe('Hero3DShowcase', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('falls back without WebGL and cancels its animation frame on unmount', () => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null)
    const cancel = vi.spyOn(window, 'cancelAnimationFrame')
    const { container, unmount } = render(<Hero3DShowcase />)

    expect(container.querySelector('[data-testid="hero-3d-fallback"]')).toBeInTheDocument()
    const canvas = container.querySelector('canvas')
    expect(canvas).toHaveAttribute('aria-hidden', 'true')

    unmount()
    expect(cancel).toHaveBeenCalled()
  })
})
