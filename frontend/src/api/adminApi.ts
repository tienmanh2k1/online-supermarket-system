import { getJson, postJson, putJson } from './httpClient'
import type { OrderDetailDto, PaginatedOrdersDto } from './orderApi'
import type { BranchDto, BranchProductInventoryDto } from './branchApi'

export interface CreateBranchBody {
  name: string
  address: string
  phone?: string | null
}

export interface UpdateBranchBody {
  name: string
  address: string
  phone: string | null
  isActive: boolean
}

export interface PromotionDto {
  id: string
  code: string
  discountType: string
  discountValue: number
  minOrderAmount: number
  usageLimit: number | null
  usageCount: number
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface PaginatedPromotionsDto {
  page: number
  pageSize: number
  totalCount: number
  promotions: PromotionDto[]
}

export interface CreatePromotionBody {
  code: string
  discountType: string
  discountValue: number
  minOrderAmount?: number
  usageLimit?: number | null
}

export interface UpdatePromotionBody {
  discountValue: number
  minOrderAmount: number
  usageLimit: number | null
  isActive: boolean
}

export interface InventoryAdjustmentBody {
  productId: string
  quantityOnHand: number
  sellingPrice?: number
  reorderLevel?: number
  reason?: string
}

export interface DashboardSummaryDto {
  totalOrders: number
  pendingOrders: number
  completedRevenue: number
  lowStockItems: number
  recentOrders: Array<{
    id: string
    createdAtUtc: string
    totalAmount: number
    status: string
    fulfillmentType: string
    itemCount: number
  }>
}

export interface UserSummaryDto {
  id: string
  email: string
  fullName: string
  phone: string | null
  role: string
  status: string
  createdAtUtc: string
  updatedAtUtc: string
}

// Note: the admin users endpoint returns { page, pageSize, totalCount, users },
// which is a different shape from the orders endpoint ({ data, ... }).
export interface PaginatedUsersDto {
  page: number
  pageSize: number
  totalCount: number
  users: UserSummaryDto[]
}

export interface GetAllOrdersParams {
  status?: string
  userId?: string
  page?: number
  pageSize?: number
}

function buildAdminOrdersQuery(params: GetAllOrdersParams) {
  const search = new URLSearchParams()
  search.set('page', String(params.page ?? 1))
  search.set('pageSize', String(params.pageSize ?? 10))
  if (params.status) search.set('status', params.status)
  if (params.userId) search.set('userId', params.userId)
  return search.toString()
}

export const adminApi = {
  // Dashboard summary
  getDashboardSummary: (token: string, signal?: AbortSignal) =>
    getJson<DashboardSummaryDto>('/admin/dashboard/summary', { token, signal }),

  // Users
  listUsers: (page: number, pageSize: number, token: string, signal?: AbortSignal) =>
    getJson<PaginatedUsersDto>(`/admin/users?page=${page}&pageSize=${pageSize}`, { token, signal }),
  updateUserStatus: (id: string, status: string, token: string, signal?: AbortSignal) =>
    putJson<{ message: string }>(`/admin/users/${encodeURIComponent(id)}/status`, { status }, { token, signal }),

  // Orders
  listOrders: (params: GetAllOrdersParams, token: string, signal?: AbortSignal) =>
    getJson<PaginatedOrdersDto>('/admin/orders?' + buildAdminOrdersQuery(params), { token, signal }),
  getOrderById: (id: string, token: string, signal?: AbortSignal) =>
    getJson<OrderDetailDto>('/admin/orders/' + encodeURIComponent(id), { token, signal }),
  updateOrderStatus: (
    id: string,
    body: { status: string; note?: string },
    token: string,
    signal?: AbortSignal,
  ) =>
    putJson<OrderDetailDto>('/admin/orders/' + encodeURIComponent(id) + '/status', body, { token, signal }),

  // Inventory & price (branch-scoped). Note: this only UPDATES an existing
  // (branch, product) inventory row; the backend returns 404 if none exists.
  adjustInventory: (
    branchId: string,
    body: InventoryAdjustmentBody,
    token: string,
    signal?: AbortSignal,
  ) =>
    putJson<BranchProductInventoryDto>(
      `/admin/branches/${encodeURIComponent(branchId)}/inventory`,
      body,
      { token, signal },
    ),

  // Branches (admin — includes inactive)
  listBranches: (token: string, signal?: AbortSignal) =>
    getJson<BranchDto[]>('/admin/branches', { token, signal }),
  createBranch: (body: CreateBranchBody, token: string, signal?: AbortSignal) =>
    postJson<BranchDto>('/admin/branches', body, { token, signal }),
  updateBranch: (id: string, body: UpdateBranchBody, token: string, signal?: AbortSignal) =>
    putJson<BranchDto>(`/admin/branches/${encodeURIComponent(id)}`, body, { token, signal }),

  // Promotions
  listPromotions: (page: number, pageSize: number, token: string, signal?: AbortSignal) =>
    getJson<PaginatedPromotionsDto>(`/admin/promotions?page=${page}&pageSize=${pageSize}`, { token, signal }),
  createPromotion: (body: CreatePromotionBody, token: string, signal?: AbortSignal) =>
    postJson<PromotionDto>('/admin/promotions', body, { token, signal }),
  updatePromotion: (id: string, body: UpdatePromotionBody, token: string, signal?: AbortSignal) =>
    putJson<PromotionDto>(`/admin/promotions/${encodeURIComponent(id)}`, body, { token, signal }),
}
