import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

/**
 * Global Scroll Restoration component.
 * Automatically scrolls viewport to top (0, 0) upon any route navigation,
 * while preserving smooth hash navigation if an in-page anchor (#reviews, #roadmap, etc.) is present.
 */
export function ScrollToTop() {
  const { pathname, hash } = useLocation()

  useEffect(() => {
    if (!hash) {
      if (typeof window !== 'undefined' && typeof window.scrollTo === 'function') {
        window.scrollTo({ top: 0, left: 0, behavior: 'instant' as ScrollBehavior })
      }
      if (typeof document !== 'undefined') {
        if (document.documentElement) document.documentElement.scrollTop = 0
        if (document.body) document.body.scrollTop = 0
      }
    }
  }, [pathname, hash])

  return null
}
