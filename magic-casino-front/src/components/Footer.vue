<script setup lang="ts">
import { 
  Mail, ShieldCheck, 
  Instagram, Twitter, Send,  
} from 'lucide-vue-next';

const currentYear = new Date().getFullYear();
</script>

<template>
  <footer class="bg-[#0b111e] border-t border-white/5 w-full shrink-0 relative z-20 mt-auto font-sans">
    
    <div class="border-b border-white/5 bg-[#0f172a]">
        <div class="max-w-[1200px] mx-auto px-4 py-3 md:px-6 md:py-4 flex flex-col md:flex-row items-center justify-between gap-3 md:gap-4">
            
            <div class="flex items-center gap-4">
                <div class="text-xl font-black italic tracking-tighter text-white flex items-center gap-1">
                    <span class="text-blue-500">quebrandoabanca</span>.bet
                </div>
                <div class="h-4 w-px bg-white/10 hidden md:block"></div>
                <p class="text-[11px] text-slate-500 hidden md:block max-w-[400px]">
                    A plataforma de torneios esportivos mais emocionante do Brasil.
                </p>
            </div>

            <div class="flex items-center gap-3">
                <a href="#" class="w-8 h-8 rounded bg-white/5 hover:bg-blue-600 hover:text-white flex items-center justify-center transition-all group">
                    <Instagram class="w-4 h-4 text-slate-400 group-hover:text-white" />
                </a>
                <a href="#" class="w-8 h-8 rounded bg-white/5 hover:bg-sky-500 hover:text-white flex items-center justify-center transition-all group">
                    <Twitter class="w-4 h-4 text-slate-400 group-hover:text-white" />
                </a>
                <a href="#" class="w-8 h-8 rounded bg-white/5 hover:bg-blue-400 hover:text-white flex items-center justify-center transition-all group">
                    <Send class="w-4 h-4 text-slate-400 group-hover:text-white" />
                </a>
            </div>
        </div>
    </div>

    <div class="max-w-[1200px] mx-auto px-4 py-4 md:px-6 md:py-8"> 
      <div class="grid grid-cols-2 md:grid-cols-4 gap-y-6 gap-x-4 md:gap-x-6 md:gap-y-8">
        
        <div class="flex flex-col gap-2 md:gap-3">
          <h3 class="text-[10px] font-bold text-white uppercase tracking-widest opacity-80">Plataforma</h3>
          <ul class="space-y-1 md:space-y-1.5 text-xs text-slate-400"> 
            <li><router-link to="/tournaments" class="hover:text-blue-400 transition-colors">Torneios</router-link></li>
            <li><router-link to="/trabalhe-conosco" class="hover:text-blue-400 transition-colors">Dúvidas Frequentes</router-link></li>
            <li><router-link to="/ranking" class="hover:text-blue-400 transition-colors">Trabalhe Conosco</router-link></li>
          </ul>
        </div>

        <div class="flex flex-col gap-2 md:gap-3">
          <h3 class="text-[10px] font-bold text-white uppercase tracking-widest opacity-80">Legal</h3>
          <ul class="space-y-1 md:space-y-1.5 text-xs text-slate-400">
            <li><router-link to="/termos" class="hover:text-blue-400 transition-colors">Termos de Uso</router-link></li>
            <li><router-link to="/privacidade" class="hover:text-blue-400 transition-colors">Política de Privacidade</router-link></li>
            <li><router-link to="/jogo-responsavel" class="hover:text-emerald-400 transition-colors">Jogo Responsável</router-link></li>
          </ul>
        </div>

        <div class="flex flex-col gap-2 md:gap-3">
          <h3 class="text-[10px] font-bold text-white uppercase tracking-widest opacity-80">Pagamentos</h3>
          <div class="flex flex-wrap gap-2">
             <div class="px-2.5 py-1 rounded bg-[#1e293b] border border-white/5 flex items-center gap-1.5">
                <div class="w-2 h-2 bg-emerald-500 rounded-sm rotate-45"></div>
                <span class="text-[10px] font-bold text-slate-300">PIX</span>
             </div>
             <div class="px-2.5 py-1 rounded bg-[#1e293b] border border-white/5 flex items-center gap-1.5">
                <div class="w-2 h-2 bg-orange-500 rounded-full"></div>
                <span class="text-[10px] font-bold text-slate-300">Crypto</span>
             </div>
          </div>
          <p class="text-[10px] text-slate-500 leading-tight max-w-[200px]">
            Transações seguras e instantâneas.
          </p>
        </div>

        <div class="flex flex-col gap-2 md:gap-3">
          <h3 class="text-[10px] font-bold text-white uppercase tracking-widest opacity-80">Suporte</h3>
          
          <a href="mailto:suporte@quebrandoabanca.bet" class="group flex items-center gap-2 p-2 rounded border border-white/5 bg-white/5 hover:border-blue-500/30 hover:bg-blue-500/10 transition-all w-full">
            <div class="w-6 h-6 rounded bg-blue-500/20 flex items-center justify-center group-hover:bg-blue-500 group-hover:text-white transition-colors text-blue-400 shrink-0">
                <Mail class="w-3 h-3" />
            </div>
            
            <div class="flex flex-col min-w-0">
                <span class="text-[9px] uppercase font-bold text-slate-500 leading-none mb-0.5">Email Oficial</span>
                <span class="text-[10px] text-slate-200 break-all leading-tight">contato@quebrandoabanca.bet</span>
            </div>
          </a>
        </div>

      </div>
    </div>

    <div class="border-t border-white/5 bg-[#050912]">
      <div class="max-w-[1200px] mx-auto px-4 py-2 md:px-6 md:py-3 flex flex-col-reverse md:flex-row items-center justify-between gap-3 md:gap-4">
        
        <div class="text-[10px] text-slate-600 text-center md:text-left flex flex-col md:flex-row md:gap-4">
          <span>© {{ currentYear }} Quebrando a Banca.</span>
          <span class="hidden md:inline text-slate-700">|</span>
          <span>Operado por QAB Games Ltd.</span>
        </div>

        <div class="flex items-center gap-4 opacity-50 grayscale hover:grayscale-0 transition-all duration-500">
            <div class="flex items-center gap-1.5" title="Site Seguro">
                <ShieldCheck class="w-4 h-4 text-slate-400" />
                <span class="text-[10px] font-bold text-slate-400">SSL</span>
            </div>
            <div class="h-3 w-px bg-white/10"></div>
            <div class="flex items-center gap-1.5" title="Proibido para menores">
                <div class="w-5 h-5 rounded-full border border-red-800 flex items-center justify-center text-[8px] font-black text-red-700 bg-red-900/10">
                    18+
                </div>
            </div>
        </div>

      </div>
    </div>
  </footer>
</template>

<style scoped>
a.router-link-active {
    color: #3b82f6; 
}
</style>