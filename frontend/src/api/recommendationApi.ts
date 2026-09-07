import { getJson, postJson } from './httpClient'

export interface RecordProductViewRequest {
  anonymousSessionId: string
  branchId?: string
}

export interface MergeSessionResponse {
  mergedCount: number
}

export interface RecommendationItemDto {
  productId: string
  name: string
  slug: string
  imageUrl?: string | null
  price: number
  availableQuantity?: number | null
  score: number
  reason: string
}

export interface RecommendationResponse {
  sourceScope?: string | null
  generatedAtUtc?: string | null
  items: RecommendationItemDto[]
}

export interface RecommendationSampleItemDto {
  productId: string
  scope: string
  audienceKey: string
  score: number
  rank: number
  reason: string
}

export interface RecommendationSampleResponse {
  jobRunId: string
  generatedAtUtc: string
  expiresAtUtc: string
  algorithmVersion: string
  items: RecommendationSampleItemDto[]
}

export interface RecommendationQuery {
  branchId?: string
  limit?: number
  token?: string
  signal?: AbortSignal
}

export const recommendationApi = {
  recordView: (
    productId: string,
    request: RecordProductViewRequest,
    token?: string,
    signal?: AbortSignal,
  ) =>
    postJson<unknown>(
      `/products/${encodeURIComponent(productId)}/view-events`,
      request,
      { token, signal },
    ),

  mergeSession: (anonymousSessionId: string, token: string, signal?: AbortSignal) =>
    postJson<MergeSessionResponse>(
      '/recommendations/session/merge',
      { anonymousSessionId },
      { token, signal },
    ),

  getRecommendations: (options: RecommendationQuery = {}) => {
    const params = new URLSearchParams()
    if (options.branchId) params.set('branchId', options.branchId)
    if (options.limit) params.set('limit', String(options.limit))
    const query = params.toString()
    const suffix = query ? `?${query}` : ''
    return getJson<RecommendationResponse>(`/recommendations${suffix}`, {
      token: options.token,
      signal: options.signal,
    })
  },

  getProductRecommendations: (productId: string, options: RecommendationQuery = {}) => {
    const params = new URLSearchParams()
    if (options.branchId) params.set('branchId', options.branchId)
    if (options.limit) params.set('limit', String(options.limit))
    const query = params.toString()
    const suffix = query ? `?${query}` : ''
    return getJson<RecommendationResponse>(
      `/products/${encodeURIComponent(productId)}/recommendations${suffix}`,
      { token: options.token, signal: options.signal },
    )
  },

  getAdminSample: (options: { scope?: string; limit?: number; token: string; signal?: AbortSignal }) => {
    const params = new URLSearchParams()
    if (options.scope) params.set('scope', options.scope)
    if (options.limit) params.set('limit', String(options.limit))
    const query = params.toString()
    const suffix = query ? `?${query}` : ''
    return getJson<RecommendationSampleResponse>(`/admin/recommendations/results${suffix}`, {
      token: options.token,
      signal: options.signal,
    })
  },

  triggerRun: (token: string, signal?: AbortSignal) =>
    postJson<{ jobRunId: string; statusUrl: string }>(
      '/admin/jobs/recommendations/runs',
      {},
      { token, signal },
    ),
}