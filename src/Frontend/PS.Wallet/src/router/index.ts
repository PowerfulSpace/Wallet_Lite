import { createRouter, createWebHistory } from 'vue-router'
import HomePage from '@/views/HomePage.vue'
import AboutPage from '@/views/AboutPage.vue'
import WalletPage from '@/views/WalletPage.vue'
import TransactionsPage from '@/views/TransactionsPage.vue'
import TransferPage from '@/views/TransferPage.vue'
import TransferDetailsPage from '@/views/TransferDetailsPage.vue'

const routes = [
  {
    path: '/',
    name: 'home',
    component: HomePage,
  },
  {
    path: '/about',
    name: 'about',
    component: AboutPage,
  },
  {
    path: '/wallet',
    name: 'wallet',
    component: WalletPage,
  },
  {
    path: '/wallet/transactions',
    name: 'transactions',
    component: TransactionsPage,
  },
  {
    path: '/transfers',
    name: 'transfer',
    component: TransferPage,
  },
  {
    path: '/transfers/:id',
    name: 'transfer-details',
    component: TransferDetailsPage,
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

export default router
