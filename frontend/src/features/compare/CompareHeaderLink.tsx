import { useCompare } from './CompareContext'
import './CompareHeaderLink.css'

export function CompareHeaderLink() {
  const { compareProducts, openModal } = useCompare()
  const count = compareProducts.length

  return (
    <button
      type="button"
      className="compare-header-link relative inline-flex items-center gap-2"
      onClick={openModal}
      aria-label={`So sánh sản phẩm${count > 0 ? ` (${count} sản phẩm)` : ''}`}
    >
      <span className="compare-header-link__icon-container relative inline-flex items-center justify-center">
        <span aria-hidden="true" className="compare-header-link__icon">
          ⇄
        </span>
        {count > 0 && (
          <span
            className="compare-header-link__badge absolute top-[-6px] right-[-10px] bg-amber-500 text-white text-[11px] font-bold h-5 w-5 rounded-full flex items-center justify-center shadow-sm pointer-events-none"
            aria-label={count + ' sản phẩm đang so sánh'}
          >
            {count}
          </span>
        )}
      </span>
      <span>So sánh</span>
    </button>
  )
}