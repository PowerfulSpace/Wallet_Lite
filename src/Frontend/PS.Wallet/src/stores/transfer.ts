import { defineStore } from 'pinia'
import { apiClient } from '@/services/api/client'
import { ApiError, toApiError } from '@/services/api/errors'
import { parseMoney } from '@/shared/money'
import { validateUsername } from '@/shared/username'
import type { CreateTransferRequest, Money, TransferResponseDto } from '@/services/api/types'

export type TransferStatus = 'idle' | 'submitting' | 'succeeded' | 'failed' | 'outcomeUnknown'

export const useTransferStore = defineStore('transfer', {
  state: () => ({
    status: 'idle' as TransferStatus,
    error: null as ApiError | null,
    result: null as TransferResponseDto | null,
    // Idempotency key and payload of the current logical transfer operation. Kept across
    // failures so a retry of the same request reuses the same key (see ADR-005).
    activeKey: null as string | null,
    activePayload: null as CreateTransferRequest | null,
  }),

  actions: {
    async submit(recipientUsername: string, amount: Money): Promise<void> {
      // Guard against double submits (double click / duplicate events).
      if (this.status === 'submitting') {
        return
      }

      const parsed = parseMoney(amount)
      if (!parsed.ok) {
        this.error = new ApiError(0, 'CLIENT_VALIDATION', undefined, parsed.reason)
        this.status = 'failed'
        return
      }

      const usernameResult = validateUsername(recipientUsername)
      if (!usernameResult.ok) {
        this.error = new ApiError(0, 'CLIENT_VALIDATION', undefined, usernameResult.reason)
        this.status = 'failed'
        return
      }
      const canonicalRecipient = usernameResult.canonical

      const normalized = parsed.value

      // Reuse the existing key when retrying the same logical operation (same recipient and
      // amount); otherwise mint a new one. crypto.randomUUID() yields e.g.
      // "3b241101-e2bb-4255-8caf-4136c566a962": only [A-Za-z0-9-], length 36, matching the
      // backend's ^[A-Za-z0-9_-]{8,128}$ requirement.
      let key = this.activeKey
      if (
        !(
          key &&
          this.activePayload?.recipientUsername === canonicalRecipient &&
          this.activePayload?.amount === normalized
        )
      ) {
        key = crypto.randomUUID()
        this.activeKey = key
        this.activePayload = { recipientUsername: canonicalRecipient, amount: normalized }
      }

      this.status = 'submitting'
      this.error = null

      try {
        const { data } = await apiClient.post<TransferResponseDto>(
          '/v1/transfers',
          { recipientUsername: canonicalRecipient, amount: normalized },
          { headers: { 'Idempotency-Key': key } },
        )

        this.result = data
        this.status = 'succeeded'
        // The key is intentionally kept; it is only cleared by reset() or when a different
        // recipient/amount starts a new logical operation.
      } catch (e) {
        const apiError = toApiError(e)
        this.error = apiError

        // Network/timeout (status 0): the request may or may not have been applied.
        if (apiError.status === 0) {
          this.status = 'outcomeUnknown'
          return
        }

        // A concurrent identical request is still being processed: retry with the same key.
        if (apiError.status === 503 && apiError.code === 'IDEMPOTENCY_IN_PROGRESS') {
          this.status = 'outcomeUnknown'
          return
        }

        // Business rejections (400/404/409) do not consume the key; the same key + payload
        // may be retried later (ADR-005 §10). 409 IDEMPOTENCY_KEY_REUSED is surfaced as a
        // failure without minting a new key, so a broken key-reuse path is not masked.
        this.status = 'failed'
      }
    },

    reset(): void {
      this.status = 'idle'
      this.error = null
      this.result = null
      this.activeKey = null
      this.activePayload = null
    },
  },
})
