import { defineStore } from 'pinia'
import { apiClient } from '@/services/api/client'
import { ApiError, toApiError } from '@/services/api/errors'
import { useDevUserStore } from '@/stores/devUser'
import type { TransferResponseDto } from '@/services/api/types'

export type TransferDetailsStatus = 'idle' | 'loading' | 'success' | 'error' | 'notFound'

// Loose UUID shape check; the server remains the source of truth.
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export const useTransferDetailsStore = defineStore('transferDetails', {
  state: () => ({
    transfer: null as TransferResponseDto | null,
    status: 'idle' as TransferDetailsStatus,
    error: null as ApiError | null,
    loadedId: null as string | null,
  }),

  actions: {
    async fetchById(id: string): Promise<void> {
      if (this.status === 'loading' && this.loadedId === id) {
        return
      }

      // Reject obviously invalid IDs locally instead of making a request the server will
      // reject with 400 VALIDATION_ERROR.
      if (!UUID_PATTERN.test(id)) {
        this.transfer = null
        this.error = null
        this.loadedId = id
        this.status = 'notFound'
        return
      }

      // Capture the identity this request belongs to so a response that arrives after the
      // user switched can be dropped.
      const requestedFor = useDevUserStore().currentUser
      this.loadedId = id
      this.status = 'loading'
      this.error = null
      this.transfer = null

      try {
        const { data } = await apiClient.get<TransferResponseDto>(`/v1/transfers/${id}`)
        // Drop the response if the user switched or a newer id superseded this request.
        if (useDevUserStore().currentUser !== requestedFor || this.loadedId !== id) {
          return
        }
        this.transfer = data
        this.status = 'success'
      } catch (e) {
        if (useDevUserStore().currentUser !== requestedFor || this.loadedId !== id) {
          return
        }
        const apiError = toApiError(e)
        if (apiError.status === 404) {
          this.transfer = null
          this.error = apiError
          this.status = 'notFound'
          return
        }
        this.error = apiError
        this.status = 'error'
      }
    },

    reset(): void {
      this.transfer = null
      this.status = 'idle'
      this.error = null
      this.loadedId = null
    },
  },
})
