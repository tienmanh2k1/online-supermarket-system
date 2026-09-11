import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { branchApi, type BranchDto } from '../../api/branchApi'
import './BranchNavMenu.css'

/**
 * Header nav item for branches. The label links to the full /branches page;
 * hovering (or keyboard-focusing) it reveals a quick dropdown of branches.
 * The list is loaded once when the header mounts (the header is mounted for
 * the whole app, so this is a single request at startup).
 */
export function BranchNavMenu() {
  const navigate = useNavigate()
  const location = useLocation()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const isActive = location.pathname.startsWith('/branches')

  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .getBranches({ signal: controller.signal })
      .then(setBranches)
      .catch(() => {
        // Header menu is non-critical; ignore load failures.
      })
    return () => controller.abort()
  }, [])

  function handleSelect(branchId: string) {
    navigate(`/browse?branchId=${encodeURIComponent(branchId)}`)
  }

  return (
    <div className="branch-nav">
      <Link
        to="/branches"
        className={`branch-nav__label ${isActive ? 'branch-nav__label--active' : ''}`}
        aria-haspopup="menu"
      >
        Chi nhánh
      </Link>
      <div className="branch-nav__menu" role="menu" aria-label="Danh sách chi nhánh">
        {branches.length === 0 ? (
          <span className="branch-nav__hint">Đang tải chi nhánh...</span>
        ) : (
          branches.map((branch) => (
            <button
              key={branch.id}
              type="button"
              role="menuitem"
              className="branch-nav__item"
              onClick={() => handleSelect(branch.id)}
            >
              <span className="branch-nav__item-name">{branch.name}</span>
              <span className="branch-nav__item-addr">{branch.address}</span>
            </button>
          ))
        )}
      </div>
    </div>
  )
}
