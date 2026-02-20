<script setup lang="ts">
import { ref } from 'vue';
import { Check, X, Lock, ShieldCheck, EyeOff } from 'lucide-vue-next';

// Recebe os dados da aposta e se deve ocultar (modo privacidade)
const props = defineProps<{
  bet: any;
  isPrivacyMode?: boolean; // Se true, aplica o blur em apostas pendentes
}>();

const isExpanded = ref(false);

// --- HELPERS VISUAIS ---
const formatCurrency = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v).replace('R$', '🪙');

const formatDate = (dateString: string) => {
  if (!dateString || dateString.startsWith('0001')) return '--/--';
  const date = new Date(dateString);
  return date.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

const formatMarketName = (name: string) => {
  if (!name) return '';
  const clean = name.toLowerCase().trim();
  if (clean === '1x2' || clean === 'match winner') return 'Resultado Final';
  return name;
};

const getSelectionDisplay = (sel: any) => {
    const rawSel = (sel.selectionName || '').toLowerCase().trim();
    if (rawSel === '1') return sel.homeTeam;
    if (rawSel === '2') return sel.awayTeam;
    if (rawSel === 'x' || rawSel === 'empate') return 'Empate';
    return sel.selectionName;
};

const getSelectionStatusClasses = (status: string) => {
    const s = (status || '').toLowerCase();
    if (s === 'won') return 'text-green-400 border-green-500/20';
    if (s === 'lost') return 'text-red-400 border-red-500/20';
    return 'text-blue-400 border-blue-400/20';
};

const getRealStatus = (bet: any): string => bet.status?.toLowerCase() || '';

const getBetStatusLabel = (bet: any) => {
  const status = getRealStatus(bet);
  if (status === 'won' || status === 'lost') return 'Finalizada';
  return 'Confirmada'; // Ou 'Pendente'
};

const getStatusClasses = (bet: any) => {
  const status = getRealStatus(bet);
  if (status === 'won') return 'text-green-400 border-green-500/30 bg-green-500/10';
  if (status === 'lost') return 'text-red-400 border-red-500/30 bg-red-500/10';
  return 'text-blue-400 border-blue-500/30 bg-blue-500/10';
};

const getBorderClass = (bet: any) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'border-green-500 border-l-4';
    if (status === 'lost') return 'border-red-500 border-l-4';
    return 'border-blue-500 border-l-4'; 
}

// 👇 1. Título alterado para "Valor Perdido"
const getReturnLabel = (bet: any) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'Retorno Pago';
    if (status === 'lost') return 'Valor Perdido'; 
    return 'Retorno Potencial';
}

// 👇 2. Valor alterado para puxar o valor apostado negativo
const formatReturnValue = (bet: any) => {
    const status = getRealStatus(bet);
    if (status === 'lost') {
        // Formata apenas o valor numérico (ex: 10.000,00)
        const numStr = new Intl.NumberFormat('pt-BR', { 
            minimumFractionDigits: 2, 
            maximumFractionDigits: 2 
        }).format(bet.amount);
        
        // Monta a string deixando a moeda e o sinal negativo colados no número
        return `🪙 -${numStr}`;
    }
    return formatCurrency(bet.potentialWin);
}

// 👇 3. Classe CSS alterada para Vermelho ao invés de riscado/cinza
const getReturnValueClass = (bet: any) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'text-[#00ffb9] drop-shadow-[0_0_5px_rgba(0,255,185,0.3)]'; 
    if (status === 'lost') return 'text-red-500 drop-shadow-[0_0_5px_rgba(239,68,68,0.3)]'; 
    return 'text-white';
}

// Verifica se deve mostrar o cadeado (Modo Privacidade ATIVO + Status Pendente)
const shouldShowLock = () => {
    return props.isPrivacyMode && getRealStatus(props.bet) === 'pending';
};
</script>

