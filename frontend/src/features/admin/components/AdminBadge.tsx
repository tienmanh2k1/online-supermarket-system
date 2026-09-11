import React from 'react'
import '../AdminDesignSystem.css'

export type AdminBadgeVariant = 'success' | 'warning' | 'danger' | 'neutral' | 'info'

interface AdminBadgeProps {
  variant?: AdminBadgeVariant
  dot?: boolean
  children: React.ReactNode
  className?: string
  role?: string
}

export function AdminBadge({
  variant = 'neutral',
  dot = false,
  children,
  className = '',
  role,
}: AdminBadgeProps) {
  const variantClass = `admin-badge-${variant}`

  return (
    <span className={`admin-status-pill ${variantClass} ${className}`} role={role}>
      {dot && <span className="admin-badge-dot" aria-hidden="true" />}
      {children}
    </span>
  )
}
