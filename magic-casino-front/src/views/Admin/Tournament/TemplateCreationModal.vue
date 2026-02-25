<template>
  <transition name="modal-fade">
    <div class="modal-overlay">
      <div class="modal-content animate-scale-in">
        <div class="modal-header">
          <div class="modal-title">
            <span class="icon">🛠️</span>
            <h3>Novo Modelo</h3>
          </div>
          <button @click="$emit('close')" class="btn-close">✕</button>
        </div>
        
        <div class="modal-body custom-scrollbar">
          <div class="form-group mb-4">
            <label>Nome do Modelo</label>
            <input v-model="newTemplateName" type="text" placeholder="Ex: Futebol Europeu Principal" autofocus />
          </div>

          <div class="sports-container mb-4">
            <label class="section-label">Esportes</label>
            <div class="sports-scroll">
              <div 
                v-for="sport in sportsData" 
                :key="sport.key"
                @click="selectSport(sport.key)"
                class="sport-card"
                :class="selectedSportKey === sport.key ? 'active' : ''"
              >
                <div class="sport-header">
                  <span class="sport-icon">{{ sport.icon }}</span> 
                  <div @click.stop>
                    <input type="checkbox" v-model="sport.isActive" class="custom-checkbox">
                  </div>
                </div>
                <span class="sport-name">{{ sport.name }}</span>
              </div>
            </div>
          </div>

          <div v-if="currentSport" class="leagues-panel">
            <div class="leagues-header">
              <div class="flex items-center gap-3">
                <h4 class="text-sm font-bold text-gray-200">{{ currentSport.name }} <span class="badge">{{ filteredLeagues.length }}</span></h4>
                <button @click="toggleAllLeagues" class="btn-link">
                  {{ areAllSelected ? 'Desmarcar Tudo' : 'Marcar Tudo' }}
                </button>
              </div>
              <div class="search-box">
                <span class="search-icon">🔍</span>
                <input v-model="searchQuery" type="text" placeholder="Buscar liga..." />
              </div>
            </div>

            <div class="leagues-list custom-scrollbar">
              <div v-if="filteredLeagues.length === 0" class="empty-state">
                Nada encontrado para "{{ searchQuery }}".
              </div>

              <div v-for="league in filteredLeagues" :key="league.id" class="league-item" :class="{ 'inactive': !league.isActive }">
                <div class="league-row" @click="toggleLeagueExpansion(league)">
                  <div class="lr-content">
                    <span class="arrow" :class="{'rotated': league.isExpanded}">▶</span>
                    <span class="league-name" :class="{'highlight': isMatch(league.name)}">{{ league.name }}</span>
                    <span class="count-badge">{{ (league.teams || []).length }}</span>
                  </div>
                  <div class="lr-actions" @click.stop>
                    <label class="switch">
                      <input type="checkbox" v-model="league.isActive" @change="syncLeagueTeams(league)">
                      <span class="slider"></span>
                    </label>
                  </div>
                </div>

                <transition name="slide-down">
                  <div v-if="league.isExpanded" class="teams-grid">
                    <div v-if="!league.teams || league.teams.length === 0" class="empty-teams">Vazio</div>
                    <div v-for="team in (league.teams || [])" :key="team.id" 
                      class="team-chip" 
                      :class="team.isActive ? 'selected' : ''"
                      @click="team.isActive = !team.isActive">
                      <span class="truncate tname" :class="{'highlight': isMatch(team.name)}">{{ team.name }}</span>
                      <div class="status-indicator" :class="team.isActive ? 'on' : 'off'"></div>
                    </div>
                  </div>
                </transition>
              </div>
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <span class="mf-info">
            <strong>{{ countActiveSports() }}</strong> esportes selecionados
          </span>
          <div class="mf-actions">
            <button @click="$emit('close')" class="btn-ghost">Cancelar</button>
            <button @click="saveNewTemplate" class="btn-primary small">Salvar Modelo</button>
          </div>
        </div>
      </div>
    </div>
  </transition>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import tournamentTemplateService from "../../../services/Tournament/TournamentTemplateService";
