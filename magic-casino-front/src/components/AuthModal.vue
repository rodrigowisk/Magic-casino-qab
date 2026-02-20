<script setup lang="ts">
import { ref, reactive, watch } from 'vue';
import { X, User, Mail, Lock, Phone, FileText, AlertCircle, Eye, EyeOff, Loader2, AtSign } from 'lucide-vue-next';
import AuthService from '../services/AuthService';
import { vMaska } from "maska/vue" 
import { useAuthStore } from '../stores/useAuthStore';

const props = defineProps<{ initialTab?: 'login' | 'register' }>();
const emit = defineEmits(['close', 'login-success']);

const authStore = useAuthStore();
const activeTab = ref<'login' | 'register'>(props.initialTab || 'login');
const isLoading = ref(false);
const showPassword = ref(false); // Controla visualização da senha (compartilhado visualmente, mas não os dados)
const globalErrorMessage = ref('');

// Estados para validação (Cadastro)
const isCheckingUser = ref(false);
let debounceTimer: any = null;
const fieldErrors = reactive({ cpf: '', email: '', password: '', userName: '' });

// ✅ CORREÇÃO 1: ESTADOS SEPARADOS PARA LOGIN E CADASTRO
// Isso impede que limpar um afete o outro
const loginForm = ref({ cpf: '', password: '' });

const initialRegisterState = { name: '', userName: '', cpf: '', email: '', phone: '', password: '' };
const registerForm = ref({ ...initialRegisterState });

const switchTab = (tab: 'login' | 'register') => {
    activeTab.value = tab;
    globalErrorMessage.value = '';
    
    // Limpa erros visuais ao trocar de aba
    Object.keys(fieldErrors).forEach(key => fieldErrors[key as keyof typeof fieldErrors] = '');

    // ✅ Se for para a aba CADASTRO, limpa o formulário de cadastro para começar do zero.
    // ✅ Se for para a aba LOGIN, NÃO FAZ NADA (mantém os dados salvos/autofill).
    if (tab === 'register') {
        registerForm.value = { ...initialRegisterState };
    }
};

// --- VALIDAÇÃO EM TEMPO REAL (Só monitora o registerForm) ---
watch(() => registerForm.value.userName, (newVal) => {
    if (activeTab.value !== 'register') return;

    clearTimeout(debounceTimer);
    fieldErrors.userName = '';

    if (!newVal) return;
    if (/\s/.test(newVal)) { fieldErrors.userName = 'Sem espaços'; return; }
    if (newVal.length < 3) return;

    isCheckingUser.value = true;

    debounceTimer = setTimeout(async () => {
        try {
            const res = await AuthService.checkUserAvailability(newVal);
            if (!res.available) {
                fieldErrors.userName = 'Indisponível (Em uso)';
            }
        } catch (error) {
            // Silencioso
        } finally {
            isCheckingUser.value = false;
        }
    }, 500);
});

