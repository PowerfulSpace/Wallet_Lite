import axios from 'axios'

// The provider avoids importing the Pinia store here (circular-import safety).
// main.ts registers it after Pinia is installed.
let getDevUser: () => string | null = () => null

export function setDevUserProvider(fn: () => string | null) {
  getDevUser = fn
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 5000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Development-only authentication: the Development backend trusts the `X-Test-User` header.
// No response interceptor is added on purpose; callers normalize failures via `toApiError`.
apiClient.interceptors.request.use((config) => {
  const user = getDevUser()
  if (user) config.headers.set('X-Test-User', user)
  return config
})
