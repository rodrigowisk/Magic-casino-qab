<script setup lang="ts">
import { ref, reactive } from 'vue';
import { X, User, Mail, Lock, Phone, FileText, AlertCircle, Eye, EyeOff } from 'lucide-vue-next';
import AuthService from '../services/AuthService';
import { vMaska } from "maska/vue" 

const props = defineProps<{
    initialTab?: 'login' | 'register'
}>();

const emit = defineEmits(['close', 'login-success']);

const activeTab = ref<'login' | 'register'>(props.initialTab || 'login');
const isLoading = ref(false);
const showPassword = ref(false);

// Objeto para controlar mensagens de erro globais
const globalErrorMessage = ref('');

// Objeto para controlar erros específicos de cada campo
const fieldErrors = reactive({
    cpf: '',
    email: '',
    password: ''
});

const form = ref({
    name: '',
    cpf: '',
    email: '',
    phone: '',
    password: ''
});

// --- FUNÇÕES AUXILIARES ---

// ✅ CORREÇÃO TS7053: Função dedicada para limpar erros e trocar aba
const switchTab = (tab: 'login' | 'register') => {
    activeTab.value = tab;
    globalErrorMessage.value = '';
    // Limpeza explícita para evitar erro de indexação dinâmica
    fieldErrors.cpf = '';
    fieldErrors.email = '';
    fieldErrors.password = '';
};

// Algoritmo oficial de validação de CPF
const isValidCPF = (cpf: string): boolean => {
    cpf = cpf.replace(/[^\d]+/g, '');
    if (cpf == '') return false;
    if (cpf.length != 11 || 
        cpf == "00000000000" || 
        cpf == "11111111111" || 
        cpf == "22222222222" || 
        cpf == "33333333333" || 
        cpf == "44444444444" || 
        cpf == "55555555555" || 
        cpf == "66666666666" || 
        cpf == "77777777777" || 
        cpf == "88888888888" || 
        cpf == "99999999999")
            return false;
      
    let add = 0;
    for (let i = 0; i < 9; i ++) add += parseInt(cpf.charAt(i)) * (10 - i);
    let rev = 11 - (add % 11);
    if (rev == 10 || rev == 11) rev = 0;
    if (rev != parseInt(cpf.charAt(9))) return false;
      
    add = 0;
    for (let i = 0; i < 10; i ++) add += parseInt(cpf.charAt(i)) * (11 - i);
    rev = 11 - (add % 11);
    if (rev == 10 || rev == 11) rev = 0;
    if (rev != parseInt(cpf.charAt(10))) return false;
          
    return true;
};

const isValidEmail = (email: string): boolean => {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
};

// --- SUBMIT ---

const handleSubmit = async () => {
    isLoading.value = true;
    globalErrorMessage.value = '';
    fieldErrors.cpf = '';
    fieldErrors.email = '';
    fieldErrors.password = '';

    let hasError = false;

    if (!form.value.cpf) {
        fieldErrors.cpf = 'CPF é obrigatório.';
        hasError = true;
    } else if (!isValidCPF(form.value.cpf)) {
        fieldErrors.cpf = 'CPF inválido. Verifique os números.';
        hasError = true;
    }

    if (activeTab.value === 'register') {
        if (!form.value.email || !isValidEmail(form.value.email)) {
            fieldErrors.email = 'Digite um e-mail válido.';
            hasError = true;
        }
        if (form.value.password.length < 6) {
            fieldErrors.password = 'A senha deve ter no mínimo 6 caracteres.';
            hasError = true;
        }
    }

    if (hasError) {
        isLoading.value = false;
        return;
    }

    try {
        let responseData;

        if (activeTab.value === 'login') {
            responseData = await AuthService.login(form.value.cpf, form.value.password);
        } else {
            responseData = await AuthService.register({
                name: form.value.name,
                email: form.value.email,
                cpf: form.value.cpf,
                password: form.value.password,
                phone: form.value.phone
            });
        }

        emit('login-success', responseData.user || responseData); 
        emit('close');

    } catch (error: any) {
        console.error("Erro auth:", error);
        
        if (error.response) {
            const backendError = error.response.data;
            const errorMsg = backendError.message || backendError.error || '';

            if (errorMsg.includes('CPF') || errorMsg.includes('cpf')) {
                fieldErrors.cpf = 'CPF já cadastrado ou inválido.';
            } else if (errorMsg.includes('email') || errorMsg.includes('Email')) {
                fieldErrors.email = 'Este e-mail já está em uso.';
            } else if (errorMsg.includes('senha') || errorMsg.includes('password') || errorMsg.includes('credentials')) {
                globalErrorMessage.value = 'Credenciais inválidas. Verifique CPF e senha.';
            } else {
                globalErrorMessage.value = typeof backendError === 'string' ? backendError : "Ocorreu um erro ao processar.";
            }
        } else {
            globalErrorMessage.value = 'Erro de conexão com o servidor.';
        }
    } finally {
        isLoading.value = false;
    }
};
</script>

