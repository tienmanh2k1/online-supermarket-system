import { getJson, postJson, type RequestOptions } from './httpClient'

export interface ProductSummaryDto {
  id: string
  name: string
  slug: string
  sku: string
  basePrice: number
  imageUrl: string | null
  categoryName: string
  brandName: string
}

export interface BranchInventoryDto {
  branchId: string
  sellingPrice: number
  availableQuantity: number
  onHand: number
}

export interface ProductDetailDto {
  id: string
  name: string
  slug: string
  sku: string
  description: string | null
  basePrice: number
  unit: string
  imageUrl: string | null
  categoryId: string
  categoryName: string
  brandId: string
  brandName: string
  branchInventory: BranchInventoryDto | null
}

export interface CategoryDto {
  id: string
  name: string
  slug: string
  parentCategoryId: string | null
  isActive: boolean
}

export interface BrandDto {
  id: string
  name: string
  slug: string
  isActive: boolean
}

export interface PaginationMeta {
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface PaginatedResponse<T> {
  data: T[]
  meta: PaginationMeta
}

export interface ProductListParams {
  categoryId?: string
  brandId?: string
  minPrice?: number
  maxPrice?: number
  branchId?: string
  search?: string
  page?: number
  pageSize?: number
}

export interface CreateProductInput {
  name: string
  sku: string
  basePrice: number
  unit: string
  categoryId: string
  brandId: string
  description?: string
  imageUrl?: string
}

export const catalogApi = {
  async getProducts(
    params?: ProductListParams,
    options?: RequestOptions | AbortSignal
  ): Promise<PaginatedResponse<ProductSummaryDto>> {
    const query = new URLSearchParams()
    if (params?.categoryId) query.set('categoryId', params.categoryId)
    if (params?.brandId) query.set('brandId', params.brandId)
    if (params?.minPrice !== undefined && !isNaN(params.minPrice)) query.set('minPrice', params.minPrice.toString())
    if (params?.maxPrice !== undefined && !isNaN(params.maxPrice)) query.set('maxPrice', params.maxPrice.toString())
    if (params?.branchId) query.set('branchId', params.branchId)
    if (params?.search && params.search.trim()) query.set('search', params.search.trim())
    if (params?.page && params.page > 0) query.set('page', params.page.toString())
    if (params?.pageSize && params.pageSize > 0) query.set('pageSize', params.pageSize.toString())

    const queryString = query.toString() ? `?${query.toString()}` : ''
    return getJson<PaginatedResponse<ProductSummaryDto>>(`/products${queryString}`, options)
  },

  async getProductById(id: string, branchId?: string, options?: RequestOptions | AbortSignal): Promise<ProductDetailDto> {
    const query = branchId ? `?branchId=${encodeURIComponent(branchId)}` : ''
    return getJson<ProductDetailDto>(`/products/${id}${query}`, options)
  },

  async createProduct(input: CreateProductInput, options?: RequestOptions): Promise<{ id: string; message: string }> {
    return postJson<{ id: string; message: string }>('/products', input, options)
  },

  async updateProduct(id: string, input: Partial<CreateProductInput>, options?: RequestOptions): Promise<{ message: string }> {
    return postJson<{ message: string }>(`/products/${id}`, input, options)
  },

  async deleteProduct(id: string, options?: RequestOptions): Promise<{ message: string }> {
    return postJson<{ message: string }>(`/products/${id}`, { isActive: false }, options)
  },

  async recordProductView(productId: string, userId?: string): Promise<void> {
    await postJson(`/products/${productId}/view`, { productId, userId })
  },

  async getRecommendations(productId?: string, limit = 6): Promise<ProductSummaryDto[]> {
    const query = productId ? `?productId=${productId}&limit=${limit}` : `?limit=${limit}`
    return getJson<ProductSummaryDto[]>(`/recommendations${query}`)
  },

  async getCategories(options?: RequestOptions | AbortSignal): Promise<CategoryDto[]> {
    return getJson<CategoryDto[]>('/categories', options)
  },

  async getBrands(options?: RequestOptions | AbortSignal): Promise<BrandDto[]> {
    return getJson<BrandDto[]>('/brands', options)
  },
}
