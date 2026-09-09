import { getJson, postJson } from './httpClient'

export interface UserDto {
  id: string
  email: string
  fullName: string
  phone?: string | null
  role: string
}

export interface RegisterRequest {
  email: string
  password: string
  fullName: string
  phone?: string | null
}

export interface RegisterResponse {
  id: string
  email: string
  fullName: string
  phone?: string | null
  role: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresInSeconds: number
  user: UserDto
}

export interface RefreshTokenRequest {
  refreshToken: string
}

export interface LogoutRequest {
  refreshToken?: string | null
}

export function registerApi(data: RegisterRequest, signal?: AbortSignal): Promise<RegisterResponse> {
  return postJson<RegisterResponse>('/auth/register', data, { signal })
}

export function loginApi(data: LoginRequest, signal?: AbortSignal): Promise<AuthResponse> {
  return postJson<AuthResponse>('/auth/login', data, { signal })
}

export function refreshTokenApi(refreshToken: string, signal?: AbortSignal): Promise<AuthResponse> {
  return postJson<AuthResponse>('/auth/refresh', { refreshToken }, { signal })
}

export function logoutApi(refreshToken?: string | null, signal?: AbortSignal): Promise<{ message: string }> {
  return postJson<{ message: string }>('/auth/logout', { refreshToken }, { signal })
}

export function getMeApi(accessToken: string, signal?: AbortSignal): Promise<UserDto> {
  return getJson<UserDto>('/auth/me', { token: accessToken, signal })
}

export function requestPasswordResetApi(email: string, signal?: AbortSignal): Promise<{ message: string }> {
  return postJson<{ message: string }>('/auth/password-reset', { email }, { signal })
}

