<template>
  <div class="admin-container animate-fade-in">
    <div class="header-actions">
      <div class="title-section">
        <h2 class="main-title">🏆 Novo Torneio</h2>
        <p class="subtitle">Defina regras, prêmios e o modelo de jogos.</p>
      </div>
      <button @click="$router.go(-1)" class="btn-back">Voltar</button>
    </div>

    <div class="form-card shadow-lg max-w-3xl mx-auto">
      <form @submit.prevent="submitTournament" class="slim-form">
        
        <div class="template-section mb-3">
            <label class="section-label">1. Modelo de Jogos</label>
            <div class="template-controls">
                <select v-model="selectedTemplateId" class="template-select" required>
                    <option :value="null" disabled>-- Selecione um Modelo --</option>
                    <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
                </select>
                <button type="button" @click="openModelCreator" class="btn-new-model flex items-center gap-1">
                   <span>+</span> Novo
                </button>
            </div>
        </div>

        <hr class="divider"/>

        <label class="section-label">2. Configuração do Evento</label>
        
        <div class="form-group">
            <label>Nome do Torneio</label>
            <input v-model="form.name" type="text" placeholder="Ex: Rei da Bet - Premier League" required />
        </div>

        <div class="grid grid-cols-3 gap-3">
             <div class="form-group">
                 <label>Entrada (R$)</label>
                 <div class="relative-input-container">
                     <span class="prefix">R$</span>
                     <input v-model.number="form.entryFee" type="number" step="0.01" min="0" class="input-with-prefix" required />
                 </div>
             </div>
             
             <div class="form-group">
                 <label>Taxa Casa (%)</label>
                 <div class="relative-input-container">
                     <input v-model.number="form.houseFeePercent" type="number" step="1" min="0" max="100" class="input-with-suffix" required />
                     <span class="suffix">%</span>
                 </div>
             </div>

             <div class="form-group">
                 <label>Fichas Iniciais</label>
                 <input v-model.number="form.initialFantasyBalance" type="number" step="1" min="100" placeholder="Ex: 1000" required />
             </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
             <div class="form-group">
                 <label>Início</label>
                 <input v-model="form.startDate" type="datetime-local" required />
             </div>
             <div class="form-group">
                 <label>Término</label>
                 <input v-model="form.endDate" type="datetime-local" required />
             </div>
        </div>

        <div class="form-group">
            <label>Descrição (Regras)</label>
            <textarea v-model="form.description" rows="2" class="resize-none" placeholder="Detalhes opcionais do torneio..."></textarea>
        </div>

        <div class="footer-actions mt-4">
           <button type="submit" class="btn-save" :disabled="isLoading || !selectedTemplateId">
            <span v-if="isLoading" class="loader-spin">⏳</span>
            {{ isLoading ? 'Processando...' : 'Publicar Torneio' }}
          </button>
        </div>
      </form>
    </div>

    <div v-if="showModal" class="modal-overlay">
        <div class="modal-content animate-scale-in">
            <div class="modal-header">
                <div class="flex items-center gap-2">
                    <span class="text-lg">🛠️</span>
                    <h3 class="text-sm font-bold uppercase tracking-wide text-gray-200">Novo Modelo</h3>
                </div>
                <button @click="showModal = false" class="btn-close hover:text-white transition-colors">✕</button>
            </div>
            
            <div class="modal-body custom-scrollbar">
                <div class="mb-3 flex items-center gap-3 bg-[#1c1c1e] p-2 rounded border border-[#323238]">
                    <label class="text-[10px] font-bold text-yellow-500 uppercase whitespace-nowrap">Nome do Modelo:</label>
                    <input v-model="newTemplateName" type="text" 
                           class="flex-1 bg-transparent text-xs text-white outline-none placeholder-gray-600 font-medium" 
                           placeholder="Ex: Futebol Europeu Principal" autofocus />
                </div>

                <div class="mb-3">
                    <div class="flex gap-1.5 overflow-x-auto pb-1 custom-scrollbar">
                        <div 
                            v-for="sport in sportsData" 
                            :key="sport.key"
                            @click="selectSport(sport.key)"
                            class="cursor-pointer relative min-w-[90px] h-[55px] px-2 py-1.5 rounded border transition-all duration-150 select-none flex flex-col justify-between group"
                            :class="selectedSportKey === sport.key ? 'border-blue-500 bg-[#29292e] shadow-md shadow-blue-900/10' : 'border-[#323238] bg-[#202024] opacity-60 hover:opacity-100 hover:border-gray-500'"
                        >
                            <div class="flex justify-between items-start">
                                <span class="text-sm">{{ sport.icon }}</span> 
                                <div @click.stop>
                                    <input type="checkbox" v-model="sport.isActive" class="accent-blue-500 w-3 h-3 cursor-pointer rounded-sm">
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <p class="font-semibold text-gray-300 text-[10px] truncate max-w-[55px]">{{ sport.name }}</p>
                                <div class="w-1.5 h-1.5 rounded-full" :class="sport.isActive ? 'bg-green-500 shadow-[0_0_5px_rgba(34,197,94,0.6)]' : 'bg-red-500/30'"></div>
                            </div>
                        </div>
                    </div>
                </div>

                <div v-if="currentSport" class="bg-[#18181b] rounded border border-[#323238] flex-1 flex flex-col overflow-hidden">
                    <div class="flex justify-between items-center px-3 py-2 bg-[#202024] border-b border-[#323238] h-10 min-h-[40px]">
                        
                        <h3 class="font-bold text-gray-300 flex items-center gap-2 text-xs">
                            <span class="text-sm">{{ currentSport.icon }}</span>
                            {{ currentSport.name }}
                            <span class="text-[10px] font-normal text-gray-500 ml-1">({{ filteredLeagues.length }})</span>
                        </h3>
                        
                        <div class="relative w-48">
                            <span class="absolute left-2 top-1.5 text-[10px] text-gray-500">🔍</span>
                            <input 
                                v-model="searchQuery" 
                                type="text" 
                                placeholder="Buscar liga ou time..." 
                                class="w-full bg-[#09090b] border border-[#3f3f46] rounded text-[10px] text-gray-300 pl-6 pr-2 py-1 outline-none focus:border-blue-500 transition-colors placeholder-gray-600"
                            />
                        </div>

                    </div>

                    <div class="flex-1 overflow-y-auto custom-scrollbar p-1">
                        <div v-if="filteredLeagues.length === 0" class="text-center py-4 text-[10px] text-gray-600 italic">
                            Nenhuma liga ou time encontrado para "{{ searchQuery }}".
                        </div>

                        <div class="space-y-0.5"> 
                            <div 
                              v-for="league in filteredLeagues" 
                              :key="league.id"
                              class="border rounded overflow-hidden transition-all duration-75"
                              :class="league.isActive ? 'border-[#323238] bg-[#202024]' : 'border-red-900/10 bg-red-900/5'"
                            >
                                <div class="flex items-center justify-between px-2 h-8 hover:bg-[#27272a] transition-colors cursor-pointer group" @click="toggleLeagueExpansion(league)">
                                    <div class="flex items-center gap-2 flex-1 overflow-hidden">
                                        <span class="text-gray-500 text-[9px] transition-transform duration-200 w-3 flex justify-center" :class="{'rotate-90': league.isExpanded}">▶</span>
                                        
                                        <span class="font-medium text-gray-300 text-[11px] truncate group-hover:text-white transition-colors"
                                              :class="{'text-yellow-400': isMatch(league.name)}">
                                            {{ league.name }}
                                        </span>
                                        
                                        <span class="text-[9px] bg-[#121214] text-blue-400/80 px-1 rounded border border-[#323238] min-w-[18px] text-center">
                                            {{ (league.teams || []).length }}
                                        </span>
                                    </div>
                                    
                                    <div class="flex items-center gap-2" @click.stop>
                                        <span class="text-[9px] font-bold tracking-wider" :class="league.isActive ? 'text-green-500/80' : 'text-red-500/60'">
                                            {{ league.isActive ? 'ON' : 'OFF' }}
                                        </span>
                                        <div class="relative inline-flex items-center cursor-pointer" @click="league.isActive = !league.isActive; syncLeagueTeams(league)">
                                            <div class="w-7 h-3.5 bg-gray-700 rounded-full peer-checked:bg-green-600 transition-colors" :class="{'bg-green-900/50': league.isActive}"></div>
                                            <div class="absolute left-0.5 top-0.5 bg-white w-2.5 h-2.5 rounded-full transition-transform duration-200 shadow-sm"
                                                 :class="{'translate-x-3.5 bg-green-400': league.isActive, 'bg-gray-400': !league.isActive}"></div>
                                        </div>
                                    </div>
                                </div>

                                <div v-if="league.isExpanded" class="px-2 py-1.5 bg-[#121214] border-t border-[#323238]">
                                    <div v-if="!league.teams || league.teams.length === 0" class="text-[10px] text-gray-600 italic text-center">
                                        Vazio
                                    </div>
                                    <div v-else class="grid grid-cols-3 md:grid-cols-4 gap-1.5">
                                        <div v-for="team in (league.teams || [])" :key="team.id" 
                                             class="flex items-center justify-between px-1.5 py-0.5 rounded border text-[10px] transition-all hover:border-blue-500/30 cursor-pointer select-none" 
                                             :class="team.isActive ? 'bg-[#1c1c1e] border-[#323238]' : 'bg-red-900/5 border-transparent opacity-50'"
                                             @click="team.isActive = !team.isActive">
                                            
                                            <span class="truncate pr-1 text-gray-400" 
                                                  :class="{'text-yellow-400 font-bold': isMatch(team.name)}"
                                                  :title="team.name">
                                                {{ team.name }}
                                            </span>
                                            <div class="w-1.5 h-1.5 rounded-full" :class="team.isActive ? 'bg-blue-500' : 'bg-gray-700'"></div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="modal-footer h-12 min-h-[48px]">
                <span class="summary-text text-[10px] text-gray-500">
                    <strong class="text-gray-300">{{ countActiveSports() }}</strong> esportes selecionados
                </span>
                <div class="modal-actions gap-2">
                    <button @click="showModal = false" class="btn-cancel text-xs px-3 py-1.5">Cancelar</button>
                    <button @click="saveNewTemplate" class="btn-confirm text-xs px-3 py-1.5">Salvar</button>
                </div>
            </div>
        </div>
    </div>

  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import tournamentService from "../../../services/Tournament/TournamentService";
