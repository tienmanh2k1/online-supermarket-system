import React from 'react'
import '../AdminDesignSystem.css'

interface AdminPaginationProps {
  currentPage: number
  totalPages: number
  totalCount?: number
  itemName?: string
  onPageChange: (page: number) => void
  infoText?: string
  className?: string
}

export function AdminPagination({
  currentPage,
  totalPages,
  totalCount,
  itemName = 'kết quả',
  onPageChange,
  infoText,
  className = '',
}: AdminPaginationProps) {
  if (totalPages <= 1 && totalCount === undefined) {
    return null
  }

  const defaultInfo = totalCount !== undefined
    ? `Trang ${currentPage} / ${totalPages || 1} (${totalCount.toLocaleString('vi-VN')} ${itemName})`
    : `Trang ${currentPage} / ${totalPages || 1}`

  return (
    <div className={`admin-table-footer flex items-center justify-between p-4 bg-white border-t border-slate-100 ${className}`}>
      <div className="admin-pagination-info text-sm text-slate-500 font-medium">
        {infoText || defaultInfo}
      </div>
      {totalPages > 1 && (
        <div className="admin-pagination-nav flex items-center gap-2">
          <button
            type="button"
            disabled={currentPage <= 1}
            onClick={() => onPageChange(Math.max(1, currentPage - 1))}
            className="admin-pagination-btn btn btn-sm btn-secondary"
            aria-label="Trang trước"
          >
            Trang trước
          </button>
          <button
            type="button"
            disabled={currentPage >= totalPages}
            onClick={() => onPageChange(Math.min(totalPages, currentPage + 1))}
            className="admin-pagination-btn btn btn-sm btn-secondary"
            aria-label="Trang sau"
          >
            Trang sau
          </button>
        </div>
      )}
    </div>
  )
}
