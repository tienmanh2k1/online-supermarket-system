import { afterEach, expect, it, vi } from 'vitest'
import { inventoryIntelligenceApi, type PaginatedInventoryTransactionsDto } from './inventoryIntelligenceApi'

function jsonResponse(data: unknown) {
  return new Response(JSON.stringify(data), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => vi.unstubAllGlobals())

const token = 'jwt-token'
const response: PaginatedInventoryTransactionsDto = {
  data: [{
    id: '00000000-0000-0000-0000-000000000001',
    branchInventoryId: '00000000-0000-0000-0000-000000000101',
    transactionType: 'Sale',
    quantityOnHandDelta: -3,
    reservedQuantityDelta: -3,
    quantityOnHandAfter: 97,
    reservedQuantityAfter: 0,
    referenceType: 'Order',
    referenceId: '00000000-0000-0000-0000-000000000201',
    actorUserId: null,
    note: null,
    createdAtUtc: '2026-09-03T01:00:00Z',
  }],
  totalCount: 1,
  page: 1,
  pageSize: 20,
}

it('loads transactions with bearer token and page params', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse(response))
  vi.stubGlobal('fetch', fetchMock)

  await inventoryIntelligenceApi.getTransactions(
    '00000000-0000-0000-0000-000000000101',
    { page: 2, pageSize: 10 },
    token,
  )

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/admin/inventory/00000000-0000-0000-0000-000000000101/transactions?page=2&pageSize=10',
    expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer jwt-token' }),
    }),
  )
})

it('encodes inventory id for the ledger route', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(response)))

  await inventoryIntelligenceApi.getTransactions('inv 1', { page: 1, pageSize: 20 }, token)

  expect(fetch).toHaveBeenCalledWith('/api/admin/inventory/inv%201/transactions?page=1&pageSize=20', expect.any(Object))
})

it('loads forecast rows for a branch and horizon', async () => {
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse([]))
  vi.stubGlobal('fetch', fetchMock)

  await inventoryIntelligenceApi.getForecast('b-1', 14, { token })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/admin/forecast?branchId=b-1&horizonDays=14',
    expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer jwt-token' }),
    }),
  )
})

it('triggers a forecast run with the branch id', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(
      jsonResponse({ jobRunId: 'job-1', statusUrl: '/api/admin/jobs/job-1' }),
    )
  vi.stubGlobal('fetch', fetchMock)

  await inventoryIntelligenceApi.triggerForecast('b-1', token)

  const [, init] = fetchMock.mock.calls[0]
  expect(init).toMatchObject({
    method: 'POST',
    body: JSON.stringify({ branchId: 'b-1' }),
    headers: {
      Authorization: 'Bearer jwt-token',
      'Content-Type': 'application/json',
    },
  })
})