import { Link } from 'react-router-dom'

export interface QuickCategoryItem {
  id: string
  name: string
  icon: string
  href: string
  highlight?: string
}

export const KANGAROO_QUICK_CATEGORIES: QuickCategoryItem[] = [
  {
    id: 'smart-tivi',
    name: 'Smart Tivi OLED & 4K',
    icon: '📺',
    href: '/browse?category=tivi-loa',
    highlight: 'Rạp phim tại gia',
  },
  {
    id: 'refrigerator',
    name: 'Tủ Lạnh Side-By-Side',
    icon: '❄️',
    href: '/browse?category=tu-lanh',
    highlight: 'Inverter tiết kiệm',
  },
  {
    id: 'washing-machine',
    name: 'Máy Giặt Lồng Ngang',
    icon: '🧺',
    href: '/browse?category=may-giat',
    highlight: 'AI diệt khuẩn 99%',
  },
  {
    id: 'water-purifier',
    name: 'Máy Lọc Nước Kangaroo',
    icon: '🚰',
    href: '/browse?search=Kangaroo',
    highlight: 'Hydrogen chính hãng',
  },
  {
    id: 'kitchen-appliance',
    name: 'Gia Dụng & Bếp Nướng',
    icon: '🍳',
    href: '/browse?category=gia-dung',
    highlight: 'Tiện nghi tổ ấm',
  },
  {
    id: 'air-conditioner',
    name: 'Điều Hòa & Quạt Lạnh',
    icon: '💨',
    href: '/browse?category=dieu-hoa',
    highlight: 'Lọc khí ion âm',
  },
]

export function QuickCategoryStrip() {
  return (
    <section
      className="kg-quick-categories"
      data-testid="home-quick-categories"
      aria-label="Danh mục sản phẩm nổi bật Kangaroo"
    >
      <div className="kg-quick-categories__container">
        <div className="kg-quick-categories__box">
          <div className="kg-quick-categories__track">
            {KANGAROO_QUICK_CATEGORIES.map((cat) => (
              <Link key={cat.id} to={cat.href} className="kg-quick-category">
                <span className="kg-quick-category__icon" aria-hidden="true">
                  {cat.icon}
                </span>
                <div className="kg-quick-category__content">
                  <strong className="kg-quick-category__title">{cat.name}</strong>
                  {cat.highlight && (
                    <small className="kg-quick-category__sub">{cat.highlight}</small>
                  )}
                </div>
              </Link>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
