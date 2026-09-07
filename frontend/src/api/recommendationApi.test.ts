import { afterEach, expect, it, vi } from 'vitest'
import { recommendationApi } from './recommendationApi'

const SESSION_ID = '00000000-0000-4000-8000-000000000001'

function jsonResponse(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => vi.unstubAllGlobals())

it('recordView posts guest payload to the product view-events route', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse(null, 202))
  vi.stubGlobal('fetch', fetchMock)

  await recommendationApi.recordView('prod-1', { anonymousSessionId: SESSION_ID })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/products/prod-1/view-events',
    expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ anonymousSessionId: SESSION_ID }),
    }),
  )
})

it('recordView sends branch and bearer token for authenticated payload', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse(null, 202))
  vi.stubGlobal('fetch', fetchMock)

  await recommendationApi.recordView('prod-1', {
    anonymousSessionId: SESSION_ID,
    branchId: 'branch-1',
  }, 'jwt-token')

  const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  expect(init.headers).toMatchObject({ Authorization: 'Bearer jwt-token' })
  expect(init.body).toBe(JSON.stringify({
    anonymousSessionId: SESSION_ID,
    branchId: 'branch-1',
  }))
})

it('mergeSession posts the anonymous id to the session merge route with token', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ mergedCount: 2 }))
  vi.stubGlobal('fetch', fetchMock)

  const result = await recommendationApi.mergeSession(SESSION_ID, 'jwt-token')

  expect(result).toEqual({ mergedCount: 2 })
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/recommendations/session/merge',
    expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({ Authorization: 'Bearer jwt-token' }),
      body: JSON.stringify({ anonymousSessionId: SESSION_ID }),
    }),
  )
})

it('getRecommendations fetches homepage recommendations with branch and limit', async () => {
  const body = { sourceScope: 'Global', items: [{ productId: 'p1' }] }
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body))
  vi.stubGlobal('fetch', fetchMock)

  const result = await recommendationApi.getRecommendations({ branchId: 'b1', limit: 8 })

  expect(result).toEqual(body)
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/recommendations?branchId=b1&limit=8',
    expect.objectContaining({ headers: expect.objectContaining({ Accept: 'application/json' }) }),
  )
})

it('getRecommendations attaches the bearer token when signed in', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ items: [] }))
  vi.stubGlobal('fetch', fetchMock)

  await recommendationApi.getRecommendations({ token: 'jwt-token' })

  const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  expect(init.headers).toMatchObject({ Authorization: 'Bearer jwt-token' })
  expect(fetchMock).toHaveBeenCalledWith('/api/recommendations', expect.anything())
})

it('getProductRecommendations fetches the similar product shelf', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ items: [] }))
  vi.stubGlobal('fetch', fetchMock)

  await recommendationApi.getProductRecommendations('prod-1', { limit: 8 })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/products/prod-1/recommendations?limit=8',
    expect.anything(),
  )
})

it('getAdminSample fetches sample rows with scope filter', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ items: [] }))
  vi.stubGlobal('fetch', fetchMock)

  await recommendationApi.getAdminSample({ scope: 'Global', token: 'jwt-token' })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/admin/recommendations/results?scope=Global',
    expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer jwt-token' }),
    }),
  )
})

it('triggerRun posts the manual run request', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ jobRunId: 'r1', statusUrl: '/x' }, 202))
  vi.stubGlobal('fetch', fetchMock)

  const result = await recommendationApi.triggerRun('jwt-token')

  expect(result.jobRunId).toBe('r1')
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/admin/jobs/recommendations/runs',
    expect.objectContaining({ method: 'POST' }),
  )
})