import SportsService from "../../../services/SportsService"; 
import Swal from 'sweetalert2';

interface Team { id: string; name: string; isActive: boolean; }
interface League { id: string; name: string; isActive: boolean; isExpanded?: boolean; teams: Team[]; }
interface Sport { key: string; name: string; icon: string; isActive: boolean; leagues: League[]; }

export default defineComponent({
  name: 'TemplateCreationModal',
  emits: ['close', 'saved'],
  data() {
    return {
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
    },
    areAllSelected(): boolean {
        if (this.filteredLeagues.length === 0) return false;
        return this.filteredLeagues.every(l => l.isActive);
    }
  },
  async mounted() {
      await this.loadFullConfiguration();
  },
  methods: {
    isMatch(text: string): boolean {
        if (!this.searchQuery) return false;
        return text.toLowerCase().includes(this.searchQuery.toLowerCase());
    },
    toggleAllLeagues() {
        const targetState = !this.areAllSelected;
        this.filteredLeagues.forEach(l => {
            l.isActive = targetState;
            this.syncLeagueTeams(l);
        });
    },
    async loadFullConfiguration() {
        try {
            const data = await SportsService.getAdminConfig();
            const rawData = Array.isArray(data) ? data : (data.data || []);
            const safeData = (Array.isArray(rawData) ? rawData : []) as any[];

            this.sportsData = safeData.map((s: any) => ({
                key: s.key || s.Key || 'unknown',
                name: s.name || s.Name || 'Unknown',
                icon: s.icon || s.Icon || '',
                isActive: true, 
                leagues: (Array.isArray(s.leagues || s.Leagues) ? (s.leagues || s.Leagues) : []).map((l: any) => ({
                    id: String(l.Id || l.id || l.LeagueId || l.league_id || l.name),
                    name: l.name || l.Name || '',
                    isActive: true, 
                    isExpanded: false,
                    teams: (Array.isArray(l.teams || l.Teams) ? (l.teams || l.Teams) : []).map((t: any) => ({
                        id: String(t.Id || t.id || t.TeamId || t.team_id || t.name), 
                        name: t.name || t.Name || '',
                        isActive: true 
                    }))
                }))
            }));

            const firstSport = this.sportsData[0];
            if (firstSport && !this.selectedSportKey) {
                this.selectedSportKey = firstSport.key;
            }
        } catch (e) {
            console.error("Erro config:", e);
            Swal.fire({ icon: 'error', title: 'Erro', text: 'Falha ao carregar esportes. Verifique a conexão.', background: '#1e293b', color: '#fff' });
        }
    },
    selectSport(key: string) { 
        this.selectedSportKey = key; 
        this.searchQuery = ''; 
    },
    toggleLeagueExpansion(league: League) { league.isExpanded = !league.isExpanded; },
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
            sports: safeSource.filter((s: any) => s.isActive).map((s: any) => {
                    const leagues = Array.isArray(s.leagues) ? s.leagues : [];
                    const activeLeagues = leagues.filter((l: any) => l.isActive).map((l: any) => {
                            const teams = Array.isArray(l.teams) ? l.teams : [];
                            const activeTeams = teams.filter((t: any) => t.isActive);
                            const selectedIds = activeTeams.map((t: any) => {
                                const val = (t.id && String(t.id).trim() !== '') ? t.id : t.name;
                                return String(val);
                            });
                            const lgId = (l.id && String(l.id).trim() !== '') ? l.id : l.name;
                            return { id: String(lgId), name: l.name, teams: activeTeams.length === teams.length ? [] : selectedIds };
                        });
                    return { key: s.key, leagues: activeLeagues };
                }).filter((s: any) => s.leagues.length > 0)
        };

        if (activeConfig.sports.length === 0) {
            Swal.fire({ title: 'Modelo Vazio', text: 'Selecione pelo menos um esporte e uma liga.', icon: 'warning', background: '#1e293b', color: '#fff' });
            return;
        }

        try {
            const res = await tournamentTemplateService.create({ name: this.newTemplateName, filterRules: JSON.stringify(activeConfig) });
            
            if (res && res.data) {
                // Emite o evento "saved" mandando o novo template para o componente pai
                this.$emit('saved', res.data);
            }
            this.$emit('close');
            Swal.fire({ icon: 'success', title: 'Salvo!', background: '#1e293b', color: '#fff', timer: 1500, showConfirmButton: false });
        } catch (e) {
            Swal.fire({ icon: 'error', title: 'Erro', text: 'Falha ao criar modelo.', background: '#1e293b', color: '#fff' });
        }
    }
  }
});
</script>