<template>
  <div class="fixed inset-0 z-[10000] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
    
    <div class="bg-stake-card w-full max-w-md rounded-lg shadow-2xl border border-stake-dark overflow-hidden relative animate-scale-in">
      
      <button 
        @click="$emit('close')" 
        class="absolute top-2 right-2 z-[60] p-1.5 rounded-md bg-gradient-to-br from-red-500 to-red-700 text-white border border-red-400/30 shadow-[0_2px_4px_rgba(220,38,38,0.4),inset_0_1px_0_rgba(255,255,255,0.2)] hover:from-red-400 hover:to-red-600 active:scale-95 transition-all duration-150 group"
        title="Fechar"
      >
        <X class="w-4 h-4 group-hover:rotate-90 transition-transform duration-200" />
      </button>

      <div class="flex border-b border-stake-dark relative z-10 mt-1"> 
        <button 
            @click="switchTab('login')"
            class="flex-1 py-4 text-sm font-bold uppercase tracking-wider transition-colors border-b-2"
            :class="activeTab === 'login' ? 'border-stake-blue text-white' : 'border-transparent text-stake-text hover:text-white'"
        >
            Entrar
        </button>
        <button 
            @click="switchTab('register')"
            class="flex-1 py-4 text-sm font-bold uppercase tracking-wider transition-colors border-b-2"
            :class="activeTab === 'register' ? 'border-stake-blue text-white' : 'border-transparent text-stake-text hover:text-white'"
        >
            Cadastre-se
        </button>
      </div>

      <div class="p-6 space-y-4">
        
        <h2 class="text-white text-xl font-bold text-center mb-6">
            {{ activeTab === 'login' ? 'Acesse sua conta' : 'Crie sua conta grátis' }}
        </h2>

        <div v-if="globalErrorMessage" class="bg-red-500/10 border border-red-500/50 text-red-500 p-3 rounded text-xs font-bold flex items-center gap-2 mb-4 animate-pulse">
            <AlertCircle class="w-4 h-4 flex-shrink-0" />
            {{ globalErrorMessage }}
        </div>

        <form @submit.prevent="handleSubmit" class="space-y-4">
            
            <div v-if="activeTab === 'register'" class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">Nome Completo</label>
                <div class="relative">
                    <User class="w-4 h-4 absolute left-3 top-3 text-stake-text" />
                    <input v-model="form.name" type="text" class="w-full bg-stake-dark border border-transparent focus:border-stake-blue rounded px-10 py-2.5 text-white outline-none text-sm transition-colors" required>
                </div>
            </div>

            <div class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">CPF</label>
                <div class="relative">
                    <FileText class="w-4 h-4 absolute left-3 top-3" :class="fieldErrors.cpf ? 'text-red-500' : 'text-stake-text'" />
                    <input 
                        v-model="form.cpf" 
                        v-maska data-maska="###.###.###-##" 
                        type="text" 
                        placeholder="000.000.000-00" 
                        class="w-full bg-stake-dark border rounded px-10 py-2.5 text-white outline-none text-sm transition-colors"
                        :class="fieldErrors.cpf ? 'border-red-500 focus:border-red-500' : 'border-transparent focus:border-stake-blue'"
                        required
                    >
                </div>
                <span v-if="fieldErrors.cpf" class="text-[10px] text-red-500 font-bold ml-1 block">{{ fieldErrors.cpf }}</span>
            </div>

            <div v-if="activeTab === 'register'" class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">E-mail</label>
                <div class="relative">
                    <Mail class="w-4 h-4 absolute left-3 top-3" :class="fieldErrors.email ? 'text-red-500' : 'text-stake-text'" />
                    <input 
                        v-model="form.email" 
                        type="email" 
                        class="w-full bg-stake-dark border rounded px-10 py-2.5 text-white outline-none text-sm transition-colors"
                        :class="fieldErrors.email ? 'border-red-500 focus:border-red-500' : 'border-transparent focus:border-stake-blue'"
                        required
                    >
                </div>
                <span v-if="fieldErrors.email" class="text-[10px] text-red-500 font-bold ml-1 block">{{ fieldErrors.email }}</span>
            </div>

            <div v-if="activeTab === 'register'" class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">Celular</label>
                <div class="relative">
                    <Phone class="w-4 h-4 absolute left-3 top-3 text-stake-text" />
                    <input v-model="form.phone" v-maska data-maska="(##) #####-####" type="tel" class="w-full bg-stake-dark border border-transparent focus:border-stake-blue rounded px-10 py-2.5 text-white outline-none text-sm transition-colors">
                </div>
            </div>

            <div class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">Senha</label>
                <div class="relative">
                    <Lock class="w-4 h-4 absolute left-3 top-3 text-stake-text" />
                    <input 
                        v-model="form.password" 
                        :type="showPassword ? 'text' : 'password'" 
                        class="w-full bg-stake-dark border rounded px-10 py-2.5 text-white outline-none text-sm transition-colors pr-10" 
                        :class="fieldErrors.password ? 'border-red-500 focus:border-red-500' : 'border-transparent focus:border-stake-blue'"
                        required
                    >
                    <button 
                        type="button"
                        @click="showPassword = !showPassword"
                        class="absolute right-3 top-3 text-stake-text hover:text-white transition-colors focus:outline-none"
                    >
                        <Eye v-if="!showPassword" class="w-4 h-4" />
                        <EyeOff v-else class="w-4 h-4" />
                    </button>
                </div>
                <span v-if="fieldErrors.password" class="text-[10px] text-red-500 font-bold ml-1 block">{{ fieldErrors.password }}</span>
            </div>

            <button 
                type="submit" 
                class="w-full bg-stake-blue hover:brightness-110 text-white font-bold py-3 rounded shadow-lg shadow-blue-500/20 transition-all mt-4 flex justify-center items-center gap-2"
                :disabled="isLoading"
            >
                <span v-if="isLoading" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                {{ activeTab === 'login' ? 'Entrar com CPF' : 'Criar Conta' }}
            </button>

        </form>

        <p class="text-center text-xs text-stake-text mt-4">
            {{ activeTab === 'login' ? 'Não tem uma conta?' : 'Já tem uma conta?' }}
            <button 
                @click="switchTab(activeTab === 'login' ? 'register' : 'login')"
                class="text-white font-bold hover:underline ml-1"
            >
                {{ activeTab === 'login' ? 'Cadastre-se' : 'Fazer Login' }}
            </button>
        </p>

      </div>
    </div>
  </div>
</template>

<style scoped>
.animate-fade-in { animation: fadeIn 0.2s ease-out; }
.animate-scale-in { animation: scaleIn 0.2s ease-out; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes scaleIn { from { transform: scale(0.95); opacity: 0; } to { transform: scale(1); opacity: 1; } }
</style>