import tournamentTemplateService, { type TournamentTemplate } from "../../../services/Tournament/TournamentTemplateService";
import apiSports from "../../../services/apiSports";
import Swal from 'sweetalert2';
import type { Tournament } from "../../../models/Tournament/Tournament";

// --- INTERFACES ---
interface Team { id: string; name: string; isActive: boolean; }
interface League { id: string; name: string; isActive: boolean; isExpanded?: boolean; teams: Team[]; }
interface Sport { key: string; name: string; icon: string; isActive: boolean; leagues: League[]; }

export default defineComponent({
  name: 'TournamentCreate',
  data() {
    return {
      isLoading: false,
      selectedTemplateId: null as number | null,
      templates: [] as TournamentTemplate[],
      
      form: {
        name: '', 
        description: '', 
        entryFee: 50.00, 
        initialFantasyBalance: 1000,
        startDate: '', 
        endDate: '', 
        sport: 'Futebol', 
        isActive: true, 
        houseFeePercent: 10
      },

      showModal: false,
      newTemplateName: '',
      sportsData: [] as Sport[],
      selectedSportKey: null as string | null,
      searchQuery: '',
    };
  },
  computed: {
    currentSport(): Sport | undefined {
        return (this.sportsData || []).find(s => s.key === this.selectedSportKey);
    },
    
    filteredLeagues(): League[] {
        if (!this.currentSport || !this.currentSport.leagues) return [];
        
        const query = this.searchQuery.toLowerCase().trim();
        if (!query) return this.currentSport.leagues;

        return this.currentSport.leagues.filter(league => {
            const leagueNameMatch = league.name.toLowerCase().includes(query);
            const hasTeamMatch = (league.teams || []).some(t => t.name.toLowerCase().includes(query));

            if (hasTeamMatch && !leagueNameMatch) {
                league.isExpanded = true;
            }
            return leagueNameMatch || hasTeamMatch;
        });
    }
  },
  async mounted() {
      await this.loadTemplates();
  },
  methods: {
    isMatch(text: string): boolean {
        if (!this.searchQuery) return false;
        return text.toLowerCase().includes(this.searchQuery.toLowerCase());
    },

    async loadTemplates() {
        try {
            const res = await tournamentTemplateService.getAll();
            this.templates = Array.isArray(res.data) ? res.data : [];
        } catch (e) { console.error(e); }
    },

    async openModelCreator() {
        this.newTemplateName = '';
        this.searchQuery = ''; 
        this.showModal = true;
        
        if (this.sportsData && this.sportsData.length > 0) {
            // Se já carregou, garante que tudo esteja RESETADO para TRUE (Padrão)
            this.sportsData.forEach(s => {
                s.isActive = true;
                if(s.leagues) {
                    s.leagues.forEach(l => {
                        l.isActive = true;
                        if(l.teams) l.teams.forEach(t => t.isActive = true);
                    });
                }
            });
        } else {
            await this.loadFullConfiguration();
        }
    },

    async loadFullConfiguration() {
        try {
            const response = await apiSports.get('/admin/configuration');
            const rawData = (response && response.data) ? response.data : [];
            const safeData = (Array.isArray(rawData) ? rawData : []) as any[];

            this.sportsData = safeData.map((s: any) => ({
                key: s.key || 'unknown',
                name: s.name || 'Unknown',
                icon: s.icon || '',
                isActive: true, // ✅ CORRIGIDO: Inicia Selecionado
                leagues: (Array.isArray(s.leagues) ? s.leagues : []).map((l: any) => ({
                    // Tenta pegar ID numérico
                    id: String(l.league_id || l.id || l.name),
                    name: l.name || '',
                    isActive: true, // ✅ CORRIGIDO: Inicia Selecionado
                    isExpanded: false,
                    teams: (Array.isArray(l.teams) ? l.teams : []).map((t: any) => ({
                        // ⚠️ CORREÇÃO CRÍTICA: Fallback para o NOME se não tiver ID numérico
                        id: String(t.team_id || t.id || t.name), 
                        name: t.name || '',
                        isActive: true // Padrão: Time selecionado
                    }))
                }))
            }));

            const firstSport = this.sportsData[0];
            if (firstSport && !this.selectedSportKey) {
                this.selectedSportKey = firstSport.key;
            }
        } catch (e) {
            Swal.fire({ icon: 'error', title: 'Erro', text: 'Falha ao carregar esportes.', background: '#1e293b', color: '#fff' });
        }
    },

    selectSport(key: string) { 
        this.selectedSportKey = key; 
        this.searchQuery = ''; 
    },
    
    toggleLeagueExpansion(league: League) { league.isExpanded = !league.isExpanded; },
    
    // Sincroniza Times com a Liga
    syncLeagueTeams(league: League) {
        if (league.teams && league.teams.length > 0) {
            league.teams.forEach(t => t.isActive = league.isActive);
        }
    },

    countActiveSports() {
        return (this.sportsData || []).filter(s => s.isActive).length;
    },


async saveNewTemplate() {
        if (!this.newTemplateName) {
            Swal.fire({ toast: true, icon: 'warning', title: 'Digite um nome para o modelo', background: '#334155', color: '#fff' });
            return;
        }

        const safeSource = (this.sportsData || []) as any[];

        const activeConfig = {
            sports: safeSource
                .filter((s: any) => s.isActive)
                .map((s: any) => {
                    const leagues = Array.isArray(s.leagues) ? s.leagues : [];
                    
                    const activeLeagues = leagues
                        .filter((l: any) => l.isActive)
                        .map((l: any) => {
                            const teams = Array.isArray(l.teams) ? l.teams : [];
                            
                            // --- CORREÇÃO AQUI ---
                            // 1. Pega os times ATIVOS
                            const activeTeams = teams.filter((t: any) => t.isActive);

                            // 2. Mapeia para ID ou Nome (Segurança máxima)
                            const selectedIds = activeTeams.map((t: any) => {
                                // Se o ID existir e não for vazio, usa ele. Senão, usa o Nome.
                                const val = (t.id && String(t.id).trim() !== '') ? t.id : t.name;
                                return String(val);
                            });

                            return {
                                id: String(l.id || l.name), 
                                name: l.name,
                                // Lógica:
                                // Se o número de ativos for IGUAL ao total, manda [] (Todos liberados)
                                // Se for MENOR (você desmarcou algum), manda a lista de nomes.
                                teams: activeTeams.length === teams.length ? [] : selectedIds
                            };
                        });

                    return {
                        key: s.key,
                        leagues: activeLeagues
                    };
                })
                .filter((s: any) => s.leagues.length > 0)
        };

        if (activeConfig.sports.length === 0) {
            Swal.fire({ title: 'Modelo Vazio', text: 'Selecione pelo menos um esporte e uma liga.', icon: 'warning', background: '#1e293b', color: '#fff' });
            return;
        }

        try {
            const res = await tournamentTemplateService.create({
                name: this.newTemplateName,
                filterRules: JSON.stringify(activeConfig)
            });

            if (res && res.data) {
                this.templates.push(res.data);
                this.selectedTemplateId = res.data.id || null;
            }

            this.showModal = false;
            Swal.fire({ icon: 'success', title: 'Salvo!', background: '#1e293b', color: '#fff', timer: 1500, showConfirmButton: false });
        } catch (e) {
            Swal.fire({ icon: 'error', title: 'Erro', text: 'Falha ao criar modelo.', background: '#1e293b', color: '#fff' });
        }
    },

    
    async submitTournament() {
      const template = this.templates.find(t => t.id === this.selectedTemplateId);

      if (!template) {
          Swal.fire({ title: 'Atenção', text: 'Selecione um Modelo de Jogos válido.', icon: 'warning', background: '#1e293b', color: '#fff' });
          return;
      }

      this.isLoading = true;
      try {
        const payload: Tournament = {
            id: 0, 
            name: this.form.name,
            description: this.form.description,
            entryFee: this.form.entryFee,
            initialFantasyBalance: this.form.initialFantasyBalance,
            startDate: new Date(this.form.startDate).toISOString(),
            endDate: new Date(this.form.endDate).toISOString(),
            sport: this.form.sport,
            isActive: this.form.isActive,
            houseFeePercent: this.form.houseFeePercent,
            isFinished: false,
            filterRules: template?.filterRules || '[]',
            prizePool: 0,
            participantsCount: 0,
            isJoined: false
        };

        await tournamentService.createTournament(payload);
        
        Swal.fire({ 
            title: 'Sucesso!', 
            text: `Torneio criado com modelo: ${template?.name || 'Novo Modelo'}`, 
            icon: 'success', 
            background: '#1e293b', 
            color: '#fff' 
        });
        this.$router.push('/admin/tournaments');
      } catch (error) {
        console.error(error);
        Swal.fire({ title: 'Erro', text: 'Falha ao criar torneio.', icon: 'error', background: '#1e293b', color: '#fff' });
      } finally {
        this.isLoading = false;
      }
    }
  }
});
</script>


