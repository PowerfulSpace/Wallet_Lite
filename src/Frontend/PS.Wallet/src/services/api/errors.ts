import axios from 'axios'
import type { ProblemDetails } from './types'

// ApiError is a technical transport/domain error. User-facing messages are built separately.
// It intentionally never carries request bodies, tokens, or keys into logs.
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string | undefined,
    public readonly problem: ProblemDetails | undefined,
  ) {
    super(`API error ${status}${code ? ` (${code})` : ''}`)
    this.name = 'ApiError'
  }
}

export function isApiError(e: unknown): e is ApiError {
  return e instanceof ApiError
}

export function toApiError(e: unknown): ApiError {
  if (axios.isAxiosError(e)) {
    if (e.response) {
      const data = e.response.data as ProblemDetails | undefined
      return new ApiError(e.response.status, data?.code, data)
    }
    return new ApiError(0, 'NETWORK_ERROR', undefined)
  }
  return new ApiError(0, 'UNKNOWN_ERROR', undefined)
}