<style scoped>
/* Estilos extraídos exclusivamente para o Modal */
.form-group { display: flex; flex-direction: column; gap: 0.4rem; }
.form-group label { font-size: 0.75rem; font-weight: 600; color: #a1a1aa; text-transform: uppercase; }
input { background: #09090b; border: 1px solid #3f3f46; color: #f4f4f5; padding: 0.65rem 0.75rem; border-radius: 6px; font-size: 0.85rem; outline: none; transition: all 0.2s; width: 100%; font-family: inherit; }
input:focus { border-color: #fbbf24; box-shadow: 0 0 0 2px rgba(251, 191, 36, 0.1); }

.modal-overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.85); backdrop-filter: blur(4px); z-index: 1000; display: flex; align-items: center; justify-content: center; }
.modal-content { background: #09090b; width: 95%; max-width: 900px; height: 85vh; border-radius: 12px; border: 1px solid #27272a; display: flex; flex-direction: column; box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5); overflow: hidden; }
.modal-header { padding: 1rem 1.5rem; border-bottom: 1px solid #27272a; display: flex; justify-content: space-between; align-items: center; background: #121214; }
.modal-title { display: flex; align-items: center; gap: 0.5rem; }
.modal-title .icon { font-size: 1.2rem; }
.modal-title h3 { margin: 0; font-size: 1rem; color: #fff; font-weight: 700; }
.btn-close { background: none; border: none; color: #52525b; font-size: 1.2rem; cursor: pointer; }
.btn-close:hover { color: #fff; }

.modal-body { flex: 1; padding: 1.5rem; overflow-y: auto; background: #09090b; }

.sports-container { margin-bottom: 1.5rem; }
.section-label { font-size: 0.75rem; font-weight: 700; color: #71717a; text-transform: uppercase; display: block; margin-bottom: 0.5rem; }
.sports-scroll { display: flex; gap: 0.75rem; overflow-x: auto; padding-bottom: 0.5rem; }
.sport-card { min-width: 110px; background: #18181b; border: 1px solid #27272a; border-radius: 8px; padding: 0.75rem; cursor: pointer; transition: all 0.2s; display: flex; flex-direction: column; justify-content: space-between; height: 70px; }
.sport-card:hover { border-color: #52525b; background: #202024; }
.sport-card.active { border-color: #3b82f6; background: rgba(59, 130, 246, 0.1); }
.sport-header { display: flex; justify-content: space-between; }
.custom-checkbox { accent-color: #fbbf24; width: 18px; height: 18px; cursor: pointer; filter: drop-shadow(0 0 2px rgba(251, 191, 36, 0.5)); }
.sport-name { font-size: 0.75rem; font-weight: 600; color: #e4e4e7; margin-top: auto; }

.leagues-panel { background: #121214; border: 1px solid #27272a; border-radius: 8px; display: flex; flex-direction: column; overflow: hidden; min-height: 300px; }
.leagues-header { padding: 0.75rem; background: #18181b; border-bottom: 1px solid #27272a; display: flex; justify-content: space-between; align-items: center; }
.badge { background: #27272a; color: #a1a1aa; font-size: 0.65rem; padding: 1px 6px; border-radius: 4px; margin-left: 0.5rem; }
.btn-link { background: none; border: none; color: #3b82f6; font-size: 0.7rem; font-weight: 600; cursor: pointer; margin-left: 1rem; }
.btn-link:hover { text-decoration: underline; }
.search-box { position: relative; width: 200px; }
.search-box input { padding-left: 2rem; font-size: 0.8rem; background: #09090b; }
.search-icon { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); font-size: 0.8rem; color: #52525b; }

.leagues-list { flex: 1; overflow-y: auto; padding: 0.5rem; }
.empty-state { padding: 2rem; text-align: center; font-size: 0.8rem; color: #52525b; font-style: italic; }

.league-item { border: 1px solid #27272a; border-radius: 6px; margin-bottom: 0.5rem; background: #18181b; overflow: hidden; transition: opacity 0.2s; }
.league-item.inactive { opacity: 0.6; border-color: #ef444430; background: #2f151520; }
.league-row { display: flex; justify-content: space-between; align-items: center; padding: 0.6rem 0.75rem; cursor: pointer; transition: background 0.2s; }
.league-row:hover { background: #202024; }
.lr-content { display: flex; align-items: center; gap: 0.75rem; flex: 1; }
.arrow { font-size: 0.6rem; color: #52525b; transition: transform 0.2s; }
.arrow.rotated { transform: rotate(90deg); }
.league-name { font-size: 0.85rem; font-weight: 600; color: #e4e4e7; }
.league-name.highlight { color: #fbbf24; }
.count-badge { font-size: 0.7rem; background: #27272a; color: #a1a1aa; padding: 1px 5px; border-radius: 4px; }

.switch { position: relative; display: inline-block; width: 34px; height: 18px; }
.switch input { opacity: 0; width: 0; height: 0; }
.slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background-color: #3f3f46; transition: .4s; border-radius: 34px; }
.slider:before { position: absolute; content: ""; height: 16px; width: 16px; left: 2px; bottom: 2px; background-color: white; transition: .4s; border-radius: 50%; }
input:checked + .slider { background-color: #10b981; }
input:checked + .slider:before { transform: translateX(16px); }

.teams-grid { padding: 0.75rem; background: #00000030; border-top: 1px solid #27272a; display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 0.75rem; }
.empty-teams { font-size: 0.8rem; color: #52525b; font-style: italic; }
.team-chip { background: #27272a; padding: 0.5rem 0.75rem; border-radius: 6px; border: 1px solid transparent; display: flex; justify-content: space-between; align-items: center; cursor: pointer; transition: all 0.2s; user-select: none; }
.team-chip:hover { background: #3f3f46; color: #fff; }
.team-chip.selected { background: rgba(251, 191, 36, 0.15); border-color: #fbbf24; }
.team-chip.selected .tname { color: #fbbf24; font-weight: 600; }
.tname { font-size: 0.8rem; color: #a1a1aa; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 85%; }
.tname.highlight { text-decoration: underline; text-decoration-color: #fbbf24; }
.status-indicator { width: 6px; height: 6px; border-radius: 50%; background: #52525b; }
.status-indicator.on { background: #fbbf24; box-shadow: 0 0 6px #fbbf24; }

.modal-footer { padding: 1rem 1.5rem; background: #18181b; border-top: 1px solid #27272a; display: flex; justify-content: space-between; align-items: center; }
.mf-info { font-size: 0.85rem; color: #a1a1aa; }
.mf-info strong { color: #fff; }
.mf-actions { display: flex; gap: 0.75rem; }
.btn-ghost { background: transparent; color: #a1a1aa; border: 1px solid #3f3f46; padding: 0.6rem 1.2rem; border-radius: 6px; font-weight: 600; cursor: pointer; transition: 0.2s; }
.btn-ghost:hover { border-color: #52525b; color: #fff; }
.btn-primary.small { background: linear-gradient(135deg, #fbbf24 0%, #d97706 100%); color: #000; border: none; font-weight: 800; border-radius: 6px; width: auto; font-size: 0.85rem; padding: 0.6rem 1.5rem; box-shadow: none; cursor: pointer; }

.custom-scrollbar::-webkit-scrollbar { width: 6px; height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #52525b; }

.animate-scale-in { animation: scaleIn 0.25s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes scaleIn { from { transform: scale(0.95); opacity: 0; } to { transform: scale(1); opacity: 1; } }
</style>