// Validadores
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
    
    // Preserva erro de validação assíncrona
    const currentUserError = fieldErrors.userName;
    Object.keys(fieldErrors).forEach(key => fieldErrors[key as keyof typeof fieldErrors] = '');
    if (currentUserError) fieldErrors.userName = currentUserError;

    let hasError = false;
    
    // Validações APENAS para Cadastro
    if (activeTab.value === 'register') {
        if (!registerForm.value.userName) { fieldErrors.userName = 'Obrigatório'; hasError = true; }
        else if (registerForm.value.userName.length < 3) { fieldErrors.userName = 'Mínimo 3 letras'; hasError = true; }
        else if (fieldErrors.userName) { hasError = true; }

        if (!registerForm.value.cpf) { fieldErrors.cpf = 'Obrigatório'; hasError = true; }
        else if (!isValidCPF(registerForm.value.cpf)) { fieldErrors.cpf = 'Inválido'; hasError = true; }

        if (!registerForm.value.email || !isValidEmail(registerForm.value.email)) { fieldErrors.email = 'Inválido'; hasError = true; }
        if (registerForm.value.password.length < 6) { fieldErrors.password = 'Mínimo 6 chars'; hasError = true; }
    } 
    else {
        // Validação Login
        if (!loginForm.value.cpf) { fieldErrors.cpf = 'Obrigatório'; hasError = true; }
        if (!loginForm.value.password) { fieldErrors.password = 'Obrigatório'; hasError = true; }
    }

    if (hasError) { isLoading.value = false; return; }

    try {
        let responseData;

        if (activeTab.value === 'login') {
            // Usa loginForm
            responseData = await AuthService.login(loginForm.value.cpf, loginForm.value.password);
        } else {
            // Usa registerForm
            await AuthService.register(registerForm.value);
            // Login automático com os dados do registro
            responseData = await AuthService.login(registerForm.value.cpf, registerForm.value.password);
        }
        
        const userData = responseData.user || responseData;
        const tokenData = responseData.token || responseData.accessToken;

        // Atualiza Store (Isso deve corrigir o botão "Comprar" -> "Jogar" no Lobby)
        if (tokenData && authStore.setLogin) {
            authStore.setLogin(userData, tokenData);
        } else {
            authStore.user = userData;
            if (tokenData) authStore.token = tokenData;
        }

        if (tokenData) localStorage.setItem('token', tokenData);
        localStorage.setItem('user', JSON.stringify(userData));

        emit('login-success', userData); 
        emit('close');

    } catch (error: any) {
        if (error.response) {
            const msg = error.response.data.message || error.response.data.error || '';
            const msgLower = typeof msg === 'string' ? msg.toLowerCase() : '';
            
            if (msgLower.includes('cpf')) fieldErrors.cpf = 'Já cadastrado/Inválido';
            else if (msgLower.includes('email')) fieldErrors.email = 'Em uso';
            else if (msgLower.includes('nome') || msgLower.includes('usuário')) fieldErrors.userName = 'Indisponível';
            else if (msgLower.includes('senha') || msgLower.includes('credentials')) globalErrorMessage.value = 'Dados incorretos';
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
  <div class="fixed inset-0 z-[99999] w-screen h-screen flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
    
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
            
            <template v-if="activeTab === 'register'">
                <div class="space-y-1">
                    <div class="relative group">
                        <User class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                        <input v-model="registerForm.name" type="text" placeholder="Nome Completo" class="input-slim pl-9" required autocomplete="new-password">
                    </div>
                </div>

                <div class="space-y-1">
                    <div class="relative group">
                        <AtSign class="w-3.5 h-3.5 absolute left-3 top-2.5 transition-colors" 
                                :class="fieldErrors.userName ? 'text-red-500' : 'text-gray-500 group-focus-within:text-blue-500'" />
                        
                        <input 
                            v-model="registerForm.userName" 
                            type="text" 
                            placeholder="Usuário (Login)" 
                            class="input-slim pl-9 pr-8"
                            :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.userName }"
                            required
                            autocomplete="new-password"
                        >
                        
                        <div class="absolute right-3 top-2.5">
                            <Loader2 v-if="isCheckingUser" class="w-3.5 h-3.5 text-blue-400 animate-spin" />
                            <span v-else-if="fieldErrors.userName" class="text-[9px] text-red-500 font-bold whitespace-nowrap">{{ fieldErrors.userName }}</span>
                        </div>
                    </div>
                </div>
            </template>

            <div class="space-y-1">
                <div class="relative group">
                    <template v-if="activeTab === 'register'">
                        <FileText class="w-3.5 h-3.5 absolute left-3 top-2.5 transition-colors" :class="fieldErrors.cpf ? 'text-red-500' : 'text-gray-500 group-focus-within:text-blue-500'" />
                        <input 
                            v-model="registerForm.cpf" 
                            v-maska data-maska="###.###.###-##" 
                            type="text" 
                            placeholder="CPF" 
                            class="input-slim pl-9"
                            :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.cpf }"
                            required
                            autocomplete="new-password"
                        >
                    </template>
                    <template v-else>
                        <User class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                        <input 
                            v-model="loginForm.cpf" 
                            type="text" 
                            placeholder="Nome de Usuário/Apelido ou CPF" 
                            class="input-slim pl-9"
                            required
                            autocomplete="username"
                        >
                    </template>

                    <span v-if="activeTab === 'register' && fieldErrors.cpf" class="absolute right-3 top-2.5 text-[9px] text-red-500 font-bold">{{ fieldErrors.cpf }}</span>
                </div>
            </div>

            <template v-if="activeTab === 'register'">
                <div class="space-y-1">
                    <div class="relative group">
                        <Mail class="w-3.5 h-3.5 absolute left-3 top-2.5 transition-colors" :class="fieldErrors.email ? 'text-red-500' : 'text-gray-500 group-focus-within:text-blue-500'" />
                        <input 
                            v-model="registerForm.email" 
                            type="email" 
                            placeholder="E-mail" 
                            class="input-slim pl-9"
                            :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.email }"
                            required
                            autocomplete="new-password"
                        >
                        <span v-if="fieldErrors.email" class="absolute right-3 top-2.5 text-[9px] text-red-500 font-bold">{{ fieldErrors.email }}</span>
                    </div>
                </div>

                <div class="space-y-1">
                    <div class="relative group">
                        <Phone class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                        <input v-model="registerForm.phone" v-maska data-maska="(##) #####-####" type="tel" placeholder="Celular" class="input-slim pl-9" autocomplete="new-password">
                    </div>
                </div>
            </template>

            <div class="space-y-1">
                <div class="relative group">
                    <Lock class="w-3.5 h-3.5 absolute left-3 top-2.5 text-gray-500 group-focus-within:text-blue-500 transition-colors" />
                    
                    <input 
                        v-if="activeTab === 'register'"
                        v-model="registerForm.password" 
                        :type="showPassword ? 'text' : 'password'" 
                        placeholder="Senha" 
                        class="input-slim pl-9 pr-9"
                        :class="{ 'border-red-500/50 focus:border-red-500': fieldErrors.password }"
                        required
                        autocomplete="new-password"
                    >
                    <input 
                        v-else
                        v-model="loginForm.password" 
                        :type="showPassword ? 'text' : 'password'" 
                        placeholder="Senha" 
                        class="input-slim pl-9 pr-9"
                        required
                        autocomplete="current-password"
                    >

                    <button type="button" @click="showPassword = !showPassword" class="absolute right-3 top-2.5 text-gray-500 hover:text-white focus:outline-none">
                        <Eye v-if="!showPassword" class="w-3.5 h-3.5" />
                        <EyeOff v-else class="w-3.5 h-3.5" />
                    </button>
                </div>
                <span v-if="activeTab === 'register' && fieldErrors.password" class="text-[9px] text-red-500 font-bold ml-1 block">{{ fieldErrors.password }}</span>
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