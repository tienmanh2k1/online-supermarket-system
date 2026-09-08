import { useEffect, useState } from 'react'
import { recommendationApi, type RecommendationItemDto } from '../../api/recommendationApi'
import { useOptionalAuth } from '../auth/AuthContext'
import { RecommendationShelf } from './RecommendationShelf'

export interface RecommendationShelfLoaderProps {
  branchId?: string
  productId?: string
  token?: string
  limit?: number
  title?: string
}

interface LoadState {
  loading: boolean
  isError: boolean
  items: RecommendationItemDto[]
}

export function RecommendationShelfLoader({
  branchId,
  productId,
  token: explicitToken,
  limit = 8,
  title,
}: RecommendationShelfLoaderProps) {
  const auth = useOptionalAuth()
  const token = explicitToken ?? auth?.accessToken ?? undefined
  const [state, setState] = useState<LoadState>({ loading: true, isError: false, items: [] })
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setState({ loading: true, isError: false, items: [] })

    const request = productId
      ? recommendationApi.getProductRecommendations(productId, {
          branchId,
          limit,
          token,
          signal: controller.signal,
        })
      : recommendationApi.getRecommendations({ branchId, limit, token, signal: controller.signal })

    request
      .then((response) => {
        if (controller.signal.aborted) return
        setState({ loading: false, isError: false, items: response.items })
      })
      .catch(() => {
        if (controller.signal.aborted) return
        setState({ loading: false, isError: true, items: [] })
      })

    return () => controller.abort()
  }, [branchId, productId, token, limit, retryKey])

  return (
    <RecommendationShelf
      title={title ?? (productId ? 'Sản phẩm tương tự' : 'Gợi ý dành cho bạn')}
      contextLabel={productId ? 'Tương tự' : 'Vì sao'}
      items={state.items}
      loading={state.loading}
      isError={state.isError}
      branchId={branchId}
      onRetry={() => setRetryKey((k) => k + 1)}
    />
  )
}