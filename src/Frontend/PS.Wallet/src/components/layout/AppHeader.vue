<template>
  <n-layout-header bordered>
    <div class="header-content">
      <div class="header-left">
        <router-link to="/">
          <img :src="logo" alt="logo" class="logo" />
        </router-link>

        <n-menu
          mode="horizontal"
          :options="menuOptions"
          :value="active"
          @update:value="onSelect"
        />
      </div>

      <div class="dev-user">
        <n-tag type="warning" size="small" :bordered="false">dev</n-tag>
        <n-select
          :value="devUser.currentUser"
          :options="userOptions"
          size="small"
          class="dev-user-select"
          @update:value="onUserChange"
        />
      </div>
    </div>
  </n-layout-header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { NLayoutHeader, NMenu, NSelect, NTag } from 'naive-ui'
import type { MenuOption, SelectOption } from 'naive-ui'
import logo from '@/assets/icons/logo.svg'
import { useDevUserStore } from '@/stores/devUser'
import { isDevUser } from '@/shared/devUser'

const router = useRouter()
const route = useRoute()
const devUser = useDevUserStore()

const active = computed(() => (typeof route.name === 'string' ? route.name : ''))

const userOptions: SelectOption[] = [
  { label: 'alice', value: 'alice' },
  { label: 'bob', value: 'bob' },
]

const menuOptions: MenuOption[] = [
  { label: 'Home', key: 'home' },
  { label: 'About', key: 'about' },
  { label: 'Wallet', key: 'wallet' },
  { label: 'Transactions', key: 'transactions' },
  { label: 'Transfer', key: 'transfer' },
]

function onSelect(key: string) {
  router.push({ name: key })
}

// Dev-only: switches the X-Test-User header used by the Development backend.
function onUserChange(value: unknown) {
  if (isDevUser(value)) {
    devUser.setUser(value)
  }
}
</script>

<style scoped>
.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  height: 64px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 24px;
}

.logo {
  height: 32px;
  display: block;
}

.dev-user {
  display: flex;
  align-items: center;
  gap: 8px;
}

.dev-user-select {
  width: 110px;
}
</style>
