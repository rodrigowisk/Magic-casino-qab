<template>
  <div class="min-h-screen bg-[#0f212e] text-white p-4 font-sans flex flex-col items-center justify-center md:block">
    
    <div class="w-full max-w-5xl mx-auto">
      
      <div class="mb-8 flex flex-col md:flex-row md:items-end justify-between gap-4">
        
        <div class="flex items-center gap-4">
            <router-link to="/" class="p-3 rounded-xl bg-[#1a2c38] border border-gray-700/50 hover:border-green-500/50 hover:bg-[#1f364d] hover:shadow-[0_0_15px_rgba(34,197,94,0.1)] transition-all group relative overflow-hidden">
                <div class="absolute inset-0 bg-green-500/10 opacity-0 group-hover:opacity-100 transition-opacity"></div>
                <ArrowLeft class="w-6 h-6 text-gray-400 group-hover:text-green-400 group-hover:-translate-x-1 transition-transform relative z-10" />
            </router-link>

            <div>
                <div class="relative pl-2">
                    <div class="absolute -top-6 -left-6 w-24 h-24 bg-green-500/10 rounded-full blur-2xl"></div>
                    <h1 class="text-2xl font-bold text-white flex items-center gap-2 relative z-10">
                        <UserCircle class="w-7 h-7 text-green-500" />
                        Minha Conta
                    </h1>
                    <p class="text-xs text-gray-400 relative z-10">Gerencie seus dados e segurança.</p>
                </div>
            </div>
        </div>
        
        <div class="flex items-center gap-3 self-end md:self-auto">
            <div class="text-right hidden md:block">
                <p class="text-[10px] text-gray-500 uppercase font-bold tracking-wider">Status Atual</p>
                <p class="text-sm font-bold" :class="levelStyle.textColor">{{ levelStyle.label }}</p>
            </div>
            <component :is="currentMedalIcon" class="w-10 h-10 drop-shadow-lg" />
        </div>
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
            <div class="absolute top-0 w-full h-24 bg-gradient-to-b opacity-20 transition-colors duration-500" :class="levelStyle.bgGradient"></div>
            
            <div class="p-6 flex flex-col items-center gap-3 relative z-10">
              
              <div 
                class="relative cursor-pointer group transition-transform duration-300 hover:scale-105"
                @click="openAvatarModal"
              >
                <div 
                  class="w-24 h-24 rounded-full bg-[#0f212e] border-[3px] flex items-center justify-center shadow-2xl p-1 transition-all duration-500 overflow-hidden"
                  :class="levelStyle.borderColor"
                >
                  <div class="w-full h-full rounded-full bg-[#15222d] flex items-center justify-center overflow-hidden relative">
                     <img 
                        :src="form.avatar || '/images/avatars/1.svg'" 
                        class="w-full h-full object-cover" 
                        alt="Avatar"
                        @error="handleImageError"
                     />
                     <div class="absolute inset-0 bg-black/60 flex flex-col gap-1 items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-300">
                        <Camera class="w-6 h-6 text-white drop-shadow-md" />
                     </div>
                  </div>
                </div>
                
                <div class="absolute -bottom-2 -right-2 w-10 h-10 filter drop-shadow-lg transform rotate-12 group-hover:rotate-0 transition-transform duration-300">
                    <component :is="currentMedalIcon" />
                </div>
              </div>

              <div class="text-center w-full">
                <h2 class="text-xl font-bold text-white truncate">{{ form.name || 'Usuário' }}</h2>
                <p class="text-xs text-green-400 font-mono mb-3">@{{ form.userName }}</p> 
                
                <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full border bg-[#0f212e]/50 backdrop-blur-sm shadow-lg border-white/5">
                    <span class="text-xs font-black uppercase tracking-wider" :class="levelStyle.textColor">
                        NÍVEL {{ levelStyle.label }}
                    </span>
                </div>
              </div>
            </div>
          </div>

          <div class="bg-[#1a2c38] rounded-xl p-4 border border-gray-700/50 shadow-lg">
            <h3 class="text-white text-sm font-bold flex items-center gap-2 mb-3">
              <ShieldCheck class="w-3 h-3 text-green-500" /> Status da Conta
            </h3>
            <ul class="space-y-3 text-xs">
              <li class="flex items-center justify-between p-2.5 rounded bg-[#0f212e]/50 border border-gray-700/30">
                <div class="flex items-center gap-2">
                    <CheckCircle v-if="form.emailVerified" class="w-4 h-4 text-green-500" />
                    <AlertTriangle v-else class="w-4 h-4 text-yellow-500" />
                    <span :class="form.emailVerified ? 'text-gray-300' : 'text-yellow-200/80'">E-mail</span>
                </div>
                <span class="text-[10px] font-bold uppercase px-2 py-0.5 rounded" :class="form.emailVerified ? 'bg-green-500/10 text-green-500' : 'bg-yellow-500/10 text-yellow-500'">
                    {{ form.emailVerified ? 'Verificado' : 'Pendente' }}
                </span>
              </li>
              <li class="flex items-center justify-between p-2.5 rounded bg-[#0f212e]/50 border border-gray-700/30">
                <div class="flex items-center gap-2">
                    <CheckCircle class="w-4 h-4 text-green-500" />
                    <span class="text-gray-300">CPF</span>
                </div>
                <span class="text-[10px] font-bold text-green-500 uppercase bg-green-500/10 px-2 py-0.5 rounded">Vinculado</span>
              </li>
            </ul>
            <div v-if="!form.emailVerified" class="mt-3 p-2 bg-yellow-500/10 rounded border border-yellow-500/20">
               <p class="text-[10px] text-yellow-200/80 text-center leading-tight">
                  <span class="font-bold">Atenção:</span> Verifique seu e-mail para subir para o nível <span class="text-gray-300 font-bold">PRATA</span>.
               </p>
            </div>
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
                  <h3 class="text-white font-bold text-lg">{{ notification.type === 'success' ? 'Sucesso!' : notification.type === 'error' ? 'Erro' : 'Atenção' }}</h3>
                  <p class="text-gray-300 text-sm leading-relaxed">{{ notification.message }}</p>
                  <button @click="notification.show = false" class="mt-2 px-6 py-2 bg-gray-700 hover:bg-gray-600 rounded-lg text-xs font-bold transition-colors text-white">Fechar</button>
                </div>
              </div>
            </transition>

            <div class="flex border-b border-gray-700 bg-[#0f212e]/30">
              <button @click="changeTab('personal')" class="flex-1 py-3 text-xs md:text-sm font-bold uppercase tracking-wide transition-colors flex items-center justify-center gap-2 border-b-2 outline-none" :class="activeTab === 'personal' ? 'border-green-500 text-white bg-[#1a2c38]' : 'border-transparent text-gray-500 hover:text-gray-300'">
                <User class="w-3 h-3 md:w-4 md:h-4" /> Dados
              </button>
              <button @click="changeTab('security')" class="flex-1 py-3 text-xs md:text-sm font-bold uppercase tracking-wide transition-colors flex items-center justify-center gap-2 border-b-2 outline-none" :class="activeTab === 'security' ? 'border-green-500 text-white bg-[#1a2c38]' : 'border-transparent text-gray-500 hover:text-gray-300'">
                <Key class="w-3 h-3 md:w-4 md:h-4" /> Senha
              </button>
            </div>

            <div class="p-5 flex-1">

              <form v-if="activeTab === 'personal'" @submit.prevent="updateProfile" class="space-y-3 animate-fade-in h-full flex flex-col justify-between">
                <div class="space-y-3">
                  <div class="grid grid-cols-2 gap-3">
                    <div class="group">
                        <label class="label-compact">Usuário</label>
                        <div class="relative opacity-70">
                        <AtSign class="icon-input text-green-500" />
                        <input type="text" v-model="form.userName" disabled class="input-compact pl-9 cursor-not-allowed font-bold" />
                        <Lock class="absolute right-3 top-2.5 w-3 h-3 text-gray-600" />
                        </div>
                    </div>
                    <div class="group">
                        <label class="label-compact">CPF</label>
                        <div class="relative opacity-70">
                        <FileText class="icon-input" />
                        <input type="text" v-model="form.cpf" disabled class="input-compact pl-9 cursor-not-allowed font-mono" />
                        <Lock class="absolute right-3 top-2.5 w-3 h-3 text-gray-600" />
                        </div>
                    </div>
                  </div>
                  <div>
                    <label class="label-compact">Nome Completo</label>
                    <div class="relative">
                      <User class="icon-input text-green-500" />
                      <input type="text" v-model="form.name" class="input-compact pl-9" placeholder="Seu nome" />
                    </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <div>
                      <label class="label-compact">E-mail</label>
                      <div class="relative">
                        <Mail class="icon-input text-green-500" />
                        <input type="email" v-model="form.email" class="input-compact pl-9" placeholder="seu@email.com" />
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
                      <input :type="showCurrentPass ? 'text' : 'password'" v-model="passForm.currentPassword" required class="input-compact pl-9 pr-10" placeholder="••••••" />
                      <button type="button" @click="showCurrentPass = !showCurrentPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                        <Eye v-if="!showCurrentPass" class="w-4 h-4" /> <EyeOff v-else class="w-4 h-4 text-green-500" />
                      </button>
                    </div>
                  </div>
                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="label-compact">Nova Senha</label>
                      <div class="relative">
                        <Lock class="icon-input text-green-500" />
                        <input :type="showNewPass ? 'text' : 'password'" v-model="passForm.newPassword" required class="input-compact pl-9 pr-10" placeholder="Nova senha" />
                        <button type="button" @click="showNewPass = !showNewPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                            <Eye v-if="!showNewPass" class="w-4 h-4" /> <EyeOff v-else class="w-4 h-4 text-green-500" />
                        </button>
                      </div>
                    </div>
                    <div>
                      <label class="label-compact">Confirmar</label>
                      <div class="relative">
                        <Lock class="icon-input text-green-500" />
                        <input :type="showConfirmPass ? 'text' : 'password'" v-model="passForm.confirmPassword" required class="input-compact pl-9 pr-10" placeholder="Repita senha" />
                        <button type="button" @click="showConfirmPass = !showConfirmPass" class="absolute right-3 top-2 text-gray-500 hover:text-white transition-colors focus:outline-none">
                            <Eye v-if="!showConfirmPass" class="w-4 h-4" /> <EyeOff v-else class="w-4 h-4 text-green-500" />
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

    <transition name="fade">
        <div v-if="showAvatarModal" class="fixed inset-0 z-[9999] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm" @click.self="showAvatarModal = false">
            <div class="bg-[#1a2c38] w-full max-w-lg rounded-xl border border-gray-700 shadow-2xl overflow-hidden animate-scale-in">
                
                <div class="p-4 border-b border-gray-700 flex justify-between items-center bg-[#0f212e]">
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                        <User class="w-4 h-4 text-green-500" /> Escolha seu Avatar
                    </h3>
                    <button @click="showAvatarModal = false" class="text-gray-500 hover:text-white transition-colors">
                        <X class="w-4 h-4" />
                    </button>
                </div>

                <div class="flex p-3 gap-3 bg-[#0f212e]">
                    <button @click="avatarFilter = 'man'" class="flex-1 py-2 rounded text-[10px] font-bold uppercase transition-all border" :class="avatarFilter === 'man' ? 'bg-blue-600/20 border-blue-500 text-blue-400' : 'bg-[#1a2c38] border-transparent text-gray-400 hover:bg-[#253a49]'">Masculino</button>
                    <button @click="avatarFilter = 'woman'" class="flex-1 py-2 rounded text-[10px] font-bold uppercase transition-all border" :class="avatarFilter === 'woman' ? 'bg-pink-600/20 border-pink-500 text-pink-400' : 'bg-[#1a2c38] border-transparent text-gray-400 hover:bg-[#253a49]'">Feminino</button>
                </div>

                <div class="p-4 max-h-[350px] overflow-y-auto custom-scrollbar bg-[#15202b]">
                    <div class="grid grid-cols-4 sm:grid-cols-5 gap-3">
                        <div 
                            v-for="(img, index) in filteredAvatars" 
                            :key="index"
                            @click="selectAvatar(img)"
                            class="aspect-square rounded-full overflow-hidden border-2 cursor-pointer transition-all hover:scale-110 hover:shadow-lg relative group bg-[#0f212e]"
                            :class="form.avatar === img ? 'border-green-500 ring-2 ring-green-500/30' : 'border-gray-700 hover:border-gray-500'"
                        >
                            <img :src="img" class="w-full h-full object-cover" loading="lazy" />
                            <div v-if="form.avatar === img" class="absolute inset-0 bg-green-500/40 flex items-center justify-center backdrop-blur-sm">
                                <CheckCircle class="w-6 h-6 text-white drop-shadow-md" />
                            </div>
                        </div>
                    </div>
                </div>

                <div class="p-3 border-t border-gray-700 bg-[#0f212e] text-center flex justify-between items-center">
                    <p class="text-[10px] text-gray-500">Selecione para salvar</p>
                    <button @click="showAvatarModal = false" class="px-3 py-1.5 rounded bg-gray-700 hover:bg-gray-600 text-[10px] font-bold uppercase transition-colors text-white">Cancelar</button>
                </div>
            </div>
        </div>
    </transition>

  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import axios from 'axios';
import { 
  User, Mail, Phone, Lock, FileText, Save, Loader2, 
  AlertTriangle, CheckCircle, ShieldCheck, UserCircle, Key, XCircle,
  Eye, EyeOff, AtSign, Camera, X, ArrowLeft
} from 'lucide-vue-next';

// ✅ 1. Importa a Store
import { useAuthStore } from '../stores/useAuthStore';

// Ícones Medalhas
const MedalBronze = { template: `<svg viewBox="0 0 100 100" class="w-full h-full" fill="none" xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="bronzeGrad" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" style="stop-color:#F59E0B" /> <stop offset="100%" style="stop-color:#78350F" /></linearGradient></defs><circle cx="50" cy="50" r="45" fill="url(#bronzeGrad)" stroke="#78350F" stroke-width="2" /><path d="M50 20 L60 40 H80 L65 55 L70 75 L50 60 L30 75 L35 55 L20 40 H40 Z" fill="white" fill-opacity="0.3" /><text x="50" y="70" font-size="22" font-weight="900" fill="#3D1803" text-anchor="middle" font-family="Arial">III</text></svg>` };
const MedalSilver = { template: `<svg viewBox="0 0 100 100" class="w-full h-full" fill="none" xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="silverGrad" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" style="stop-color:#F3F4F6" /> <stop offset="100%" style="stop-color:#4B5563" /></linearGradient></defs><circle cx="50" cy="50" r="45" fill="url(#silverGrad)" stroke="#374151" stroke-width="2" /><path d="M50 15 L55 35 H75 L60 50 L65 70 L50 55 L35 70 L40 50 L25 35 H45 Z" fill="white" fill-opacity="0.4" /><text x="50" y="70" font-size="22" font-weight="900" fill="#1F2937" text-anchor="middle" font-family="Arial">II</text></svg>` };
const MedalGold = { template: `<svg viewBox="0 0 100 100" class="w-full h-full" fill="none" xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="goldGrad" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" style="stop-color:#FDE68A" /> <stop offset="50%" style="stop-color:#F59E0B" /> <stop offset="100%" style="stop-color:#B45309" /></linearGradient></defs><circle cx="50" cy="50" r="45" fill="url(#goldGrad)" stroke="#B45309" stroke-width="2" /><path d="M50 10 L62 38 H92 L68 56 L78 86 L50 68 L22 86 L32 56 L8 38 H38 Z" fill="white" fill-opacity="0.5" /><text x="50" y="70" font-size="24" font-weight="900" fill="#78350F" text-anchor="middle" font-family="Arial">I</text></svg>` };

const route = useRoute();
const router = useRouter();
// ✅ 2. Inicializa a Store
const authStore = useAuthStore();

const notification = ref({ show: false, message: '', type: 'success' as 'success' | 'error' | 'warning' });
const showNotify = (msg: string, type: 'success' | 'error' | 'warning' = 'success') => {
  notification.value = { show: true, message: msg, type };
  setTimeout(() => { notification.value.show = false; }, 3000);
};

const activeTab = ref<'personal' | 'security'>('personal');
const changeTab = (tab: 'personal' | 'security') => {
  activeTab.value = tab;
  router.replace({ query: { ...route.query, tab } });
};

const loading = ref(true);
const errorMsg = ref('');
const saving = ref(false);
const savingPass = ref(false);
const showCurrentPass = ref(false);
const showNewPass = ref(false);
const showConfirmPass = ref(false);

const showAvatarModal = ref(false);
const avatarFilter = ref<'man' | 'woman'>('man');

const form = ref({ 
    cpf: '', userName: '', name: '', email: '', phone: '', 
    level: 'bronze', emailVerified: false, 
    avatar: '' 
});
const passForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' });

const avatarsList = {
    man: Array.from({ length: 22 }, (_, i) => `/images/avatars/man/${i + 1}.svg`),
    woman: Array.from({ length: 6 }, (_, i) => `/images/avatars/woman/${i + 1}.svg`)
};

const filteredAvatars = computed(() => {
    return avatarFilter.value === 'man' ? avatarsList.man : avatarsList.woman;
});

const openAvatarModal = () => {
    showAvatarModal.value = true;
};

const selectAvatar = (path: string) => {
    form.value.avatar = path;
    showAvatarModal.value = false;
    updateProfile(); 
};

const handleImageError = (e: Event) => {
    (e.target as HTMLImageElement).src = '/images/avatars/1.svg';
};

const levelStyle = computed(() => {
    const lvl = (form.value.level || 'bronze').toLowerCase();
    if (lvl.includes('ouro')) return { label: 'OURO', textColor: 'text-yellow-400', bgGradient: 'from-yellow-600/20 to-transparent', borderColor: 'border-yellow-500 shadow-yellow-500/20' };
    if (lvl.includes('prata')) return { label: 'PRATA', textColor: 'text-gray-300', bgGradient: 'from-gray-400/20 to-transparent', borderColor: 'border-gray-300 shadow-gray-300/20' };
    return { label: 'BRONZE', textColor: 'text-orange-600', bgGradient: 'from-orange-800/20 to-transparent', borderColor: 'border-orange-700 shadow-orange-700/20' };
});

const currentMedalIcon = computed(() => {
    const lvl = (form.value.level || 'bronze').toLowerCase();
    if (lvl.includes('ouro')) return MedalGold;
    if (lvl.includes('prata')) return MedalSilver;
    return MedalBronze;
});

// LOAD
const loadProfile = async () => {
  loading.value = true;
  errorMsg.value = '';
  try {
    const rawToken = localStorage.getItem('token');
    if (!rawToken) { errorMsg.value = "Sessão expirada. Faça login novamente."; loading.value = false; return; }
    const token = rawToken.replace(/['"]+/g, '').trim();
    const response = await axios.get('/core/api/user/profile', { headers: { Authorization: `Bearer ${token}` } });
    const data = response.data;

    form.value = {
        cpf: data.cpf || '', 
        userName: data.user_name || '', 
        name: data.name || '',
        email: data.email || '',
        phone: data.phone || '',
        level: data.level || 'bronze',
        emailVerified: data.email_verified || false,
        avatar: data.avatar || '/images/avatars/1.svg' 
    };
  } catch (error: any) {
    if (error.response && error.response.status === 401) {
        errorMsg.value = "Sessão expirada.";
        localStorage.removeItem('token');
    } else { errorMsg.value = "Erro ao carregar dados."; }
  } finally { loading.value = false; }
};

// UPDATE
const updateProfile = async () => {
  saving.value = true;
  try {
    const token = localStorage.getItem('token')?.replace(/['"]+/g, '');
    const payload = { 
        name: form.value.name, 
        email: form.value.email, 
        phone: form.value.phone,
        avatar: form.value.avatar 
    };
    await axios.put('/core/api/user/update', payload, { headers: { Authorization: `Bearer ${token}` } });
    
    // ✅ 3. CHAMA A ACTION DA STORE PARA ATUALIZAR GLOBALMENTE
    authStore.updateUser({
        name: form.value.name,
        avatar: form.value.avatar
    });
    
    showNotify("Dados atualizados com sucesso!", "success");
  } catch { showNotify("Erro ao atualizar dados.", "error"); } finally { saving.value = false; }
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
    passForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' };
  } catch { showNotify("Erro ao alterar senha.", "error"); } finally { savingPass.value = false; }
};

onMounted(() => {
  loadProfile();
  if (route.query.tab === 'security') activeTab.value = 'security';
});
watch(() => route.query.tab, (newTab) => activeTab.value = newTab === 'security' ? 'security' : 'personal');
</script>

<style scoped>
.label-compact { @apply block text-[10px] font-bold text-gray-400 uppercase mb-1 ml-1; }
.input-compact { @apply w-full bg-[#0f212e] text-white text-sm border border-gray-700 rounded-lg py-2 pr-10 focus:border-green-500 focus:ring-1 focus:ring-green-500 outline-none transition-all placeholder-gray-600; }
.icon-input { @apply absolute left-3 top-2.5 w-4 h-4; }
.btn-compact { @apply bg-green-600 hover:bg-green-500 text-white text-xs font-bold py-2.5 px-6 rounded-lg shadow-lg shadow-green-900/20 transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2; }

.custom-scrollbar::-webkit-scrollbar { width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f212e; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #2c3e50; border-radius: 3px; }

.animate-fade-in { animation: fadeIn 0.3s ease-out; }
.animate-fade-in-up { animation: fadeInUp 0.4s ease-out; }
.animate-scale-in { animation: scaleIn 0.2s cubic-bezier(0.16, 1, 0.3, 1); }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes fadeInUp { from { opacity: 0; transform: translateY(15px); } to { opacity: 1; transform: translateY(0); } }
@keyframes scaleIn { from { opacity: 0; transform: scale(0.95); } to { opacity: 1; transform: scale(1); } }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>