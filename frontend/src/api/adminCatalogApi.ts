import { getJson, postJson, putJson, patchJson } from './httpClient'
import type { PaginatedResponse, PaginationMeta } from './catalogApi'

export type { PaginatedResponse, PaginationMeta }

export interface AdminCategoryDto {
  id: string
  name: string
  slug: string
  parentCategoryId: string | null
  isActive: boolean
}

export interface AdminBrandDto {
  id: string
  name: string
  slug: string
  isActive: boolean
}

export interface AdminProductDto {
  id: string
  categoryId: string
  categoryName: string
  brandId: string
  brandName: string
  sku: string
  name: string
  slug: string
  description: string | null
  basePrice: number
  unit: string
  imageUrl: string | null
  isActive: boolean
}

export interface UpsertCategoryRequest {
  name: string
  slug: string
  parentCategoryId: string | null
}

export interface UpsertBrandRequest {
  name: string
  slug: string
}

export interface UpsertProductRequest {
  categoryId: string
  brandId: string
  sku: string
  name: string
  slug: string
  description: string | null
  basePrice: number
  unit: string
  imageUrl: string | null
}

export interface UpdateCatalogStatusRequest {
  isActive: boolean
}

export interface AdminProductListParams {
  page?: number
  pageSize?: number
  search?: string
  categoryId?: string
  brandId?: string
  isActive?: boolean
}

export const adminCatalogApi = {
  // Categories
  getCategories: (token: string, signal?: AbortSignal) =>
    getJson<AdminCategoryDto[]>('/admin/catalog/categories', { token, signal }),

  createCategory: (req: UpsertCategoryRequest, token: string, signal?: AbortSignal) =>
    postJson<AdminCategoryDto>('/admin/catalog/categories', req, { token, signal }),

  updateCategory: (id: string, req: UpsertCategoryRequest, token: string, signal?: AbortSignal) =>
    putJson<AdminCategoryDto>(`/admin/catalog/categories/${encodeURIComponent(id)}`, req, { token, signal }),

  updateCategoryStatus: (id: string, req: UpdateCatalogStatusRequest, token: string, signal?: AbortSignal) =>
    patchJson<AdminCategoryDto>(`/admin/catalog/categories/${encodeURIComponent(id)}/status`, req, { token, signal }),

  // Brands
  getBrands: (token: string, signal?: AbortSignal) =>
    getJson<AdminBrandDto[]>('/admin/catalog/brands', { token, signal }),

  createBrand: (req: UpsertBrandRequest, token: string, signal?: AbortSignal) =>
    postJson<AdminBrandDto>('/admin/catalog/brands', req, { token, signal }),

  updateBrand: (id: string, req: UpsertBrandRequest, token: string, signal?: AbortSignal) =>
    putJson<AdminBrandDto>(`/admin/catalog/brands/${encodeURIComponent(id)}`, req, { token, signal }),

  updateBrandStatus: (id: string, req: UpdateCatalogStatusRequest, token: string, signal?: AbortSignal) =>
    patchJson<AdminBrandDto>(`/admin/catalog/brands/${encodeURIComponent(id)}/status`, req, { token, signal }),

  // Products
  getProducts: (
    params: AdminProductListParams | undefined,
    token: string,
    signal?: AbortSignal
  ): Promise<PaginatedResponse<AdminProductDto>> => {
    const searchParams = new URLSearchParams()
    if (params) {
      if (params.page !== undefined) searchParams.append('page', params.page.toString())
      if (params.pageSize !== undefined) searchParams.append('pageSize', params.pageSize.toString())
      if (params.search) searchParams.append('search', params.search)
      if (params.categoryId) searchParams.append('categoryId', params.categoryId)
      if (params.brandId) searchParams.append('brandId', params.brandId)
      if (params.isActive !== undefined) searchParams.append('isActive', params.isActive.toString())
    }
    const q = searchParams.toString()
    const path = q ? `/admin/catalog/products?${q}` : '/admin/catalog/products'
    return getJson<any>(path, { token, signal }).then((res) => {
      const list: AdminProductDto[] = res.data ?? res.items ?? []
      return {
        data: list,
        items: list,
        meta: res.meta,
      }
    })
  },

  createProduct: (req: UpsertProductRequest, token: string, signal?: AbortSignal) =>
    postJson<AdminProductDto>('/admin/catalog/products', req, { token, signal }),

  updateProduct: (id: string, req: UpsertProductRequest, token: string, signal?: AbortSignal) =>
    putJson<AdminProductDto>(`/admin/catalog/products/${encodeURIComponent(id)}`, req, { token, signal }),

  updateProductStatus: (id: string, req: UpdateCatalogStatusRequest, token: string, signal?: AbortSignal) =>
    patchJson<AdminProductDto>(`/admin/catalog/products/${encodeURIComponent(id)}/status`, req, { token, signal }),
}
