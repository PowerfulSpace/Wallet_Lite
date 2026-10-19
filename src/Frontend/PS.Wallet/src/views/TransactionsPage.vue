<template>
  <section>
    <h1>Transactions</h1>

    <n-spin
      v-if="transactionsStore.status === 'loading' && transactionsStore.items.length === 0"
      size="small"
    >
      <n-text>Loading…</n-text>
    </n-spin>

    <template v-else-if="transactionsStore.status === 'error'">
      <n-alert type="error" title="Failed to load transactions">{{ transactionsError }}</n-alert>
      <n-button class="retry-button" size="small" @click="transactionsStore.fetchFirstPage()">
        Retry
      </n-button>
    </template>

    <n-empty
      v-else-if="transactionsStore.status === 'success' && transactionsStore.items.length === 0"
      description="No transactions yet."
    />

    <template v-else>
      <n-list bordered>
        <n-list-item v-for="tx in transactionsStore.items" :key="tx.transactionId">
          <div class="transaction">
            <div class="transaction-main">
              <n-tag :type="tagType(tx.type)" size="small" :bordered="false">
                {{ typeLabel(tx.type) }}
              </n-tag>
              <router-link
                v-if="tx.type !== 'deposit'"
                class="transaction-description"
                :to="'/transfers/' + tx.relatedOperationId"
              >
                {{ tx.description }}
              </router-link>
              <n-text v-else class="transaction-description" depth="3">
                {{ tx.description }}
              </n-text>
            </div>
            <div class="transaction-meta">
              <n-text class="transaction-amount" :class="amountClass(tx.amount)">
                {{ formatMoney(tx.amount) }}
              </n-text>
              <n-text depth="3" class="transaction-date">
                {{ formatDateTime(tx.createdAtUtc) }}
              </n-text>
            </div>
          </div>
        </n-list-item>
      </n-list>

      <n-alert
        v-if="transactionsStore.loadMoreStatus === 'error'"
        class="load-more"
        type="error"
      >
        Failed to load more.
      </n-alert>

      <n-button
        v-if="transactionsStore.hasMore"
        class="load-more"
        :disabled="transactionsStore.loadMoreStatus === 'loading'"
        :loading="transactionsStore.loadMoreStatus === 'loading'"
        @click="transactionsStore.loadMore()"
      >
        Load more
      </n-button>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { NAlert, NButton, NEmpty, NList, NListItem, NSpin, NTag, NText } from 'naive-ui'
import { useTransactionsStore } from '@/stores/transactions'
import { useDevUserStore } from '@/stores/devUser'
import { formatMoney } from '@/shared/money'
import { formatDateTime } from '@/shared/datetime'
import type { Money, TransactionType } from '@/services/api/types'

const transactionsStore = useTransactionsStore()
const devUserStore = useDevUserStore()

const transactionsError = computed(
  () =>
    transactionsStore.error?.problem?.detail ??
    'Could not load transactions. Please try again.',
)

function typeLabel(type: TransactionType): string {
  switch (type) {
    case 'deposit':
      return 'Deposit'
    case 'transfer_in':
      return 'Transfer in'
    case 'transfer_out':
      return 'Transfer out'
  }
}

function tagType(type: TransactionType): 'success' | 'info' | 'warning' | 'default' {
  switch (type) {
    case 'deposit':
      return 'success'
    case 'transfer_in':
      return 'info'
    case 'transfer_out':
      return 'default'
  }
}

// The sign is already part of the formatted amount; color is an additional cue.
function amountClass(amount: Money): string {
  return amount.startsWith('-') ? 'amount-negative' : 'amount-positive'
}

onMounted(() => {
  if (transactionsStore.status === 'idle') {
    void transactionsStore.fetchFirstPage()
  }
})

watch(
  () => devUserStore.currentUser,
  () => {
    // The store was reset centrally in main.ts; reload for the new user.
    if (transactionsStore.status === 'idle') {
      void transactionsStore.fetchFirstPage()
    }
  },
)
</script>

<style scoped>
.retry-button {
  margin-top: 12px;
}

.transaction {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  width: 100%;
}

.transaction-main {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.transaction-description {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.transaction-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  flex-shrink: 0;
}

.transaction-amount {
  font-weight: 600;
}

.amount-positive {
  color: #18a058;
}

.amount-negative {
  color: #d03050;
}

.transaction-date {
  font-size: 12px;
}

.load-more {
  margin-top: 16px;
}
</style>
