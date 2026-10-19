import './assets/styles/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import naive from 'naive-ui'
import { useDevUserStore } from '@/stores/devUser'
import { useWalletStore } from '@/stores/wallet'
import { useDepositStore } from '@/stores/deposit'
import { useTransferStore } from '@/stores/transfer'
import { useTransactionsStore } from '@/stores/transactions'
import { useTransferDetailsStore } from '@/stores/transferDetails'
import { setDevUserProvider } from '@/services/api/client'

const app = createApp(App)

const pinia = createPinia()
app.use(pinia)
app.use(router)
app.use(naive)

// Dev-only: wire the X-Test-User provider using the explicitly passed Pinia instance.
const devUserStore = useDevUserStore(pinia)
setDevUserProvider(() => devUserStore.currentUser)

// Switching the dev user mimics switching the authenticated user: every user-specific store
// must be reset so no data from the previous user leaks into the UI
// (see docs/frontend/state-management.md §13).
const walletStore = useWalletStore(pinia)
const depositStore = useDepositStore(pinia)
const transferStore = useTransferStore(pinia)
const transactionsStore = useTransactionsStore(pinia)
const transferDetailsStore = useTransferDetailsStore(pinia)
devUserStore.$subscribe(() => {
  walletStore.reset()
  depositStore.reset()
  transferStore.reset()
  transactionsStore.reset()
  transferDetailsStore.reset()
  // Eager reload for the new user: the balance must reflect the current identity,
  // not remain empty until the page is revisited.
  void walletStore.fetchWallet()
})

app.mount('#app')
