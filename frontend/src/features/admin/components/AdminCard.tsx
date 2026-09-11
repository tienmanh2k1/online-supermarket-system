import React from 'react'
import '../AdminDesignSystem.css'

interface AdminCardProps {
  toolbar?: React.ReactNode
  footer?: React.ReactNode
  children: React.ReactNode
  className?: string
  noTableWrapper?: boolean
}

export function AdminCard({
  toolbar,
  footer,
  children,
  className = '',
  noTableWrapper = false,
}: AdminCardProps) {
  return (
    <div className={`admin-card-container bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden ${className}`}>
      {toolbar && (
        <div className="admin-card-toolbar border-b border-slate-100 p-4">
          {toolbar}
        </div>
      )}
      {noTableWrapper ? (
        children
      ) : (
        <div className="admin-table-wrap overflow-x-auto">
          {children}
        </div>
      )}
      {footer}
    </div>
  )
}
