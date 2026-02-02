<script setup lang="ts">
import { ref, reactive } from 'vue';
import { X, User, Mail, Lock, Phone, FileText, AlertCircle, Eye, EyeOff, Loader2 } from 'lucide-vue-next';
import AuthService from '../services/AuthService';
import { vMaska } from "maska/vue" 

const props = defineProps<{ initialTab?: 'login' | 'register' }>();
const emit = defineEmits(['close', 'login-success']);

const activeTab = ref<'login' | 'register'>(props.initialTab || 'login');
const isLoading = ref(false);
const showPassword = ref(false);
const globalErrorMessage = ref('');
const fieldErrors = reactive({ cpf: '', email: '', password: '' });

const form = ref({ name: '', cpf: '', email: '', phone: '', password: '' });

const switchTab = (tab: 'login' | 'register') => {
    activeTab.value = tab;
    globalErrorMessage.value = '';
    fieldErrors.cpf = ''; fieldErrors.email = ''; fieldErrors.password = '';
};

// Validadores (Simplificados para leitura)
const isValidCPF = (cpf: string) => {
    cpf = cpf.replace(/[^\d]+/g, '');
    if (cpf.length !== 11 || /^(\d)\1{10}$/.test(cpf)) return false;
    let soma = 0, resto;
    for (let i = 1; i <= 9; i++) soma = soma + parseInt(cpf.substring(i-1, i)) * (11 - i);
    resto = (soma * 10) % 11;
    if ((resto == 10) || (resto == 11)) resto = 0;
    if (resto != parseInt(cpf.substring(9, 10))) return false;
    soma = 0;
    for (let i = 1; i <= 10; i++) soma = soma + parseInt(cpf.substring(i-1, i)) * (12 - i);
    resto = (soma * 10) % 11;
    if ((resto == 10) || (resto == 11)) resto = 0;
    if (resto != parseInt(cpf.substring(10, 11))) return false;
    return true;
};
const isValidEmail = (email: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

const handleSubmit = async () => {
    isLoading.value = true;
    globalErrorMessage.value = '';
    Object.keys(fieldErrors).forEach(key => fieldErrors[key as keyof typeof fieldErrors] = '');

    let hasError = false;
    if (!form.value.cpf) { fieldErrors.cpf = 'Obrigatório'; hasError = true; }
    else if (!isValidCPF(form.value.cpf)) { fieldErrors.cpf = 'Inválido'; hasError = true; }

    if (activeTab.value === 'register') {
        if (!form.value.email || !isValidEmail(form.value.email)) { fieldErrors.email = 'Inválido'; hasError = true; }
        if (form.value.password.length < 6) { fieldErrors.password = 'Mínimo 6 chars'; hasError = true; }
    }

    if (hasError) { isLoading.value = false; return; }

    try {
        let responseData;
        if (activeTab.value === 'login') {
            responseData = await AuthService.login(form.value.cpf, form.value.password);
        } else {
            responseData = await AuthService.register(form.value);
        }
        emit('login-success', responseData.user || responseData); 
        emit('close');
    } catch (error: any) {
        if (error.response) {
            const msg = error.response.data.message || error.response.data.error || '';
            if (msg.toLowerCase().includes('cpf')) fieldErrors.cpf = 'Já cadastrado';
            else if (msg.toLowerCase().includes('email')) fieldErrors.email = 'Em uso';
            else if (msg.toLowerCase().includes('senha') || msg.toLowerCase().includes('credentials')) globalErrorMessage.value = 'Dados inválidos';
            else globalErrorMessage.value = typeof msg === 'string' ? msg : "Erro ao processar";
        } else {
            globalErrorMessage.value = 'Erro de conexão';
        }
    } finally {
        isLoading.value = false;
    }
};
</script>

<template>
  <div class="fixed inset-0 z-[10000] flex items-center justify-center p-4 bg-black/90 backdrop-blur-sm animate-fade-in">
    
    <div class="w-full max-w-[340px] bg-[#1a2c38] rounded-xl shadow-2xl border border-gray-700/50 overflow-hidden relative animate-scale-in">
      
      <button 
        @click="$emit('close')" 
        class="absolute top-3 right-3 text-gray-500 hover:text-white transition-colors z-20"
      >
        <X class="w-4 h-4" />
      </button>

      <div class="pt-5 pb-3 px-5 text-center">
        <h2 class="text-white text-lg font-bold mb-4">Bem-vindo</h2>
        
        <div class="bg-[#0f212e] p-1 rounded-lg flex relative">
          <div 
            class="absolute top-1 bottom-1 w-[calc(50%-4px)] bg-[#213743] rounded-md shadow transition-all duration-300 ease-out"
            :class="activeTab === 'login' ? 'left-1' : 'left-[calc(50%+2px)]'"
          ></div>
          <button 
            @click="switchTab('login')" 
            class="flex-1 py-1.5 text-xs font-bold uppercase tracking-wide relative z-10 transition-colors"
            :class="activeTab === 'login' ? 'text-white' : 'text-gray-500 hover:text-gray-300'"
          >
            Entrar
          </button>
          <button 
            @click="switchTab('register')" 
            class="flex-1 py-1.5 text-xs font-bold uppercase tracking-wide relative z-10 transition-colors"
            :class="activeTab === 'register' ? 'text-white' : 'text-gray-500 hover:text-gray-300'"
          >
            Criar Conta
          </button>
        </div>
      </div>

      <div class="px-5 pb-6">
        
        <div v-if="globalErrorMessage" class="mb-3 p-2 bg-red-500/10 border border-red-500/20 rounded text-[10px] text-red-400 flex items-center gap-2 font-bold">
            <AlertCircle class="w-3 h-3" /> {{ globalErrorMessage }}
        </div>

        <form @submit.prevent="handleSubmit" class="space-y-2.5">
            
            <div v-if="activeTab === 'register'" class="space-y-1">
                <div class="relative group">
                    <User class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                    <input v-model="form.name" type="text" placeholder="Nome Completo" class="input-slim pl-9" required>
                </div>
            </div>

            <div class="space-y-1">
                <div class="relative group">
                    <FileText class="w-3.5 h-3.5 absolute left-3 top-2.5 transition-colors" :class="fieldErrors.cpf ? 'text-red-500' : 'text-gray-500 group-focus-within:text-blue-500'" />
                    <input 
                        v-model="form.cpf" 
                        v-maska data-maska="###.###.###-##" 
                        type="text" 
                        placeholder="CPF" 
                        class="input-slim pl-9"
                        :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.cpf }"
                        required
                    >
                    <span v-if="fieldErrors.cpf" class="absolute right-3 top-2.5 text-[9px] text-red-500 font-bold">{{ fieldErrors.cpf }}</span>
                </div>
            </div>

            <div v-if="activeTab === 'register'" class="space-y-1">
                <div class="relative group">
                    <Mail class="w-3.5 h-3.5 absolute left-3 top-2.5 transition-colors" :class="fieldErrors.email ? 'text-red-500' : 'text-gray-500 group-focus-within:text-blue-500'" />
                    <input 
                        v-model="form.email" 
                        type="email" 
                        placeholder="E-mail" 
                        class="input-slim pl-9"
                        :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.email }"
                        required
                    >
                    <span v-if="fieldErrors.email" class="absolute right-3 top-2.5 text-[9px] text-red-500 font-bold">{{ fieldErrors.email }}</span>
                </div>
            </div>

            <div v-if="activeTab === 'register'" class="space-y-1">
                <div class="relative group">
                    <Phone class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                    <input v-model="form.phone" v-maska data-maska="(##) #####-####" type="tel" placeholder="Celular" class="input-slim pl-9">
                </div>
            </div>

            <div class="space-y-1">
                <div class="relative group">
                    <Lock class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                    <input 
                        v-model="form.password" 
                        :type="showPassword ? 'text' : 'password'" 
                        placeholder="Senha" 
                        class="input-slim pl-9 pr-9"
                        :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.password }"
                        required
                    >
                    <button type="button" @click="showPassword = !showPassword" class="absolute right-3 top-2.5 text-gray-500 hover:text-white focus:outline-none">
                        <Eye v-if="!showPassword" class="w-3.5 h-3.5" />
                        <EyeOff v-else class="w-3.5 h-3.5" />
                    </button>
                </div>
                <span v-if="fieldErrors.password" class="text-[9px] text-red-500 font-bold ml-1 block">{{ fieldErrors.password }}</span>
            </div>

            <button 
                type="submit" 
                class="w-full bg-blue-600 hover:bg-blue-500 text-white font-bold text-xs py-3 rounded-lg shadow-lg shadow-blue-900/20 transition-all transform active:scale-95 flex justify-center items-center gap-2 mt-2"
                :disabled="isLoading"
            >
                <Loader2 v-if="isLoading" class="w-3.5 h-3.5 animate-spin" />
                {{ activeTab === 'login' ? 'ENTRAR' : 'CRIAR CONTA' }}
            </button>

        </form>

        <div class="mt-4 text-center">
            <p class="text-[10px] text-gray-500">
                {{ activeTab === 'login' ? 'Novo por aqui?' : 'Já tem conta?' }}
                <button 
                    @click="switchTab(activeTab === 'login' ? 'register' : 'login')"
                    class="text-blue-400 hover:text-blue-300 font-bold ml-1 transition-colors"
                >
                    {{ activeTab === 'login' ? 'Cadastre-se' : 'Entrar' }}
                </button>
            </p>
        </div>

      </div>
    </div>
  </div>
</template>

<style scoped>
.input-slim {
    @apply w-full bg-[#0f212e] border border-gray-700/50 rounded-lg py-2 text-white text-xs outline-none transition-all placeholder-gray-500 focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50;
}

.animate-fade-in { animation: fadeIn 0.2s ease-out; }
.animate-scale-in { animation: scaleIn 0.2s cubic-bezier(0.16, 1, 0.3, 1); }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes scaleIn { from { transform: scale(0.95); opacity: 0; } to { transform: scale(1); opacity: 1; } }
</style>