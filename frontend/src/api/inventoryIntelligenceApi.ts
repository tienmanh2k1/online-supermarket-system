import { getJson, postJson } from './httpClient'

export interface InventoryTransactionDto {
  id: string
  branchInventoryId: string
  transactionType: string
  quantityOnHandDelta: number
  reservedQuantityDelta: number
  quantityOnHandAfter: number
  reservedQuantityAfter: number
  referenceType: string
  referenceId: string | null
  actorUserId: string | null
  note: string | null
  createdAtUtc: string
}

export interface PaginatedInventoryTransactionsDto {
  data: InventoryTransactionDto[]
  totalCount: number
  page: number
  pageSize: number
}

export const inventoryIntelligenceApi = {
  getTransactions: (
    inventoryId: string,
    params: { page: number; pageSize: number },
    token?: string,
    signal?: AbortSignal,
  ) =>
    getJson<PaginatedInventoryTransactionsDto>(
      `/admin/inventory/${encodeURIComponent(inventoryId)}/transactions` +
        `?page=${params.page}&pageSize=${params.pageSize}`,
      { token, signal },
    ),

  getForecast: (branchId: string, horizonDays: number, options?: { token?: string; signal?: AbortSignal }) =>
    getJson<ForecastDto[]>(
      `/admin/forecast?branchId=${encodeURIComponent(branchId)}&horizonDays=${horizonDays}`,
      options,
    ),

  triggerForecast: (branchId: string, token?: string, signal?: AbortSignal) =>
    postJson<TriggerForecastRunResponse>(
      '/admin/jobs/forecast/runs',
      { branchId },
      { token, signal },
    ),

  getForecastRun: (runId: string, options?: { token?: string; signal?: AbortSignal }) =>
    getJson<ForecastJobRunDto>(
      `/admin/jobs/${encodeURIComponent(runId)}`,
      options,
    ),

  getForecastRuns: (branchId: string, options?: { token?: string; signal?: AbortSignal }) =>
    getJson<PaginatedJobRunsDto>(
      `/admin/jobs?jobName=Forecast&branchId=${encodeURIComponent(branchId)}&pageSize=20`,
      options,
    ),
}

export interface ForecastDto {
  id: string
  branchInventoryId: string
  productId: string
  productName: string
  horizonDays: number
  predictedQuantity: number
  actualDataDays: number
  dataQuality: string
  forecastStartDate: string
  forecastEndDate: string
  generatedAtUtc: string
  jobRunId: string
}

export interface TriggerForecastRunResponse {
  jobRunId: string
  statusUrl: string
}

export interface ForecastJobRunDto {
  id: string
  jobName: string
  status: string
  createdAtUtc: string
  startedAtUtc: string | null
  completedAtUtc: string | null
  errorSummary: string | null
}

export interface PaginatedJobRunsDto {
  items: ForecastJobRunDto[]
  totalCount: number
  page: number
  pageSize: number
}