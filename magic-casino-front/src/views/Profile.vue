<template>
  <div class="min-h-screen bg-[#0f212e] text-white p-4 font-sans flex flex-col items-center justify-center md:block">
    
    <div class="w-full max-w-5xl mx-auto">
      
      <div class="mb-4 relative pl-2">
        <div class="absolute -top-6 -left-6 w-24 h-24 bg-green-500/10 rounded-full blur-2xl"></div>
        <h1 class="text-2xl font-bold text-white flex items-center gap-2 relative z-10">
          <UserCircle class="w-7 h-7 text-green-500" />
          Minha Conta
        </h1>
        <p class="text-xs text-gray-400 relative z-10">Gerencie seus dados e segurança.</p>
      </div>

      <div v-if="loading" class="flex flex-col items-center justify-center py-20">
        <div class="w-10 h-10 border-4 border-green-500/30 border-t-green-500 rounded-full animate-spin"></div>
        <p class="mt-3 text-sm text-gray-400 animate-pulse">Carregando...</p>
      </div>

      <div v-else-if="errorMsg" class="bg-red-500/10 border border-red-500/20 rounded-xl p-4 text-center mx-auto max-w-lg">
        <AlertTriangle class="w-8 h-8 text-red-500 mx-auto mb-2" />
        <p class="text-red-200 text-sm mb-3">{{ errorMsg }}</p>
        <button @click="loadProfile" class="px-4 py-1.5 bg-red-600 hover:bg-red-500 rounded text-white text-xs font-bold transition-colors">
          Tentar Novamente
        </button>
      </div>

      <div v-else class="grid grid-cols-1 md:grid-cols-12 gap-4 animate-fade-in-up">
        
        <div class="md:col-span-4 space-y-4">
          
          <div class="bg-[#1a2c38] rounded-xl border border-gray-700/50 shadow-lg relative overflow-hidden group">
            <div class="absolute top-0 w-full h-16 bg-gradient-to-b from-green-500/10 to-transparent"></div>
            
            <div class="p-5 flex flex-row md:flex-col items-center gap-4 md:gap-2 relative">
              <div class="relative">
                <div class="w-16 h-16 md:w-20 md:h-20 rounded-full bg-[#0f212e] border-2 border-green-500 flex items-center justify-center shadow-lg">
                  <User class="w-8 h-8 md:w-10 md:h-10 text-gray-400" />
                </div>
                <div class="absolute bottom-0 right-0 bg-green-500 w-4 h-4 rounded-full border-2 border-[#1a2c38]"></div>
              </div>

              <div class="text-left md:text-center flex-1">
                <h2 class="text-lg font-bold text-white truncate">{{ form.name || 'Usuário' }}</h2>
                <div class="flex items-center md:justify-center gap-1 text-xs text-gray-400 mb-2">
                  <ShieldCheck class="w-3 h-3 text-green-500" /> Verificado
                </div>
                <div class="inline-flex items-center bg-[#0f212e] rounded px-2 py-1 gap-2 border border-gray-700/50">
                  <span class="text-[10px] text-gray-400 uppercase font-bold">VIP</span>
                  <span class="text-[10px] text-green-400 font-bold">OURO</span>
                </div>
              </div>
            </div>
          </div>

          <div class="bg-[#1a2c38] rounded-xl p-4 border border-gray-700/50 shadow-lg">
            <h3 class="text-white text-sm font-bold flex items-center gap-2 mb-3">
              <Lock class="w-3 h-3 text-green-500" /> Segurança
            </h3>
            <ul class="space-y-2 text-xs text-gray-400">
              <li class="flex items-center gap-2"><CheckCircle class="w-3 h-3 text-green-500" /> E-mail verificado</li>
              <li class="flex items-center gap-2"><CheckCircle class="w-3 h-3 text-green-500" /> CPF vinculado</li>
            </ul>
          </div>
        </div>

        <div class="md:col-span-8 relative">
          
          <div class="bg-[#1a2c38] rounded-xl border border-gray-700/50 shadow-lg overflow-hidden h-full flex flex-col relative">
            
            <transition name="fade">
              <div v-if="notification.show" class="absolute inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 rounded-xl">
                <div class="bg-[#0f212e] border border-gray-600 p-6 rounded-2xl shadow-2xl flex flex-col items-center gap-3 max-w-xs text-center animate-scale-in">
                  
                  <CheckCircle v-if="notification.type === 'success'" class="w-12 h-12 text-green-500" />
                  <AlertTriangle v-if="notification.type === 'warning'" class="w-12 h-12 text-yellow-500" />
                  <XCircle v-if="notification.type === 'error'" class="w-12 h-12 text-red-500" />
                  
                  <h3 class="text-white font-bold text-lg">
                    {{ notification.type === 'success' ? 'Sucesso!' : notification.type === 'error' ? 'Erro' : 'Atenção' }}
                  </h3>
                  <p class="text-gray-300 text-sm leading-relaxed">{{ notification.message }}</p>
                  
                  <button @click="notification.show = false" class="mt-2 px-6 py-2 bg-gray-700 hover:bg-gray-600 rounded-lg text-xs font-bold transition-colors text-white">
                    Fechar
                  </button>
                </div>
              </div>
            </transition>

            <div class="flex border-b border-gray-700 bg-[#0f212e]/30">
              <button 
                @click="changeTab('personal')"
                class="flex-1 py-3 text-xs md:text-sm font-bold uppercase tracking-wide transition-colors flex items-center justify-center gap-2 border-b-2 outline-none"
                :class="activeTab === 'personal' ? 'border-green-500 text-white bg-[#1a2c38]' : 'border-transparent text-gray-500 hover:text-gray-300'"
              >
                <User class="w-3 h-3 md:w-4 md:h-4" /> Dados
              </button>
              <button 
                @click="changeTab('security')"
                class="flex-1 py-3 text-xs md:text-sm font-bold uppercase tracking-wide transition-colors flex items-center justify-center gap-2 border-b-2 outline-none"
                :class="activeTab === 'security' ? 'border-green-500 text-white bg-[#1a2c38]' : 'border-transparent text-gray-500 hover:text-gray-300'"
              >
                <Key class="w-3 h-3 md:w-4 md:h-4" /> Senha
              </button>
            </div>

            <div class="p-5 flex-1">

              <form v-if="activeTab === 'personal'" @submit.prevent="updateProfile" class="space-y-3 animate-fade-in h-full flex flex-col justify-between">
                
                <div class="space-y-3">
                  <div class="group">
                    <label class="label-compact">CPF</label>
                    <div class="relative opacity-70">
                      <FileText class="icon-input" />
                      <input type="text" v-model="form.cpf" disabled class="input-compact pl-9 cursor-not-allowed font-mono" />
                      <Lock class="absolute right-3 top-2.5 w-3 h-3 text-gray-600" />
                    </div>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <div>
                      <label class="label-compact">Nome</label>
                      <div class="relative">
                        <User class="icon-input text-green-500" />
                        <input type="text" v-model="form.name" class="input-compact pl-9" placeholder="Seu nome" />
                      </div>
                    </div>
                    <div>
                      <label class="label-compact">Celular</label>
                      <div class="relative">
                        <Phone class="icon-input text-green-500" />
                        <input type="text" v-model="form.phone" class="input-compact pl-9" placeholder="(00) 00000-0000" />
                      </div>
                    </div>
                  </div>

                  <div>
                    <label class="label-compact">E-mail</label>
                    <div class="relative">
                      <Mail class="icon-input text-green-500" />
                      <input type="email" v-model="form.email" class="input-compact pl-9" placeholder="seu@email.com" />
                    </div>
                  </div>
                </div>

                <div class="pt-3 flex items-center justify-between border-t border-gray-700/50 mt-2">
                  <span class="text-[10px] text-gray-500">* Campos obrigatórios</span>
                  <button type="submit" :disabled="saving" class="btn-compact">
                    <Loader2 v-if="saving" class="w-4 h-4 animate-spin" />
                    <Save v-else class="w-4 h-4" />
                    {{ saving ? 'Salvar' : 'Salvar Alterações' }}
                  </button>
                </div>
              </form>

              <form v-if="activeTab === 'security'" @submit.prevent="changePassword" class="space-y-3 animate-fade-in h-full flex flex-col justify-between">
                
                <div class="space-y-3">
                  <div class="bg-blue-500/10 border border-blue-500/20 rounded p-3 flex items-start gap-2">
                    <ShieldCheck class="w-4 h-4 text-blue-400 mt-0.5" />
                    <div>
                      <h4 class="text-xs font-bold text-blue-100">Dica de Segurança</h4>
                      <p class="text-[10px] text-blue-200/70 leading-tight">Use senhas complexas com símbolos e números.</p>
                    </div>
                  </div>

                  <div>
                    <label class="label-compact">Senha Atual</label>
                    <div class="relative">
                      <Key class="icon-input text-gray-500" />
                      
                      <input 
                        :type="showCurrentPass ? 'text' : 'password'" 
                        v-model="passForm.currentPassword" 
                        required 
                        class="input-compact pl-9 pr-10" 
                        placeholder="••••••" 
                      />
                      
                      <button type="button" @click="showCurrentPass = !showCurrentPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                        <Eye v-if="!showCurrentPass" class="w-4 h-4" />
                        <EyeOff v-else class="w-4 h-4 text-green-500" />
                      </button>
                    </div>
                  </div>

                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="label-compact">Nova Senha</label>
                      <div class="relative">
                        <Lock class="icon-input text-green-500" />
                        
                        <input 
                          :type="showNewPass ? 'text' : 'password'"
                          v-model="passForm.newPassword" 
                          required 
                          class="input-compact pl-9 pr-10" 
                          placeholder="Nova senha" 
                        />
                        
                        <button type="button" @click="showNewPass = !showNewPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                            <Eye v-if="!showNewPass" class="w-4 h-4" />
                            <EyeOff v-else class="w-4 h-4 text-green-500" />
                        </button>
                      </div>
                    </div>

                    <div>
                      <label class="label-compact">Confirmar</label>
                      <div class="relative">
                        <Lock class="icon-input text-green-500" />
                        
                        <input 
                          :type="showConfirmPass ? 'text' : 'password'"
                          v-model="passForm.confirmPassword" 
                          required 
                          class="input-compact pl-9 pr-10" 
                          placeholder="Repita senha" 
                        />
                        
                        <button type="button" @click="showConfirmPass = !showConfirmPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                            <Eye v-if="!showConfirmPass" class="w-4 h-4" />
                            <EyeOff v-else class="w-4 h-4 text-green-500" />
                        </button>
                      </div>
                    </div>
                  </div>
                </div>

                <div class="pt-3 flex items-center justify-end border-t border-gray-700/50 mt-2">
                  <button type="submit" :disabled="savingPass" class="btn-compact bg-blue-600 hover:bg-blue-500 shadow-blue-900/20">
                    <Loader2 v-if="savingPass" class="w-4 h-4 animate-spin" />
                    <Lock v-else class="w-4 h-4" />
                    {{ savingPass ? 'Alterando...' : 'Atualizar Senha' }}
                  </button>
                </div>
              </form>

            </div>
          </div>
        </div>

      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router'; // ✅ UseRoute Importado
