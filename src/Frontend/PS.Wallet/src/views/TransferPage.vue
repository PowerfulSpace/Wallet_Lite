<template>
  <section>
    <h1>Transfer</h1>

    <n-card title="Current balance" class="balance-card">
      <n-text v-if="wallet" class="balance-value">{{ formatMoney(wallet.balance) }}</n-text>
      <n-text v-else depth="3">{{ walletStore.status === 'loading' ? 'Loading…' : '—' }}</n-text>
    </n-card>

    <n-card title="New transfer" class="form-card">
      <n-form @submit.prevent="onSubmit">
        <n-form-item
          label="Recipient"
          :feedback="recipientError ?? undefined"
          :validation-status="recipientError ? 'error' : undefined"
        >
          <n-input
            v-model:value="recipientUsername"
            placeholder="bob"
            :input-props="{ autocomplete: 'off' }"
            :disabled="transferStore.status === 'submitting'"
            @blur="validateRecipientField"
          />
        </n-form-item>

        <n-form-item
          label="Amount (USD)"
          :feedback="amountError ?? undefined"
          :validation-status="amountError ? 'error' : undefined"
        >
          <n-input
            v-model:value="amount"
            placeholder="0.00"
            :input-props="{ inputmode: 'decimal' }"
            :disabled="transferStore.status === 'submitting'"
            @blur="validateAmountField"
          />
        </n-form-item>

        <n-button
          attr-type="submit"
          type="primary"
          :disabled="transferStore.status === 'submitting'"
          :loading="transferStore.status === 'submitting'"
        >
          {{ transferStore.status === 'submitting' ? 'Transferring…' : 'Transfer' }}
        </n-button>
      </n-form>

      <n-alert
        v-if="transferStore.status === 'succeeded' && transferStore.result"
        class="transfer-result"
        type="success"
        title="Transfer completed"
      >
        Transferred {{ formatMoney(transferStore.result.amount) }} to
        {{ transferStore.result.recipientUsername }}. Transfer ID:
        {{ shortTransferId }}.
      </n-alert>
      <div v-if="transferStore.status === 'succeeded'" class="transfer-result">
        <n-button size="small" @click="onViewDetails">View details</n-button>
        <n-button class="transfer-result-action" size="small" @click="onNewTransfer">
          New transfer
        </n-button>
      </div>

      <n-alert
        v-else-if="transferStore.status === 'failed'"
        class="transfer-result"
        type="error"
        title="Transfer failed"
      >
        {{ transferErrorMessage }}
      </n-alert>

      <template v-else-if="transferStore.status === 'outcomeUnknown'">
        <n-alert class="transfer-result" type="warning" title="Uncertain result">
          The result of this transfer is uncertain. Retry with the same details to confirm — the
          server will not double-charge.
        </n-alert>
        <n-button
          v-if="transferStore.activePayload"
          class="transfer-result"
          size="small"
          @click="onRetry"
        >
          Retry
        </n-button>
        <n-button v-else class="transfer-result" size="small" @click="onNewTransfer">
          Start new transfer
        </n-button>
      </template>
    </n-card>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { NAlert, NButton, NCard, NForm, NFormItem, NInput, NText } from 'naive-ui'
import { useTransferStore } from '@/stores/transfer'
import { useWalletStore } from '@/stores/wallet'
import { useDevUserStore } from '@/stores/devUser'
import { formatMoney, parseMoney } from '@/shared/money'
import type { Money } from '@/services/api/types'

const RECIPIENT_USERNAME_PATTERN = /^[A-Za-z0-9_]{3,32}$/

const router = useRouter()
const transferStore = useTransferStore()
const walletStore = useWalletStore()
const devUserStore = useDevUserStore()

const wallet = computed(() => walletStore.wallet)
const shortTransferId = computed(() =>
  transferStore.result ? transferStore.result.transferId.slice(0, 8) : '',
)

const recipientUsername = ref('')
const amount = ref('')
const recipientError = ref<string | null>(null)
const amountError = ref<string | null>(null)

const transferErrorMessage = computed(() => {
  const err = transferStore.error
  if (!err) {
    return 'Transfer failed.'
  }
  switch (err.code) {
    case 'INSUFFICIENT_FUNDS':
      return 'Insufficient funds. Your balance is lower than the transfer amount.'
    case 'SELF_TRANSFER':
      return 'You cannot transfer to yourself.'
    case 'BALANCE_LIMIT_EXCEEDED':
      return "This transfer would exceed the recipient's wallet limit ($1,000,000.00)."
    case 'RESOURCE_NOT_FOUND':
      return 'Recipient not found.'
    case 'IDEMPOTENCY_KEY_REUSED':
      return 'This request conflicts with a previous operation. Start a new transfer.'
    case 'VALIDATION_ERROR':
    case 'CLIENT_VALIDATION':
      return 'Check the transfer details.'
    default:
      return err.problem?.detail ?? 'Transfer failed.'
  }
})

function validateRecipientField() {
  const trimmed = recipientUsername.value.trim()
  if (trimmed === '') {
    recipientError.value = null
    return
  }
  recipientError.value = RECIPIENT_USERNAME_PATTERN.test(trimmed)
    ? null
    : 'Recipient username is invalid.'
}

function validateAmountField() {
  if (amount.value === '') {
    amountError.value = null
    return
  }
  const parsed = parseMoney(amount.value)
  amountError.value = parsed.ok ? null : parsed.reason
}

function clearForm() {
  recipientUsername.value = ''
  amount.value = ''
  recipientError.value = null
  amountError.value = null
}

async function submitTransfer(recipient: string, money: Money) {
  await transferStore.submit(recipient, money)
  if (transferStore.status === 'succeeded') {
    clearForm()
    // The balance is a server-side source of truth; never recompute it locally.
    await walletStore.fetchWallet()
  }
}

async function onSubmit() {
  const trimmed = recipientUsername.value.trim()
  if (trimmed === '') {
    recipientError.value = 'Recipient username is required.'
  } else if (!RECIPIENT_USERNAME_PATTERN.test(trimmed)) {
    recipientError.value = 'Recipient username is invalid.'
  } else {
    recipientError.value = null
  }
  const recipientValid = recipientError.value === null

  const parsed = parseMoney(amount.value)
  amountError.value = parsed.ok ? null : parsed.reason

  if (!recipientValid || !parsed.ok) {
    return
  }

  await submitTransfer(trimmed, parsed.value)
}

function onRetry() {
  if (!transferStore.activePayload) {
    // Cannot safely retry: the original operation's identity is gone.
    transferStore.reset()
    return
  }
  void submitTransfer(
    transferStore.activePayload.recipientUsername,
    transferStore.activePayload.amount,
  )
}

function onNewTransfer() {
  transferStore.reset()
  clearForm()
}

function onViewDetails() {
  if (transferStore.result) {
    void router.push('/transfers/' + transferStore.result.transferId)
  }
}

// Clear local form text so it does not leak between users. The store reset itself is handled
// centrally in main.ts via devUserStore.$subscribe().
watch(
  () => devUserStore.currentUser,
  () => {
    clearForm()
  },
)

onMounted(() => {
  if (walletStore.status === 'idle') {
    void walletStore.fetchWallet()
  }
})
</script>

<style scoped>
.balance-card,
.form-card {
  max-width: 520px;
  margin-bottom: 20px;
}

.balance-value {
  font-size: 24px;
  font-weight: 600;
}

.transfer-result {
  margin-top: 16px;
}

.transfer-result-action {
  margin-left: 8px;
}
</style>
