<template>
  <section>
    <h1>Wallet</h1>

    <n-card title="Balance" class="wallet-card">
      <n-spin v-if="walletStore.status === 'loading' && !wallet" size="small">
        <n-text>Loading…</n-text>
      </n-spin>

      <template v-else-if="walletStore.status === 'error'">
        <n-alert type="error" title="Failed to load wallet">{{ walletError }}</n-alert>
        <n-button class="retry-button" size="small" @click="walletStore.fetchWallet()">
          Retry
        </n-button>
      </template>

      <div v-else-if="wallet" class="balance">
        <n-statistic label="Available balance" :value="formatMoney(wallet.balance)">
          <template #suffix>{{ wallet.currency }}</template>
        </n-statistic>
        <n-text depth="3" class="wallet-id">Wallet ID: {{ wallet.walletId }}</n-text>
      </div>

      <n-text v-else depth="3">No wallet loaded.</n-text>
    </n-card>

    <n-card title="Deposit" class="deposit-card">
      <n-form @submit.prevent="onSubmit">
        <n-form-item
          label="Amount (USD)"
          :feedback="amountError ?? undefined"
          :validation-status="amountError ? 'error' : undefined"
        >
          <n-input
            v-model:value="amount"
            placeholder="0.00"
            :input-props="{ inputmode: 'decimal' }"
            :disabled="depositStore.status === 'submitting'"
            @blur="validateAmount"
          />
        </n-form-item>

        <n-button
          attr-type="submit"
          type="primary"
          :disabled="depositStore.status === 'submitting'"
          :loading="depositStore.status === 'submitting'"
        >
          {{ depositStore.status === 'submitting' ? 'Depositing…' : 'Deposit' }}
        </n-button>
      </n-form>

      <n-alert
        v-if="depositStore.status === 'succeeded' && depositStore.result"
        class="deposit-result"
        type="success"
        title="Deposit completed"
      >
        Deposited {{ formatMoney(depositStore.result.amount) }}. New balance:
        {{ formatMoney(depositStore.result.balanceAfter) }}.
      </n-alert>
      <n-button
        v-if="depositStore.status === 'succeeded'"
        class="deposit-result"
        size="small"
        @click="onNewDeposit"
      >
        New deposit
      </n-button>

      <n-alert
        v-else-if="depositStore.status === 'failed'"
        class="deposit-result"
        type="error"
        title="Deposit failed"
      >
        {{ depositErrorMessage }}
      </n-alert>

      <template v-else-if="depositStore.status === 'outcomeUnknown'">
        <n-alert class="deposit-result" type="warning" title="Uncertain result">
          The result of this deposit is uncertain. Retry with the same amount to confirm — the
          server will not double-charge.
        </n-alert>
        <n-button
          v-if="depositStore.activePayload"
          class="deposit-result"
          size="small"
          @click="onRetry"
        >
          Retry
        </n-button>
        <n-button v-else class="deposit-result" size="small" @click="depositStore.reset()">
          Start new deposit
        </n-button>
      </template>
    </n-card>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  NAlert,
  NButton,
  NCard,
  NForm,
  NFormItem,
  NInput,
  NSpin,
  NStatistic,
  NText,
} from 'naive-ui'
import { useWalletStore } from '@/stores/wallet'
import { useDepositStore } from '@/stores/deposit'
import { useDevUserStore } from '@/stores/devUser'
import { formatMoney, parseMoney } from '@/shared/money'
import type { Money } from '@/services/api/types'

const walletStore = useWalletStore()
const depositStore = useDepositStore()
const devUserStore = useDevUserStore()

const wallet = computed(() => walletStore.wallet)

const amount = ref('')
const amountError = ref<string | null>(null)

// Clear the local form text so it does not leak between users.
watch(
  () => devUserStore.currentUser,
  () => {
    amount.value = ''
    amountError.value = null
  },
)

const walletError = computed(
  () => walletStore.error?.problem?.detail ?? 'Could not load the wallet. Please try again.',
)

const depositErrorMessage = computed(() => {
  const err = depositStore.error
  if (!err) {
    return 'Deposit failed.'
  }
  switch (err.code) {
    case 'VALIDATION_ERROR':
      return 'Check the amount.'
    case 'BALANCE_LIMIT_EXCEEDED':
      return 'This deposit would exceed the wallet limit ($1,000,000.00).'
    default:
      return err.problem?.detail ?? 'Deposit failed.'
  }
})

function validateAmount() {
  if (amount.value === '') {
    amountError.value = null
    return
  }
  const parsed = parseMoney(amount.value)
  amountError.value = parsed.ok ? null : parsed.reason
}

async function submitDeposit(money: Money) {
  await depositStore.submit(money)
  if (depositStore.status === 'succeeded') {
    amount.value = ''
    // The balance is a server-side source of truth; never recompute it locally.
    await walletStore.fetchWallet()
  }
}

async function onSubmit() {
  const parsed = parseMoney(amount.value)
  if (!parsed.ok) {
    amountError.value = parsed.reason
    return
  }
  amountError.value = null
  await submitDeposit(parsed.value)
}

function onRetry() {
  if (!depositStore.activePayload) {
    // Cannot safely retry: the original operation's identity is gone.
    depositStore.reset()
    return
  }
  void submitDeposit(depositStore.activePayload.amount)
}

function onNewDeposit() {
  depositStore.reset()
}

onMounted(() => {
  if (walletStore.status === 'idle') {
    void walletStore.fetchWallet()
  }
})
</script>

<style scoped>
.wallet-card,
.deposit-card {
  max-width: 520px;
  margin-bottom: 20px;
}

.retry-button {
  margin-top: 12px;
}

.wallet-id {
  display: block;
  margin-top: 8px;
  font-size: 12px;
  word-break: break-all;
}

.deposit-result {
  margin-top: 16px;
}
</style>
