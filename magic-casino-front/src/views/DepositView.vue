<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import axios from 'axios';
import QrcodeVue from 'qrcode.vue';
import { 
  ArrowLeft, Wallet, CheckCircle2, Copy, Loader2, AlertCircle, 
  User, Mail, Fingerprint, Pencil, QrCode
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
    error.value = 'E-mail inválido.';
    return;
  }
  if (!form.value.cpf) {
    error.value = 'CPF não encontrado.';
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
  <div class="min-h-screen bg-[#0f212e] p-4 flex justify-center items-start pt-10 font-sans">
    
    <div class="w-full max-w-sm bg-[#1a2c38] border border-gray-700/50 rounded-xl shadow-2xl overflow-hidden animate-fade-in-up">
      
      <div class="bg-[#15222b] px-4 py-3 border-b border-gray-700/50 flex items-center justify-between">
        <div class="flex items-center gap-2">
          <button @click="router.back()" class="text-gray-500 hover:text-white transition-colors">
            <ArrowLeft class="w-4 h-4" />
          </button>
          <h2 class="text-white font-bold text-sm flex items-center gap-2">
            <Wallet class="w-4 h-4 text-green-500" />
            Depósito PIX
          </h2>
        </div>
        <div class="flex items-center gap-1.5 px-2 py-0.5 bg-green-500/10 border border-green-500/20 rounded text-[10px] text-green-400 font-bold uppercase tracking-wider">
          <QrCode class="w-3 h-3" />
          Instantâneo
        </div>
      </div>

      <div class="p-4">
        
        <div v-if="step === 1" class="space-y-4">
          
          <div class="bg-[#0f212e] rounded-lg p-3 border border-gray-700/50 flex flex-col gap-2">
            <div class="flex items-center justify-between border-b border-gray-700/50 pb-1.5">
              <div class="flex items-center gap-2 text-xs text-gray-300">
                <User class="w-3.5 h-3.5 text-green-500" />
                <span class="truncate max-w-[150px]">{{ form.name || 'Usuário' }}</span>
              </div>
              <span class="text-[9px] text-gray-500 font-mono uppercase">Titular</span>
            </div>
            
            <div class="flex items-center justify-between border-b border-gray-700/50 pb-1.5">
              <div class="flex items-center gap-2 text-xs">
                <Fingerprint class="w-3.5 h-3.5 text-gray-500" />
                <span v-if="form.cpf" class="font-mono text-gray-300">{{ form.cpf }}</span>
                <input v-else v-model="form.cpf" type="text" class="bg-transparent border-b border-yellow-500/50 text-white w-32 focus:outline-none text-xs" placeholder="CPF..." />
              </div>
              <Pencil v-if="!form.cpf" class="w-3 h-3 text-yellow-500" />
            </div>

            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2 text-xs">
                <Mail class="w-3.5 h-3.5 text-gray-500" />
                <span v-if="form.email" class="text-gray-300 truncate max-w-[180px]">{{ form.email }}</span>
                <input v-else v-model="form.email" type="email" class="bg-transparent border-b border-yellow-500/50 text-white w-40 focus:outline-none text-xs" placeholder="E-mail..." />
              </div>
              <Pencil v-if="!form.email" class="w-3 h-3 text-yellow-500" />
            </div>
          </div>

          <div>
            <label class="block text-gray-500 text-[10px] font-bold uppercase mb-1.5 pl-1">Valor do Depósito</label>
            <div class="relative group">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 font-bold text-sm group-focus-within:text-green-500 transition-colors">R$</span>
              <input 
                v-model="form.amount" 
                type="text" 
                class="w-full bg-[#0f212e] border border-gray-600 rounded-lg py-2.5 pl-9 pr-3 text-white font-bold text-lg focus:border-green-500 focus:ring-1 focus:ring-green-500/50 outline-none transition-all placeholder-gray-600 font-mono tracking-tight" 
                placeholder="0,00" 
              />
            </div>
          </div>

          <button 
            @click="handleDeposit" 
            :disabled="loading" 
            class="w-full bg-green-600 hover:bg-green-500 text-white font-bold text-xs py-3 rounded-lg shadow-lg shadow-green-900/20 flex items-center justify-center gap-2 transition-all transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-70 disabled:cursor-not-allowed uppercase tracking-wide"
          >
            <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
            <span v-else>Gerar PIX</span>
          </button>

          <div v-if="error" class="bg-red-500/10 border border-red-500/20 p-2.5 rounded-lg flex items-start gap-2 text-red-400 text-xs">
            <AlertCircle class="w-4 h-4 shrink-0 mt-0.5" />
            <span class="leading-tight">{{ error }}</span>
          </div>
        </div>

        <div v-else class="flex flex-col items-center animate-fade-in text-center">
          
          <div class="text-white font-bold text-sm mb-1">Pagamento Gerado!</div>
          <p class="text-gray-400 text-[10px] mb-4">Escaneie ou copie o código abaixo.</p>

          <div class="bg-white p-3 rounded-xl mb-4 shadow-lg ring-2 ring-white/10">
            <qrcode-vue 
              v-if="pixResult?.qrCode"
              :value="pixResult.qrCode" 
              :size="160" 
              level="M" 
              render-as="svg"
            />
          </div>

          <div class="w-full bg-[#0f212e] border border-gray-700 rounded-lg p-2 flex items-center gap-2 mb-4 group relative overflow-hidden">
            <div class="flex-1 overflow-hidden">
              <p class="text-[10px] text-gray-500 font-mono truncate select-all">{{ pixResult?.qrCode }}</p>
            </div>
            <button 
              @click="copyPixCode" 
              class="shrink-0 bg-green-600 hover:bg-green-500 text-white p-1.5 rounded transition-colors"
              title="Copiar"
            >
              <CheckCircle2 v-if="copied" class="w-3.5 h-3.5" />
              <Copy v-else class="w-3.5 h-3.5" />
            </button>
            <div v-if="copied" class="absolute inset-0 bg-green-600/90 flex items-center justify-center text-white text-xs font-bold backdrop-blur-sm transition-all">
              Copiado com sucesso!
            </div>
          </div>

          <button @click="resetForm" class="text-gray-500 hover:text-white text-xs font-bold underline decoration-gray-600 underline-offset-4 transition-colors">
            Voltar e depositar outro valor
          </button>
        </div>

      </div>
    </div>
  </div>
</template>

<style scoped>
.animate-fade-in-up { animation: fadeInUp 0.4s ease-out; }
.animate-fade-in { animation: fadeIn 0.3s ease-out; }

@keyframes fadeInUp { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>