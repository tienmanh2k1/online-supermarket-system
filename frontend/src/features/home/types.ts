export interface ApplianceSpec {
  key: string
  value: string
}

export type CategoryTabSlug =
  | 'all'
  | 'tivi-loa'
  | 'tu-lanh'
  | 'may-giat'
  | 'dieu-hoa'
  | 'gia-dung'
  | 'dien-tu'

export type KnownCategorySlug = CategoryTabSlug

export interface ApplianceProduct {
  id: string
  name: string
  modelNumber: string
  brand: string
  slug: string
  imageUrl: string
  originalPrice: number
  salePrice: number
  discountPercent: number
  installmentBadge?: string // Ví dụ: "Trả góp 0%"
  energyRating?: number // Ví dụ: 5 sao
  giftNote?: string // Ví dụ: "Tặng Nồi chiên không dầu 1.200.000đ"
  specs: ApplianceSpec[] // Thông số tóm tắt: kích thước màn hình, dung tích, công nghệ Inverter
  stockProgress: {
    sold: number
    total: number
  }
  categorySlug: CategoryTabSlug
  isHotDeal?: boolean
}

export interface CategoryMenuItem {
  id: string
  name: string
  slug: CategoryTabSlug
  icon: string
  badgeText?: string
  subcategories?: string[]
}

export interface HeroBanner {
  id: string
  title: string
  subtitle: string
  tagline?: string
  imageUrl: string
  linkUrl: string
  alt: string
}

export interface SubBanner {
  id: string
  title: string
  badge: string
  highlight: string
  imageUrl: string
  linkUrl: string
  alt: string
}

export interface TrustBadgeItem {
  id: string
  icon: string
  title: string
  subtitle: string
}

export interface CategoryTabItem {
  id: string
  name: string
  slug: CategoryTabSlug
}
