<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import axios from 'axios';
// 1. IMPORTAMOS O GERADOR DE QR CODE AQUI
import QrcodeVue from 'qrcode.vue';
import { 
  ArrowLeft, Wallet, CheckCircle2, Copy, Loader2, AlertCircle, 
  User, Mail, Fingerprint, Pencil 
} from 'lucide-vue-next';
import { useAuthStore } from '../stores/useAuthStore';

const authStore = useAuthStore();
const router = useRouter();
const loading = ref(false);
const error = ref('');
const step = ref(1); 
const pixResult = ref<{ qrCode: string, qrCodeUrl: string | null } | null>(null);
const copied = ref(false);

const form = ref({
  amount: '20,00', 
  name: '',
  cpf: '',
  email: ''
});

// --- Sincronização Robusta ---
const syncUserData = () => {
  const user = authStore.user;
  if (user) {
    form.value.name = user.name || form.value.name;
    let cpfRaw = user.code || user.Code || user.cpf || user.Cpf || user.document;
    
    if (!cpfRaw) {
        try {
            const lsUser = JSON.parse(localStorage.getItem('user') || '{}');
            cpfRaw = lsUser.code || lsUser.cpf;
        } catch (e) {}
    }
    if (cpfRaw) form.value.cpf = cpfRaw;
    const emailRaw = user.email || user.Email;
    if (emailRaw) form.value.email = emailRaw;
  }
};

onMounted(syncUserData);
watch(() => authStore.user, syncUserData, { deep: true });

// --- Lógica de Depósito ---
const parseAmount = (val: string) => parseFloat(val.replace('.', '').replace(',', '.'));

const handleDeposit = async () => {
  error.value = '';
  
  if (!form.value.email || !form.value.email.includes('@')) {
    error.value = 'E-mail inválido. Se não veio automático, digite manualmente.';
    return;
  }
  if (!form.value.cpf) {
    error.value = 'CPF não encontrado. Tente sair e logar novamente.';
    return;
  }

  loading.value = true;

  try {
    const payload = {
      amount: parseAmount(form.value.amount),
      cpf: form.value.cpf, 
      name: form.value.name,
      email: form.value.email
    };

    const response = await axios.post('/core/api/Velana/deposit-pix', payload);
    
    // O console confirmou que a API manda a URL como NULL, mas manda o código
    console.log("PIX GERADO:", response.data);
    pixResult.value = response.data.pix; 
    step.value = 2; 

  } catch (err: any) {
    const msg = err.response?.data?.message || err.response?.data?.error || 'Falha ao gerar PIX';
    error.value = msg;
  } finally {
    loading.value = false;
  }
};

const copyPixCode = () => {
  if (pixResult.value?.qrCode) {
    navigator.clipboard.writeText(pixResult.value.qrCode);
    copied.value = true;
    setTimeout(() => copied.value = false, 2000);
  }
};

const resetForm = () => {
  step.value = 1;
  pixResult.value = null;
  error.value = '';
};
</script>

