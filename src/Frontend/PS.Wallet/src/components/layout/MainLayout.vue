<template>
  <div class="layout">
    <AppHeader />

    <n-layout-content content-style="padding: 20px;">
      <router-view />
    </n-layout-content>

    <AppFooter />
  </div>
</template>

<script setup lang="ts">
import { NLayoutContent } from 'naive-ui'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
</script>

<style scoped>
.layout {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}
</style>