import axios from 'axios';
import { 
  User, Mail, Phone, Lock, FileText, Save, Loader2, 
  AlertTriangle, CheckCircle, ShieldCheck, UserCircle, Key, XCircle,
  Eye, EyeOff 
} from 'lucide-vue-next';

// --- ROUTER ---
const route = useRoute(); // ✅ Inicialização do Route
const router = useRouter();

// --- ESTADOS ---
const notification = ref({
  show: false,
  message: '',
  type: 'success' as 'success' | 'error' | 'warning'
});

const showNotify = (msg: string, type: 'success' | 'error' | 'warning' = 'success') => {
  notification.value = { show: true, message: msg, type };
  setTimeout(() => { notification.value.show = false; }, 3000);
};

// --- CONTROLE DE ABAS ---
const activeTab = ref<'personal' | 'security'>('personal');

// Função para mudar aba e atualizar a URL (opcional, mas bom para UX)
const changeTab = (tab: 'personal' | 'security') => {
  activeTab.value = tab;
  // Atualiza a URL sem recarregar para o link ficar compartilhável
  router.replace({ query: { ...route.query, tab } });
};

const loading = ref(true);
const errorMsg = ref('');
const saving = ref(false);
const savingPass = ref(false);

// ✅ ESTADOS DE VISIBILIDADE DA SENHA
const showCurrentPass = ref(false);
const showNewPass = ref(false);
const showConfirmPass = ref(false);