<style scoped>
/* CSS Focado na Elegância Dark & Compacta */
.admin-container { padding: 1rem; color: #f8fafc; max-width: 1200px; margin: 0 auto; font-family: 'Inter', sans-serif; }
.header-actions { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
.main-title { font-size: 1.1rem; font-weight: 700; color: #ffd700; margin: 0; }
.subtitle { font-size: 0.75rem; color: #94a3b8; margin: 0; }
.btn-back { background: #27272a; color: #a1a1aa; border: 1px solid #3f3f46; padding: 0.3rem 0.8rem; border-radius: 4px; font-size: 0.7rem; font-weight: 600; cursor: pointer; }

/* Formulário Principal */
.form-card { background: #121214; padding: 1.5rem; border-radius: 8px; border: 1px solid #27272a; }
.slim-form { display: flex; flex-direction: column; gap: 0.8rem; }
.section-label { font-size: 0.8rem; font-weight: 700; color: #fbbf24; text-transform: uppercase; margin-bottom: 0.2rem; display: block; }
.divider { border: 0; border-top: 1px solid #27272a; margin: 0.5rem 0; }
.template-controls { display: flex; gap: 6px; }
.template-select { flex: 1; background: #18181b; border: 1px solid #3f3f46; color: white; padding: 6px; border-radius: 4px; font-size: 0.8rem; outline: none; }
.btn-new-model { background: #2563eb; color: white; border: none; padding: 0 10px; border-radius: 4px; font-size: 0.8rem; font-weight: 600; cursor: pointer; }

/* Estilos de Input e Grid */
.grid { display: grid; }
.grid-cols-2 { grid-template-columns: repeat(2, 1fr); }
.grid-cols-3 { grid-template-columns: repeat(3, 1fr); }
.gap-3 { gap: 0.75rem; }

.form-group { display: flex; flex-direction: column; gap: 2px; }
.form-group label { font-size: 0.7rem; font-weight: 600; color: #71717a; text-transform: uppercase; }
.form-group input, .form-group textarea { background: #18181b; border: 1px solid #27272a; color: #e4e4e7; padding: 6px; border-radius: 4px; font-size: 0.8rem; outline: none; transition: border-color 0.2s; width: 100%; }
.form-group input:focus, .form-group textarea:focus { border-color: #fbbf24; }
.resize-none { resize: none; }

/* Containers Relativos para Prefixos/Sufixos */
.relative-input-container { position: relative; width: 100%; }
.prefix { position: absolute; left: 0.5rem; top: 50%; transform: translateY(-50%); font-size: 0.7rem; color: #71717a; font-weight: bold; }
.suffix { position: absolute; right: 0.5rem; top: 50%; transform: translateY(-50%); font-size: 0.7rem; color: #71717a; font-weight: bold; }
.input-with-prefix { padding-left: 1.8rem !important; }
.input-with-suffix { padding-right: 1.8rem !important; }

/* Botão Salvar */
.btn-save { background: #ffd700; color: #000; font-weight: 800; padding: 10px; border: none; border-radius: 4px; cursor: pointer; font-size: 0.9rem; text-transform: uppercase; width: 100%; display: flex; justify-content: center; gap: 0.5rem; align-items: center; }
.btn-save:hover { background: #eab308; }
.btn-save:disabled { opacity: 0.5; cursor: not-allowed; }
.loader-spin { animation: spin 1s linear infinite; font-size: 0.8rem; }
@keyframes spin { 100% { transform: rotate(360deg); } }

/* === MODAL ULTRA COMPACTO === */
.modal-overlay { 
    position: fixed; inset: 0; background: rgba(0,0,0,0.85); backdrop-filter: blur(2px); z-index: 100; 
    display: flex; align-items: center; justify-content: center;
}

.modal-content { 
    background: #09090b; 
    width: 90%; 
    max-width: 800px; 
    max-height: 90vh; 
    border-radius: 8px; 
    border: 1px solid #27272a; 
    display: flex; 
    flex-direction: column; 
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7);
}

.modal-header { 
    padding: 0.75rem 1rem; 
    background: #121214; 
    border-bottom: 1px solid #27272a; 
    display: flex; justify-content: space-between; align-items: center; 
    height: 45px;
    flex-shrink: 0;
}

.modal-body { 
    flex: 1; 
    padding: 1rem; 
    display: flex; 
    flex-direction: column; 
    overflow: hidden; 
}

.modal-footer { 
    padding: 0 1rem; 
    background: #121214; 
    border-top: 1px solid #27272a; 
    display: flex; justify-content: space-between; align-items: center; 
    flex-shrink: 0;
}

.btn-close { font-size: 1rem; color: #52525b; transition: 0.2s; background: none; border: none; cursor: pointer; }
.btn-cancel { background: transparent; color: #a1a1aa; border: 1px solid #3f3f46; border-radius: 4px; transition: 0.2s; cursor: pointer; }
.btn-cancel:hover { border-color: #71717a; color: white; }
.btn-confirm { background: #fbbf24; color: #000; border-radius: 4px; font-weight: 700; border: none; transition: 0.2s; cursor: pointer; }
.btn-confirm:hover { background: #f59e0b; }

/* Scrollbars finas */
.custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #09090b; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #52525b; }

.animate-scale-in { animation: scaleIn 0.2s ease-out; }
@keyframes scaleIn { from { transform: scale(0.98); opacity: 0; } to { transform: scale(1); opacity: 1; } }
</style>