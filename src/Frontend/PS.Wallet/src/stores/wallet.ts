import { defineStore } from 'pinia'
import { apiClient } from '@/services/api/client'
import { ApiError, toApiError } from '@/services/api/errors'
import { useDevUserStore } from '@/stores/devUser'
import type { WalletDto } from '@/services/api/types'

export type WalletStatus = 'idle' | 'loading' | 'success' | 'error'

export const useWalletStore = defineStore('wallet', {
  state: () => ({
    wallet: null as WalletDto | null,
    status: 'idle' as WalletStatus,
    error: null as ApiError | null,
  }),

  actions: {
    // The balance always comes from the server; the client never recomputes it.
    async fetchWallet(): Promise<void> {
      // Capture the identity this request belongs to, so a response that arrives after the
      // user switched can be dropped instead of overwriting the new user's wallet.
      const requestedFor = useDevUserStore().currentUser
      this.status = 'loading'
      this.error = null

      try {
        const { data } = await apiClient.get<WalletDto>('/v1/wallet')
        if (useDevUserStore().currentUser !== requestedFor) {
          return // stale response for a previous user; drop it
        }
        this.wallet = data
        this.status = 'success'
      } catch (e) {
        if (useDevUserStore().currentUser !== requestedFor) {
          return
        }
        this.error = toApiError(e)
        this.status = 'error'
      }
    },

    reset(): void {
      this.wallet = null
      this.status = 'idle'
      this.error = null
    },
  },
})
