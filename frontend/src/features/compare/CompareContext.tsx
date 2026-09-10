import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react'

export interface CompareProduct {
  id: string
  categoryId: string
  categoryName: string
  categorySlug: string
}

export interface CompareContextValue {
  compareProducts: CompareProduct[]
  isInCompare: (id: string) => boolean
  addToCompare: (product: CompareProduct) => boolean
  removeFromCompare: (id: string) => void
  clearCompare: () => void
  canAddMore: boolean
  hasProduct: boolean
  getDifferentCategoryWarning: (product: CompareProduct) => string | null
  openModal: () => void
  closeModal: () => void
  isModalOpen: boolean
}

const CompareContext = createContext<CompareContextValue | null>(null)

const MAX_COMPARE_PRODUCTS = 4
const COMPARE_STORAGE_KEY = 'aptechmart_compare_products'

const UNCATEGORIZED_SLUG = 'uncategorized'

function getCompareBlockReason(
  existing: CompareProduct | undefined,
  candidate: CompareProduct,
): string | null {
  if (candidate.categorySlug === UNCATEGORIZED_SLUG ||
      existing?.categorySlug === UNCATEGORIZED_SLUG) {
    return 'Sản phẩm chưa được phân loại nên chưa thể so sánh.'
  }
  if (existing && existing.categoryId !== candidate.categoryId) {
    return 'Chỉ có thể so sánh các sản phẩm cùng loại.'
  }
  return null
}

function loadFromStorage(): CompareProduct[] {
  try {
    const stored = localStorage.getItem(COMPARE_STORAGE_KEY)
    if (stored) {
      const parsed = JSON.parse(stored)
      if (Array.isArray(parsed)) {
        return parsed.filter(
          (p): p is CompareProduct =>
            typeof p === 'object' &&
            typeof p.id === 'string' &&
            typeof p.categoryId === 'string' &&
            typeof p.categoryName === 'string' &&
            typeof p.categorySlug === 'string'
        )
      }
    }
  } catch {
    // Invalid data, ignore
  }
  return []
}

function saveToStorage(products: CompareProduct[]) {
  try {
    if (products.length === 0) {
      localStorage.removeItem(COMPARE_STORAGE_KEY)
    } else {
      localStorage.setItem(COMPARE_STORAGE_KEY, JSON.stringify(products))
    }
  } catch {
    // Storage full or unavailable, ignore
  }
}

export function CompareProvider({ children }: PropsWithChildren) {
  const initialProducts = loadFromStorage()
  const [compareProducts, setCompareProducts] = useState<CompareProduct[]>(initialProducts)
  const [isModalOpen, setIsModalOpen] = useState(false)
  const compareRef = useRef<CompareProduct[]>(initialProducts)

  const updateCompare = useCallback((next: CompareProduct[]) => {
    compareRef.current = next
    setCompareProducts(next)
    saveToStorage(next)
  }, [])

  // Listen for global open event
  useEffect(() => {
    const handleOpenModal = () => setIsModalOpen(true)
    window.addEventListener('open-compare-modal', handleOpenModal)
    return () => window.removeEventListener('open-compare-modal', handleOpenModal)
  }, [])

  const openModal = useCallback(() => setIsModalOpen(true), [])
  const closeModal = useCallback(() => setIsModalOpen(false), [])

  const isInCompare = useCallback(
    (id: string) => compareProducts.some((p) => p.id === id),
    [compareProducts]
  )

  const getDifferentCategoryWarning = useCallback(
    (product: CompareProduct): string | null =>
      getCompareBlockReason(compareProducts[0], product),
    [compareProducts]
  )

  const addToCompare = useCallback(
    (product: CompareProduct): boolean => {
      const current = compareRef.current
      if (current.some((p) => p.id === product.id)) return false
      if (current.length >= MAX_COMPARE_PRODUCTS) return false
      if (getCompareBlockReason(current[0], product) !== null) return false
      updateCompare([...current, product])
      return true
    },
    [updateCompare]
  )

  const removeFromCompare = useCallback(
    (id: string) => {
      updateCompare(compareRef.current.filter((p) => p.id !== id))
    },
    [updateCompare]
  )

  const clearCompare = useCallback(() => {
    updateCompare([])
  }, [updateCompare])

  const value: CompareContextValue = {
    compareProducts,
    isInCompare,
    addToCompare,
    removeFromCompare,
    clearCompare,
    canAddMore: compareRef.current.length < MAX_COMPARE_PRODUCTS,
    hasProduct: compareRef.current.length > 0,
    getDifferentCategoryWarning,
    openModal,
    closeModal,
    isModalOpen,
  }

  return (
    <CompareContext.Provider value={value}>{children}</CompareContext.Provider>
  )
}

export function useCompare(): CompareContextValue {
  const context = useContext(CompareContext)
  if (!context) {
    throw new Error('useCompare must be used within a CompareProvider')
  }
  return context
}
