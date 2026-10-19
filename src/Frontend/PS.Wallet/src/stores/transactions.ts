import { defineStore } from 'pinia'
import { apiClient } from '@/services/api/client'
import { ApiError, toApiError } from '@/services/api/errors'
import { useDevUserStore } from '@/stores/devUser'
import type { TransactionDto, TransactionsPageDto } from '@/services/api/types'

export type TransactionsStatus = 'idle' | 'loading' | 'success' | 'error'
export type LoadMoreStatus = 'idle' | 'loading' | 'error'

export const useTransactionsStore = defineStore('transactions', {
  state: () => ({
    items: [] as TransactionDto[],
    nextCursor: null as string | null,
    hasMore: false,
    status: 'idle' as TransactionsStatus,
    loadMoreStatus: 'idle' as LoadMoreStatus,
    error: null as ApiError | null,
  }),

  actions: {
    // Loads the first page (no cursor). Subsequent pages use loadMore().
    async fetchFirstPage(): Promise<void> {
      if (this.status === 'loading') {
        return
      }

      // Capture the identity this request belongs to, so a response that arrives after the
      // user switched can be dropped instead of showing another user's transactions.
      const requestedFor = useDevUserStore().currentUser
      this.status = 'loading'
      this.error = null

      try {
        const { data } = await apiClient.get<TransactionsPageDto>('/v1/wallet/transactions')
        if (useDevUserStore().currentUser !== requestedFor) {
          return // stale response for a previous user; drop it
        }
        this.items = data.items
        this.nextCursor = data.nextCursor
        this.hasMore = data.hasMore
        this.status = 'success'
      } catch (e) {
        if (useDevUserStore().currentUser !== requestedFor) {
          return
        }
        this.error = toApiError(e)
        this.status = 'error'
      }
    },

    // Appends the next page using the opaque cursor returned by the server.
    async loadMore(): Promise<void> {
      if (this.loadMoreStatus === 'loading') {
        return
      }
      if (!this.hasMore || !this.nextCursor) {
        return
      }

      const requestedFor = useDevUserStore().currentUser
      const cursor = this.nextCursor
      this.loadMoreStatus = 'loading'
      this.error = null

      try {
        const { data } = await apiClient.get<TransactionsPageDto>('/v1/wallet/transactions', {
          params: { cursor },
        })
        if (useDevUserStore().currentUser !== requestedFor) {
          return // stale response for a previous user; drop it
        }
        this.items = [...this.items, ...data.items]
        this.nextCursor = data.nextCursor
        this.hasMore = data.hasMore
        this.loadMoreStatus = 'idle'
      } catch (e) {
        if (useDevUserStore().currentUser !== requestedFor) {
          return
        }
        // Keep already-loaded items; only the pagination attempt failed.
        this.error = toApiError(e)
        this.loadMoreStatus = 'error'
      }
    },

    reset(): void {
      this.items = []
      this.nextCursor = null
      this.hasMore = false
      this.status = 'idle'
      this.loadMoreStatus = 'idle'
      this.error = null
    },
  },
})
