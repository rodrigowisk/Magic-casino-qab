<script setup lang="ts">
import { ref, computed, nextTick, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { 
  User, 
  Lock, 
  History, 
  LogOut, 
  FileText, 
  MoreVertical,
  Dices
} from 'lucide-vue-next';
import { useAuthStore } from '../stores/useAuthStore'; // Ajuste o caminho conforme seu projeto

const router = useRouter();
const authStore = useAuthStore();
const isOpen = ref(false);
const triggerRef = ref<HTMLElement | null>(null);
const dropdownRef = ref<HTMLElement | null>(null);

const menuStyle = ref({
  top: '0px',
  left: '0px',
  width: '240px'
});

// Lógica para obter as iniciais (1ª do Nome + 1ª do Sobrenome)
const userInitials = computed(() => {
  const name = authStore.user?.name || '';
  if (!name) return 'U';
  
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) {
    return parts[0].charAt(0).toUpperCase();
  }
  return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
});

const calculatePosition = () => {
  if (triggerRef.value) {
    const rect = triggerRef.value.getBoundingClientRect();
    menuStyle.value = {
      top: `${rect.bottom + 8}px`,
      left: `${rect.right - 240}px`, // Alinha à direita
      width: '240px'
    };
  }
};

const toggleMenu = async () => {
  isOpen.value = !isOpen.value;
  if (isOpen.value) {
    await nextTick();
    calculatePosition();
  }
};

const closeMenu = () => {
  isOpen.value = false;
};

const navigateTo = (path: string) => {
  router.push(path);
  closeMenu();
};

const handleLogout = () => {
  authStore.logout(); // Sua lógica de logout
  router.push('/');
  closeMenu();
};

const handleClickOutside = (event: MouseEvent) => {
  const clickedInsideMenu = dropdownRef.value?.contains(event.target as Node);
  const clickedInsideTrigger = triggerRef.value?.contains(event.target as Node);
  
  if (!clickedInsideMenu && !clickedInsideTrigger) {
    closeMenu();
  }
};

const handleResize = () => {
  if (isOpen.value) calculatePosition();
};

onMounted(() => {
  document.addEventListener('click', handleClickOutside);
  window.addEventListener('resize', handleResize);
  window.addEventListener('scroll', handleResize, true);
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
  window.removeEventListener('resize', handleResize);
  window.removeEventListener('scroll', handleResize, true);
});
</script>

<template>
  <div class="relative">
    <button 
      ref="triggerRef"
      @click.stop="toggleMenu"
      class="flex items-center gap-2 p-1 pr-2 rounded-full hover:bg-[#1e293b] transition-colors group select-none"
      :class="{ 'bg-[#1e293b]': isOpen }"
    >
      <div class="w-9 h-9 bg-gradient-to-br from-blue-600 to-blue-800 rounded-full flex items-center justify-center text-white font-bold shadow-lg border border-blue-400/30">
        {{ userInitials }}
      </div>

      <MoreVertical class="w-4 h-4 text-gray-400 group-hover:text-white transition-colors" />
    </button>

    <Teleport to="body">
      <transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="transform scale-95 opacity-0 -translate-y-2"
        enter-to-class="transform scale-100 opacity-100 translate-y-0"
        leave-active-class="transition duration-150 ease-in"
        leave-from-class="transform scale-100 opacity-100 translate-y-0"
        leave-to-class="transform scale-95 opacity-0 -translate-y-2"
      >
        <div 
          v-if="isOpen" 
          ref="dropdownRef"
          :style="menuStyle"
          class="fixed bg-[#1e293b] border border-gray-700 rounded-xl shadow-2xl z-[9999] overflow-hidden ring-1 ring-black/50"
        >
          <div class="p-1.5 space-y-1">
            
            <button @click="navigateTo('/perfil')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-medium text-gray-300 hover:text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <User class="w-4 h-4 text-gray-500 group-hover:text-blue-400" />
              Editar Perfil
            </button>

            <button @click="navigateTo('/senha')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-medium text-gray-300 hover:text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <Lock class="w-4 h-4 text-gray-500 group-hover:text-yellow-400" />
              Alterar Senha
            </button>

            <div class="h-px bg-gray-700/50 mx-2 my-1"></div>

            <button @click="navigateTo('/minhas-apostas')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-medium text-gray-300 hover:text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <Dices class="w-4 h-4 text-gray-500 group-hover:text-purple-400" />
              Minhas Apostas
            </button>

            <button @click="navigateTo('/transacoes')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-medium text-gray-300 hover:text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <FileText class="w-4 h-4 text-gray-500 group-hover:text-green-400" />
              Histórico de Transações
            </button>

            <div class="h-px bg-gray-700/50 mx-2 my-1"></div>

            <button @click="handleLogout" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-bold text-red-400 hover:bg-[#0f172a] rounded-lg group transition-all">
              <LogOut class="w-4 h-4 group-hover:text-red-500" />
              Sair
            </button>

          </div>
        </div>
      </transition>
    </Teleport>
  </div>
</template>