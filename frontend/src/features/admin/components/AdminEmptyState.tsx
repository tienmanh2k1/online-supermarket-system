import React from 'react'
import '../AdminDesignSystem.css'

interface AdminEmptyStateProps {
  icon?: React.ReactNode
  message?: string
  description?: string
  action?: React.ReactNode
  colSpan?: number
  className?: string
}

export function AdminEmptyState({
  icon = '🔍',
  message = 'Không tìm thấy dữ liệu phù hợp',
  description,
  action,
  colSpan,
  className = '',
}: AdminEmptyStateProps) {
  const content = (
    <div className={`admin-empty-state py-12 text-center ${className}`}>
      <div className="admin-empty-state-icon text-3xl mb-2 opacity-60" aria-hidden="true">
        {icon}
      </div>
      <p className="admin-empty-state-text font-medium text-slate-600 text-base">{message}</p>
      {description && <p className="text-sm text-slate-400 mt-1">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )

  if (colSpan !== undefined) {
    return (
      <tr>
        <td colSpan={colSpan} style={{ padding: 0, textAlign: 'center', border: 'none' }}>
          {content}
        </td>
      </tr>
    )
  }

  return content
}