const form = ref({ cpf: '', name: '', email: '', phone: '' });
const passForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' });

// --- MÉTODOS ---

const loadProfile = async () => {
  loading.value = true;
  errorMsg.value = '';
  try {
    const rawToken = localStorage.getItem('token');
    if (!rawToken) { errorMsg.value = "Sessão expirada."; loading.value = false; return; }
    const token = rawToken.replace(/['"]+/g, '');

    const response = await axios.get('/core/api/user/profile', { headers: { Authorization: `Bearer ${token}` } });
    form.value = response.data;
  } catch (error) { errorMsg.value = "Erro ao carregar dados."; } finally { loading.value = false; }
};

const updateProfile = async () => {
  saving.value = true;
  try {
    const token = localStorage.getItem('token')?.replace(/['"]+/g, '');
    await axios.put('/core/api/user/update', form.value, { headers: { Authorization: `Bearer ${token}` } });
    
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    user.name = form.value.name;
    localStorage.setItem('user', JSON.stringify(user));
    
    showNotify("Dados atualizados com sucesso!", "success");
  } catch (error) { 
    showNotify("Erro ao atualizar dados.", "error");
  } finally { saving.value = false; }
};

const changePassword = async () => {
  if (passForm.value.newPassword.length < 6) return showNotify("Mínimo 6 caracteres.", "warning");
  if (passForm.value.newPassword !== passForm.value.confirmPassword) return showNotify("Senhas não coincidem.", "warning");
  
  savingPass.value = true;
  try {
    const token = localStorage.getItem('token')?.replace(/['"]+/g, '');
    await axios.post('/core/api/user/change-password', {
      currentPassword: passForm.value.currentPassword,
      newPassword: passForm.value.newPassword
    }, { headers: { Authorization: `Bearer ${token}` } });
    
    showNotify("Senha alterada com sucesso!", "success");
    // Reseta form e visibilidade
    passForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' };
    showCurrentPass.value = false;
    showNewPass.value = false;
    showConfirmPass.value = false;
    
  } catch (error: any) {
    const msg = error.response?.data?.message || "Erro ao alterar senha.";
    showNotify(msg, "error");
  } finally { savingPass.value = false; }
};

// --- LIFECYCLE (AQUI ESTÁ A LÓGICA DO MENU) ---
onMounted(() => {
  loadProfile();
  
  // ✅ Verifica a URL ao abrir a página
  if (route.query.tab === 'security') {
    activeTab.value = 'security';
  }
});

// ✅ Monitora a URL caso você clique no menu ESTANDO já na página
watch(() => route.query.tab, (newTab) => {
  if (newTab === 'security') {
    activeTab.value = 'security';
  } else {
    activeTab.value = 'personal';
  }
});
</script>

<style scoped>
/* Estilos Compactos Reutilizáveis */
.label-compact { @apply block text-[10px] font-bold text-gray-400 uppercase mb-1 ml-1; }
/* Adicionado pr-10 para o texto não ficar em cima do olho */
.input-compact { @apply w-full bg-[#0f212e] text-white text-sm border border-gray-700 rounded-lg py-2 pr-10 focus:border-green-500 focus:ring-1 focus:ring-green-500 outline-none transition-all placeholder-gray-600; }
.icon-input { @apply absolute left-3 top-2.5 w-4 h-4; }
.btn-compact { @apply bg-green-600 hover:bg-green-500 text-white text-xs font-bold py-2.5 px-6 rounded-lg shadow-lg shadow-green-900/20 transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2; }

/* Animações */
.animate-fade-in { animation: fadeIn 0.3s ease-out; }
.animate-fade-in-up { animation: fadeInUp 0.4s ease-out; }
.animate-scale-in { animation: scaleIn 0.2s cubic-bezier(0.16, 1, 0.3, 1); }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes fadeInUp { from { opacity: 0; transform: translateY(15px); } to { opacity: 1; transform: translateY(0); } }
@keyframes scaleIn { from { opacity: 0; transform: scale(0.95); } to { opacity: 1; transform: scale(1); } }

/* Transitions Vue */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>