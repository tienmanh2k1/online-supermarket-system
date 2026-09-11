import { getJson, postJson } from './httpClient'

export type MockOutcome = 'Success' | 'Failed' | 'Cancelled'
export type MockPaymentDto = { paymentId: string; orderId: string; method: string; amount: number; paymentStatus: string; orderStatus: string; isMock: boolean }

export const mockPaymentApi = {
  get: (paymentId: string, token: string, signal?: AbortSignal) => getJson<MockPaymentDto>(`/payments/mock/${paymentId}`, { token, signal }),
  complete: (paymentId: string, outcome: MockOutcome, token: string, signal?: AbortSignal) => postJson<MockPaymentDto>(`/payments/mock/${paymentId}/complete`, { outcome }, { token, signal }),
}