<template>
  <div class="bg-[#1e293b] rounded-lg shadow-lg overflow-hidden border-l-[4px] transition-all hover:bg-[#263345] relative group"
       :class="getBorderClass(bet)">
    
    <div v-if="shouldShowLock()" 
         class="absolute inset-0 z-20 flex flex-col items-center justify-center bg-gradient-to-br from-[#1e293b] via-[#1e293b] to-[#0f172a] m-[1px]">
         
         <div class="absolute -right-8 -bottom-10 text-white/[0.03] transform rotate-12 transition-transform duration-700 group-hover:rotate-0 group-hover:scale-110 pointer-events-none">
            <Lock :size="160" stroke-width="1.5" />
         </div>

         <div class="relative z-10 flex flex-col items-center text-center space-y-3 px-4">
            
            <div class="relative mb-1">
                <div class="absolute inset-0 bg-blue-500/20 blur-xl rounded-full animate-pulse"></div>
                <div class="relative bg-[#0f172a] p-3.5 rounded-full border border-slate-700/80 shadow-2xl flex items-center justify-center ring-1 ring-white/5">
                    <EyeOff class="w-6 h-6 text-blue-400" />
                </div>
            </div>

            <div class="space-y-1">
                <h3 class="text-white font-bold tracking-[0.15em] text-[10px] uppercase flex items-center justify-center gap-2">
                    <Lock class="w-3 h-3 text-blue-500" />
                    Bilhete Protegido
                </h3>
                
                <p class="text-[10px] text-slate-400 max-w-[240px] leading-relaxed mx-auto font-medium">
                    A visualização deste bilhete será liberada automaticamente após a 
                    <span class="text-blue-300 font-bold border-b border-blue-500/20 pb-0.5">finalização dos jogos</span>.
                </p>
            </div>

            <div class="flex items-center gap-1.5 px-3 py-1 bg-slate-800/50 backdrop-blur-sm rounded-full border border-slate-700/50 mt-2">
                <ShieldCheck class="w-3 h-3 text-emerald-500" />
                <span class="text-[8px] text-slate-400 font-bold uppercase tracking-wider">Fair Play</span>
            </div>
         </div>
    </div>

    <div :class="{ 'blur-md opacity-30 pointer-events-none select-none grayscale': shouldShowLock() }">
        
        <div class="px-4 py-3 flex justify-between items-start cursor-pointer border-b border-slate-700/50" 
             @click="bet.selections.length > 1 ? isExpanded = !isExpanded : null">
          
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2">
              <span class="text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-sm shadow-sm"
                    :class="bet.selections.length > 1 ? 'bg-blue-500/20 text-blue-400' : 'bg-slate-600/20 text-slate-400'">
                {{ bet.selections.length > 1 ? 'Múltipla' : 'Simples' }}
              </span>
              <span class="text-[11px] text-slate-500 font-mono tracking-wide">#{{ bet.id }}</span>
            </div>
            <span class="text-[11px] text-slate-400 ml-0.5">Apostado em: {{ formatDate(bet.placedAt) }}</span>
          </div>

          <div class="text-right">
            <div class="flex items-center justify-end gap-2 mb-1">
              <span class="text-[10px] font-bold uppercase border px-2 py-0.5 rounded shadow-sm" 
                    :class="getStatusClasses(bet)">
                {{ getBetStatusLabel(bet) }}
              </span>
            </div>
            
            <div v-if="bet.selections.length > 1" class="text-[10px] text-slate-500 flex items-center justify-end gap-1 hover:text-white transition">
              {{ isExpanded ? 'Fechar' : 'Ver' }} {{ bet.selections.length }} Jogos
              <span class="text-xs">{{ isExpanded ? '▲' : '▼' }}</span>
            </div>
          </div>
        </div>

        <div v-show="bet.selections.length === 1 || isExpanded" class="bg-[#0f172a]/40">
          <div v-for="(sel, idx) in bet.selections" :key="idx" 
               class="p-4 border-b border-slate-700/30 last:border-0 relative group">
            
            <div class="absolute left-0 top-0 bottom-0 w-[3px]"
                 :class="{
                   'bg-green-500': sel.status === 'Won',
                   'bg-red-500': sel.status === 'Lost',
                   'bg-slate-700': sel.status === 'Pending' || sel.status === 'pending'
                 }"></div>

            <div class="flex justify-between items-center ml-2">
              <div class="flex-1 min-w-0 pr-4">
                
                <div class="text-[10px] text-slate-500 mb-1.5 font-mono flex items-center gap-1.5">
                  <span class="opacity-70">⚽</span> {{ sel.homeTeam }} x {{ sel.awayTeam }}
                </div>

                <div class="text-[13px] text-slate-200 font-medium mb-2 flex items-center flex-wrap leading-snug">
                  
                  <div v-if="sel.finalScore" class="flex items-center gap-2 mr-2">
                      <span class="font-mono text-white font-bold bg-slate-700/50 px-1.5 rounded text-xs border border-slate-600">
                      {{ sel.finalScore }}
                      </span>
                  </div>
                  
                  <span v-if="sel.status === 'Live'" class="mr-2 text-[9px] font-bold text-red-500 animate-pulse bg-red-500/10 px-1.5 py-0.5 rounded border border-red-500/20">LIVE</span>
                </div>
                
                <div class="flex items-center flex-wrap gap-2 text-xs">
                  
                  <span class="text-slate-500 font-bold uppercase text-[10px] tracking-wide bg-slate-800/50 px-1.5 py-0.5 rounded">
                      {{ formatMarketName(sel.marketName) }}
                  </span>

                  <div v-if="sel.status === 'Won'" class="flex items-center justify-center bg-green-500/10 w-4 h-4 rounded-full">
                      <Check class="w-3 h-3 text-green-500" stroke-width="3" />
                  </div>
                  <div v-else-if="sel.status === 'Lost'" class="flex items-center justify-center bg-red-500/10 w-4 h-4 rounded-full">
                      <X class="w-3 h-3 text-red-500" stroke-width="3" />
                  </div>
                  <span v-else class="text-slate-600">👉</span>
                  
                  <span class="font-bold border-b pb-0.5"
                        :class="getSelectionStatusClasses(sel.status)">
                      {{ getSelectionDisplay(sel) }}
                  </span>

                </div>

              </div>

              <div class="flex flex-col items-end justify-center h-full min-w-[60px]">
                <span class="text-sm font-bold text-white bg-slate-700/50 px-2.5 py-1 rounded border border-slate-600/50 shadow-sm">
                  {{ sel.odds.toFixed(2) }}
                </span>
                <span v-if="sel.status === 'Won'" class="text-[9px] text-green-500 mt-1.5 font-bold tracking-wider flex items-center gap-1"><span class="w-1 h-1 bg-green-500 rounded-full"></span> VENCEU</span>
                <span v-if="sel.status === 'Lost'" class="text-[9px] text-red-500 mt-1.5 font-bold tracking-wider flex items-center gap-1"><span class="w-1 h-1 bg-red-500 rounded-full"></span> PERDEU</span>
              </div>
            </div>
          </div>
        </div>

        <div class="px-4 py-3 bg-[#1e293b] flex items-center justify-between border-t border-slate-700/50">
          <div class="flex flex-col">
            <span class="text-[9px] text-slate-500 uppercase tracking-wide font-bold">Valor Apostado</span>
            <span class="text-sm font-bold text-white tracking-wide">{{ formatCurrency(bet.amount) }}</span>
          </div>

          <div class="text-right">
            <span class="text-[9px] uppercase tracking-wide block mb-0.5 font-bold" 
                  :class="bet.status?.toLowerCase() === 'lost' ? 'text-red-500/80' : 'text-slate-400'">
              {{ getReturnLabel(bet) }}
            </span>
            <span class="text-base font-black tracking-wide" 
                  :class="getReturnValueClass(bet)">
              {{ formatReturnValue(bet) }}
            </span>
          </div>
        </div>
    </div>

  </div>
</template>