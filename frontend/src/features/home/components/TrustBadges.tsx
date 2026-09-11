import type { TrustBadgeItem } from '../types'

interface TrustBadgesProps {
  badges: TrustBadgeItem[]
}

export function TrustBadges({ badges }: TrustBadgesProps) {
  return (
    <section
      className="home-perks appliance-trust-badges bg-white rounded-2xl border border-slate-200 shadow-sm"
      data-testid="home-trust-strip"
      aria-label="Cam kết dịch vụ điện máy"
    >
      <ul className="home-perks__container appliance-trust-badges__grid">
        {badges.map((item) => (
          <li key={item.id} className="home-perk-item appliance-trust-badge">
            <div className="home-perk-item__icon appliance-trust-badge__icon" aria-hidden="true">
              {item.icon}
            </div>
            <div className="home-perk-item__text appliance-trust-badge__text">
              <strong className="appliance-trust-badge__title">{item.title}</strong>
              <small className="appliance-trust-badge__sub">{item.subtitle}</small>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
