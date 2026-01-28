<script setup lang="ts">
import { ref } from 'vue';
import { X, User, Mail, Lock, Phone, FileText, AlertCircle } from 'lucide-vue-next';
import AuthService from '../services/AuthService';
import { vMaska } from "maska/vue" 

// 1. Definimos as Props para aceitar a aba inicial
const props = defineProps<{
    initialTab?: 'login' | 'register'
}>();

const emit = defineEmits(['close', 'login-success']);

// 2. Usamos a prop para definir o valor inicial. Se não vier nada, usa 'login'
const activeTab = ref<'login' | 'register'>(props.initialTab || 'login');

const isLoading = ref(false);
const errorMessage = ref('');

const form = ref({
  name: '',
  cpf: '',
  email: '',
  phone: '',
  password: ''
});

const handleSubmit = async () => {
    isLoading.value = true;
    errorMessage.value = '';

    try {
        let responseData;

        if (activeTab.value === 'login') {
            if (!form.value.cpf) {
                throw { response: { data: { message: "Digite seu CPF para entrar." } } };
            }
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
            if (backendError.error) {
                errorMessage.value = backendError.error;
            } else if (typeof backendError === 'string') {
                errorMessage.value = backendError;
            } else {
                errorMessage.value = "Dados inválidos. Verifique CPF e senha.";
            }
        } else {
            errorMessage.value = 'Erro de conexão com o servidor.';
        }
    } finally {
        isLoading.value = false;
    }
};
</script>

<template>
  <div class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
    
    <div class="bg-stake-card w-full max-w-md rounded-lg shadow-2xl border border-stake-dark overflow-hidden relative animate-scale-in">
      
      <button @click="$emit('close')" class="absolute top-4 right-4 text-stake-text hover:text-white transition-colors">
        <X class="w-5 h-5" />
      </button>

      <div class="flex border-b border-stake-dark">
        <button 
            @click="activeTab = 'login'; errorMessage = ''"
            class="flex-1 py-4 text-sm font-bold uppercase tracking-wider transition-colors border-b-2"
            :class="activeTab === 'login' ? 'border-stake-blue text-white' : 'border-transparent text-stake-text hover:text-white'"
        >
            Entrar
        </button>
        <button 
            @click="activeTab = 'register'; errorMessage = ''"
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

        <div v-if="errorMessage" class="bg-red-500/10 border border-red-500/50 text-red-500 p-3 rounded text-xs font-bold flex items-center gap-2 mb-4 animate-pulse">
            <AlertCircle class="w-4 h-4 flex-shrink-0" />
            {{ errorMessage }}
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
                    <FileText class="w-4 h-4 absolute left-3 top-3 text-stake-text" />
                    <input v-model="form.cpf" v-maska data-maska="###.###.###-##" type="text" placeholder="000.000.000-00" class="w-full bg-stake-dark border border-transparent focus:border-stake-blue rounded px-10 py-2.5 text-white outline-none text-sm transition-colors" required>
                </div>
            </div>

            <div v-if="activeTab === 'register'" class="space-y-1">
                <label class="text-xs font-bold text-stake-text ml-1">E-mail</label>
                <div class="relative">
                    <Mail class="w-4 h-4 absolute left-3 top-3 text-stake-text" />
                    <input v-model="form.email" type="email" class="w-full bg-stake-dark border border-transparent focus:border-stake-blue rounded px-10 py-2.5 text-white outline-none text-sm transition-colors" required>
                </div>
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
                    <input v-model="form.password" type="password" class="w-full bg-stake-dark border border-transparent focus:border-stake-blue rounded px-10 py-2.5 text-white outline-none text-sm transition-colors" required>
                </div>
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
                @click="activeTab = activeTab === 'login' ? 'register' : 'login'"
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