<template>
  <div class="min-h-screen bg-[#0f172a] p-4 md:p-8 flex justify-center items-start pt-20">
    <div class="w-full max-w-md bg-[#1e293b] border border-gray-700 rounded-2xl shadow-2xl overflow-hidden">
      
      <div class="bg-[#0f172a]/50 p-4 border-b border-gray-700 flex items-center gap-3">
        <button @click="router.back()" class="text-gray-400 hover:text-white transition-colors">
          <ArrowLeft class="w-5 h-5" />
        </button>
        <h2 class="text-white font-bold text-lg flex items-center gap-2">
          <Wallet class="w-5 h-5 text-green-400" />
          Depósito via PIX
        </h2>
      </div>

      <div class="p-6">
        <div v-if="step === 1" class="space-y-6">
          <div class="bg-slate-800/50 rounded-xl p-4 border border-slate-700/50 flex flex-col gap-3 shadow-inner">
            <div class="flex items-center gap-2 mb-2">
                 <CheckCircle2 class="w-3 h-3 text-green-500" />
                 <span class="text-[10px] text-gray-500 uppercase font-bold tracking-widest">Dados da Conta</span>
            </div>
            
            <div class="flex items-center gap-3 text-white font-medium text-sm border-b border-slate-700/50 pb-2">
              <User class="w-4 h-4 text-green-400 shrink-0" />
              <span class="truncate">{{ form.name || 'Usuário' }}</span>
            </div>
            
            <div class="flex items-center gap-3 text-sm border-b border-slate-700/50 pb-2">
              <Fingerprint class="w-4 h-4 text-gray-400 shrink-0" :class="{'text-yellow-400': !form.cpf}" />
              <span v-if="form.cpf" class="font-mono tracking-wide text-xs text-gray-400">{{ form.cpf }}</span>
              <input v-else v-model="form.cpf" type="text" class="bg-slate-900/80 border border-yellow-500/30 rounded px-2 py-1 text-white text-xs w-full focus:outline-none focus:border-yellow-500 font-mono" placeholder="Digite seu CPF" />
              <Pencil v-if="!form.cpf" class="w-3 h-3 text-yellow-500 animate-pulse ml-auto" />
            </div>

            <div class="flex items-center gap-3 text-sm">
              <Mail class="w-4 h-4 text-gray-400 shrink-0" :class="{'text-yellow-400': !form.email}" />
              <span v-if="form.email" class="text-gray-400 text-xs truncate">{{ form.email }}</span>
              <input v-else v-model="form.email" type="email" class="bg-slate-900/80 border border-yellow-500/30 rounded px-2 py-1 text-white text-xs w-full focus:outline-none focus:border-yellow-500 placeholder-gray-500" placeholder="Digite seu e-mail" />
              <Pencil v-if="!form.email" class="w-3 h-3 text-yellow-500 animate-pulse ml-auto" />
            </div>
          </div>

          <div>
            <label class="block text-gray-400 text-xs font-bold uppercase mb-2">Quanto você quer depositar?</label>
            <div class="relative group">
              <span class="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 font-bold text-xl group-focus-within:text-green-500 transition-colors">R$</span>
              <input v-model="form.amount" type="text" class="w-full bg-[#0f172a] border border-gray-600 rounded-xl py-4 pl-12 pr-4 text-white font-bold text-2xl focus:ring-2 focus:ring-green-500/50 focus:border-green-500 outline-none transition-all placeholder-gray-600" placeholder="0,00" />
            </div>
          </div>

          <button @click="handleDeposit" :disabled="loading" class="w-full bg-gradient-to-r from-green-600 to-green-500 hover:from-green-500 hover:to-green-400 text-white font-bold py-4 rounded-xl shadow-lg flex items-center justify-center gap-2 transition-all disabled:opacity-70">
            <Loader2 v-if="loading" class="w-6 h-6 animate-spin" />
            <span v-else>GERAR PIX DE R$ {{ form.amount }}</span>
          </button>

          <div v-if="error" class="bg-red-500/10 border border-red-500/20 p-4 rounded-xl flex items-start gap-3 text-red-400 text-sm">
            <AlertCircle class="w-5 h-5 shrink-0 mt-0.5" />
            <span>{{ error }}</span>
          </div>
        </div>

        <div v-else class="flex flex-col items-center animate-in fade-in zoom-in duration-300">
          
          <div class="bg-white p-4 rounded-2xl mb-6 shadow-2xl ring-4 ring-white/5 flex items-center justify-center">
            <qrcode-vue 
              v-if="pixResult?.qrCode"
              :value="pixResult.qrCode" 
              :size="220" 
              level="H" 
              render-as="svg"
            />
          </div>

          <div class="w-full relative group">
            <textarea readonly class="w-full bg-[#0f172a] border border-gray-600 rounded-xl p-4 text-xs text-gray-400 font-mono resize-none h-24 focus:outline-none" :value="pixResult?.qrCode"></textarea>
            <button @click="copyPixCode" class="absolute bottom-3 right-3 bg-slate-700 hover:bg-slate-600 text-white text-xs px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-all">
              <CheckCircle2 v-if="copied" class="w-3.5 h-3.5 text-green-400" />
              <Copy v-else class="w-3.5 h-3.5" />
              {{ copied ? 'Copiado!' : 'Copiar' }}
            </button>
          </div>
          <button @click="resetForm" class="mt-8 text-gray-500 hover:text-white text-sm font-medium">Voltar</button>
        </div>

      </div>
    </div>
  </div>
</template>