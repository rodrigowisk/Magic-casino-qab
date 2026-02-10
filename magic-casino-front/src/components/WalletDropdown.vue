<script setup lang="ts">
import { ref, nextTick, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { 
  ArrowDownToLine, 
  ArrowUpFromLine, 
  History,
  ChevronDown,
  Wallet // Certifique-se de que Wallet está importado
} from 'lucide-vue-next';

defineProps<{
  balance: number | string;
}>();

const router = useRouter();
const isOpen = ref(false);
const triggerRef = ref<HTMLElement | null>(null);
const dropdownRef = ref<HTMLElement | null>(null);

const menuStyle = ref({
  top: '0px',
  left: '0px',
  width: '208px'
});

const calculatePosition = () => {
  if (triggerRef.value) {
    const rect = triggerRef.value.getBoundingClientRect();
    // Ajuste para não estourar a tela no mobile (alinha à direita do botão)
    const leftPos = Math.max(10, rect.right - 208); 
    
    menuStyle.value = {
      top: `${rect.bottom + 8}px`,
      left: `${leftPos}px`,
      width: '208px'
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

const handleClickOutside = (event: MouseEvent) => {
  if (!isOpen.value) return;

  const target = event.target as Node;
  const clickedInsideMenu = dropdownRef.value?.contains(target);
  const clickedInsideTrigger = triggerRef.value?.contains(target);
  
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
      @click="toggleMenu"
      class="flex items-center gap-1.5 md:gap-2 bg-[#0f172a] hover:bg-[#1e293b] px-3 md:px-4 py-1.5 rounded-full text-xs md:text-sm text-white border border-gray-700 shadow-inner transition-all duration-200 group"
      :class="{ 'border-blue-500/50 bg-[#1e293b]': isOpen }"
    >
      <Wallet class="w-3.5 h-3.5 md:w-4 md:h-4 text-green-400 group-hover:text-green-300 transition-colors" />
      
      <span class="font-bold tracking-wide whitespace-nowrap">
        R$ {{ Number(balance).toFixed(2) }}
      </span>

      <ChevronDown 
        class="w-3 h-3 text-gray-500 group-hover:text-white transition-transform duration-200 ml-0.5"
        :class="{ 'rotate-180': isOpen }" 
      />
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
            
            <button @click="navigateTo('/deposito')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-bold text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <div class="bg-green-500/10 p-1.5 rounded-md group-hover:bg-green-500 text-green-400 group-hover:text-white transition-colors">
                <ArrowUpFromLine class="w-4 h-4" />
              </div>
              Depositar
            </button>

            <button @click="navigateTo('/saque')" class="flex items-center gap-3 w-full px-3 py-2.5 text-sm font-bold text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <div class="bg-red-500/10 p-1.5 rounded-md group-hover:bg-red-500 text-red-400 group-hover:text-white transition-colors">
                <ArrowDownToLine class="w-4 h-4" />
              </div>
              Sacar
            </button>

            <div class="h-px bg-gray-700/50 mx-2 my-1"></div>

            <button @click="navigateTo('/transactions')" class="flex items-center gap-3 w-full px-3 py-2 text-sm font-medium text-gray-400 hover:text-white hover:bg-[#0f172a] rounded-lg group transition-all">
              <History class="w-4 h-4 text-gray-500 group-hover:text-white" />
              Histórico
            </button>

          </div>
        </div>
      </transition>
    </Teleport>
  </div>
</template>