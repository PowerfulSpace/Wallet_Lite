<template>
  <section>
    <h1>Transfer details</h1>

    <n-spin v-if="store.status === 'loading'" size="small">
      <n-text>Loading…</n-text>
    </n-spin>

    <template v-else-if="store.status === 'notFound'">
      <n-alert type="warning" title="Not available">
        Transfer not found, or you do not have access to it.
      </n-alert>
      <n-button class="action" size="small" @click="goToTransactions">
        Back to transactions
      </n-button>
    </template>

    <template v-else-if="store.status === 'error'">
      <n-alert type="error" title="Failed to load transfer">{{ errorMessage }}</n-alert>
      <n-button class="action" size="small" @click="retry">Retry</n-button>
    </template>

    <n-card v-else-if="store.status === 'success' && store.transfer" title="Transfer">
      <div class="amount">
        <n-text class="amount-value">{{ formatMoney(store.transfer.amount) }}</n-text>
        <n-text depth="3">{{ store.transfer.currency }}</n-text>
      </div>

      <n-text class="direction" depth="3">{{ direction }}</n-text>

      <n-descriptions :column="1" label-placement="left">
        <n-descriptions-item label="Sender">
          {{ store.transfer.senderUsername }}
        </n-descriptions-item>
        <n-descriptions-item label="Recipient">
          {{ store.transfer.recipientUsername }}
        </n-descriptions-item>
        <n-descriptions-item label="Date">
          {{ formatDateTime(store.transfer.createdAtUtc) }}
        </n-descriptions-item>
        <n-descriptions-item label="Transfer ID">
          <n-text code>{{ store.transfer.transferId }}</n-text>
        </n-descriptions-item>
      </n-descriptions>

      <n-button class="action" size="small" @click="goToTransactions">
        Back to transactions
      </n-button>
    </n-card>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NAlert,
  NButton,
  NCard,
  NDescriptions,
  NDescriptionsItem,
  NSpin,
  NText,
} from 'naive-ui'
import { useTransferDetailsStore } from '@/stores/transferDetails'
import { useDevUserStore } from '@/stores/devUser'
import { formatMoney } from '@/shared/money'
import { formatDateTime } from '@/shared/datetime'

const route = useRoute()
const router = useRouter()
const store = useTransferDetailsStore()
const devUserStore = useDevUserStore()

const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))

const errorMessage = computed(
  () => store.error?.problem?.detail ?? 'Could not load the transfer.',
)

const direction = computed(() => {
  const transfer = store.transfer
  if (!transfer) {
    return ''
  }
  // Dev identity matches senderUsername/recipientUsername from the API response.
  return transfer.senderUsername === devUserStore.currentUser.toLowerCase()
    ? `Sent to ${transfer.recipientUsername}`
    : `Received from ${transfer.senderUsername}`
})

function goToTransactions() {
  void router.push('/wallet/transactions')
}

function retry() {
  void store.fetchById(id.value)
}

onMounted(() => {
  if (store.loadedId !== id.value) {
    void store.fetchById(id.value)
  }
})

watch(
  () => route.params.id,
  (newId) => {
    void store.fetchById(typeof newId === 'string' ? newId : '')
  },
)

watch(
  () => devUserStore.currentUser,
  () => {
    // The store was reset centrally in main.ts; reload for the new user if we have an id.
    if (typeof id.value === 'string' && id.value !== '' && store.status === 'idle') {
      void store.fetchById(id.value)
    }
  },
)
</script>

<style scoped>
.amount {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.amount-value {
  font-size: 28px;
  font-weight: 600;
}

.direction {
  display: block;
  margin: 8px 0 16px;
}

.action {
  margin-top: 16px;
}
</style>
