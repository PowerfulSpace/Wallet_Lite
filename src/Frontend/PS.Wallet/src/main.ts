import './assets/styles/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import naive from 'naive-ui'
import { useDevUserStore } from '@/stores/devUser'
import { setDevUserProvider } from '@/services/api/client'

const app = createApp(App)

const pinia = createPinia()
app.use(pinia)
app.use(router)
app.use(naive)

// Dev-only: wire the X-Test-User provider using the explicitly passed Pinia instance.
const devUserStore = useDevUserStore(pinia)
setDevUserProvider(() => devUserStore.currentUser)

app.mount('#app')
