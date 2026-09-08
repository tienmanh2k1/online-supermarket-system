import React, { createContext, useContext, useEffect, useState, useCallback, type PropsWithChildren } from 'react'
import {
  loginApi,
  registerApi,
  refreshTokenApi,
  logoutApi,
  getMeApi,
  type UserDto,
  type LoginRequest,
  type RegisterRequest,
} from '../../api/authApi'
import { recommendationApi } from '../../api/recommendationApi'
import {
  getOrCreateAnonymousSessionId,
  rotateAnonymousSessionId,
} from '../recommendations/recommendationSession'

interface AuthContextType {
  user: UserDto | null
  accessToken: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (data: LoginRequest) => Promise<void>
  register: (data: RegisterRequest) => Promise<void>
  logout: () => Promise<void>
  updateUser: (updatedFields: Partial<UserDto>) => void
  refreshUser: () => Promise<void>
}

const ACCESS_TOKEN_KEY = 'os_access_token'
const REFRESH_TOKEN_KEY = 'os_refresh_token'

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [accessToken, setAccessToken] = useState<string | null>(() => localStorage.getItem(ACCESS_TOKEN_KEY))
  const [isLoading, setIsLoading] = useState(true)

  const clearAuth = useCallback(() => {
    setUser(null)
    setAccessToken(null)
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
  }, [])

  const setAuthTokens = useCallback((token: string, refresh: string, userData: UserDto) => {
    setAccessToken(token)
    setUser(userData)
    localStorage.setItem(ACCESS_TOKEN_KEY, token)
    localStorage.setItem(REFRESH_TOKEN_KEY, refresh)
  }, [])

  const mergeAnonymousViewSession = useCallback((token: string) => {
    const anonymousSessionId = getOrCreateAnonymousSessionId()
    recommendationApi
      .mergeSession(anonymousSessionId, token)
      .then(() => rotateAnonymousSessionId())
      .catch(() => {
        // keep the old anonymous id so a later authenticated init retries the merge
      })
  }, [])

  useEffect(() => {
    const initAuth = async () => {
      const storedAccessToken = localStorage.getItem(ACCESS_TOKEN_KEY)
      const storedRefreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)

      if (!storedAccessToken && !storedRefreshToken) {
        setIsLoading(false)
        return
      }

      if (storedAccessToken) {
        try {
          const userData = await getMeApi(storedAccessToken)
          setUser(userData)
          setAccessToken(storedAccessToken)
          mergeAnonymousViewSession(storedAccessToken)
          setIsLoading(false)
          return
        } catch {
          // Access token might be expired, try refreshing
        }
      }

      if (storedRefreshToken) {
        try {
          const authRes = await refreshTokenApi(storedRefreshToken)
          setAuthTokens(authRes.accessToken, authRes.refreshToken, authRes.user)
          mergeAnonymousViewSession(authRes.accessToken)
        } catch {
          clearAuth()
        }
      } else {
        clearAuth()
      }

      setIsLoading(false)
    }

    initAuth()
  }, [clearAuth, setAuthTokens, mergeAnonymousViewSession])

  const login = async (data: LoginRequest) => {
    const authRes = await loginApi(data)
    setAuthTokens(authRes.accessToken, authRes.refreshToken, authRes.user)
    mergeAnonymousViewSession(authRes.accessToken)
  }

  const register = async (data: RegisterRequest) => {
    await registerApi(data)
    // Auto login after successful register
    const authRes = await loginApi({ email: data.email, password: data.password })
    setAuthTokens(authRes.accessToken, authRes.refreshToken, authRes.user)
    mergeAnonymousViewSession(authRes.accessToken)
  }

  const logout = async () => {
    const storedRefreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
    try {
      if (storedRefreshToken) {
        await logoutApi(storedRefreshToken)
      }
    } catch {
      // ignore logout network errors
    } finally {
      clearAuth()
    }
  }

  const updateUser = useCallback((updatedFields: Partial<UserDto>) => {
    setUser((prev) => (prev ? { ...prev, ...updatedFields } : null))
  }, [])

  const refreshUser = useCallback(async () => {
    const currentToken = accessToken || localStorage.getItem(ACCESS_TOKEN_KEY)
    if (currentToken) {
      try {
        const userData = await getMeApi(currentToken)
        setUser(userData)
      } catch {
        // ignore
      }
    }
  }, [accessToken])

  return (
    <AuthContext.Provider
      value={{
        user,
        accessToken,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        logout,
        updateUser,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

export function useOptionalAuth(): AuthContextType | undefined {
  return useContext(AuthContext)
}

