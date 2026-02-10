<template>
  <div class="admin-container animate-fade-in">
    
    <div class="header-actions">
      <div class="title-section">
        <div class="icon-wrapper">🏆</div>
        <div>
          <h2 class="main-title">Novo Torneio</h2>
          <p class="subtitle">Defina regras, visual e o modelo de jogos.</p>
        </div>
      </div>
      <button @click="$router.go(-1)" class="btn-back">
        <span class="arrow">←</span> Voltar
      </button>
    </div>

    <div class="content-wrapper">
      <form @submit.prevent="submitTournament" class="main-form">
        
        <div class="form-card">
          <div class="card-header">
            <span class="step-indicator">01</span>
            <h3>Identidade Visual</h3>
          </div>
          <div class="card-body">
            
            <div class="form-group mb-6">
                <label>Categoria do Torneio</label>
                
                <div class="category-wrapper">
                    <div v-if="!isCreatingCategory" class="select-mode">
                        <div class="select-wrapper flex-1">
                            <select v-model="form.category" class="custom-select">
                                <option disabled value="">Selecione uma categoria...</option>
                                <option v-for="cat in existingCategories" :key="cat" :value="cat">{{ cat }}</option>
                            </select>
                            <span class="chevron">▼</span>
                        </div>
                        <button type="button" @click="toggleCategoryMode" class="btn-create-cat">
                            + Nova
                        </button>
                    </div>

                    <div v-else class="create-mode animate-fade-in">
                        <input 
                            v-model="form.category" 
                            type="text" 
                            placeholder="Digite o nome da nova categoria..." 
                            class="input-highlight" 
                            autofocus
                        />
                        <button type="button" @click="toggleCategoryMode" class="btn-cancel-cat">
                            ✕ Cancelar
                        </button>
                    </div>
                </div>
                <p class="helper-text">
                    {{ isCreatingCategory ? 'Digite o nome para criar uma nova tag.' : 'Escolha uma lista existente ou crie uma nova.' }}
                </p>
            </div>

            <div class="form-group">
                <label>Imagem de Capa</label>
                
                <div v-if="hasImages" class="covers-grid custom-scrollbar">
                    <div 
                        v-for="(path, fileName) in availableCovers" 
                        :key="fileName"
                        @click="selectCover(fileName)"
                        class="cover-item"
                        :class="{ 'selected': form.coverImage === fileName }"
                    >
                        <img :src="path" loading="lazy" />
                        <div class="selection-overlay">
                            <span class="check-icon">✔</span>
                        </div>
                        <span class="file-name">{{ fileName }}</span>
                    </div>
                </div>

                <div v-else class="empty-covers">
                    <div class="icon">📁</div>
                    <p>Nenhuma imagem encontrada.</p>
                    <small>Adicione imagens em: <br> <code>src/assets/tournament_covers/</code></small>
                </div>
            </div>

          </div>
        </div>

        <div class="form-card">
          <div class="card-header">
            <span class="step-indicator">02</span>
            <h3>Modelo de Jogos</h3>
          </div>
          <div class="card-body">
            <div class="form-group">
              <label>Selecione um Modelo</label>
              <div class="input-group">
                <div class="select-wrapper flex-1">
                  <select v-model="selectedTemplateId" required class="custom-select">
                    <option :value="null" disabled>-- Selecione um Modelo --</option>
                    <option v-for="t in templates" :key="(t as any).id || (t as any).Id" :value="(t as any).id || (t as any).Id">
                      {{ (t as any).name || (t as any).Name }}
                    </option>
                  </select>
                  <span class="chevron">▼</span>
                </div>
                <button type="button" @click="openModelCreator" class="btn-secondary">
                  <span>+</span> Novo
                </button>
              </div>
              
              <p v-if="!isLoading && templates.length === 0" class="helper-warning">
                ⚠️ Nenhum modelo encontrado. Crie um novo.
              </p>
            </div>
          </div>
        </div>

        <div class="form-card">
          <div class="card-header">
            <span class="step-indicator">03</span>
            <h3>Configuração do Evento</h3>
          </div>
          
          <div class="card-body grid-layout">
            <div class="form-group full-width">
              <label>Nome do Torneio</label>
              <input v-model="form.name" type="text" placeholder="Ex: Rei da Bet - Premier League" required />
            </div>

            <div class="form-group">
              <label>Entrada (R$)</label>
              <div class="input-icon-wrapper">
                <span class="icon-prefix">R$</span>
                <input v-model.number="form.entryFee" type="number" step="0.01" min="0" class="pl-8" required />
              </div>
            </div>

            <div class="form-group">
              <label>Tipo de Prêmio</label>
              <div class="select-wrapper">
                 <select v-model="prizeType" class="custom-select">
                    <option value="dynamic">Acumulado (Inscrições)</option>
                    <option value="fixed">Fixo (Garantido)</option>
                 </select>
                 <span class="chevron">▼</span>
              </div>
            </div>

            <div class="form-group" v-if="prizeType === 'fixed'">
               <label class="highlight-label">Valor Garantido (R$)</label>
               <div class="input-icon-wrapper">
                  <span class="icon-prefix text-gold">R$</span>
                  <input v-model.number="form.fixedPrize" type="number" step="10" min="0" class="pl-8 input-gold" />
               </div>
            </div>

            <div class="form-group" v-else>
              <label>Taxa Casa (%)</label>
              <div class="input-icon-wrapper right">
                <input v-model.number="form.houseFeePercent" type="number" step="1" min="0" max="100" class="pr-8" required />
                <span class="icon-suffix">%</span>
              </div>
            </div>

            <div class="form-group">
                <div class="flex justify-between items-center mb-1">
                    <label>Participantes</label>
                    <div class="flex items-center gap-2">
                        <span class="text-[10px] text-gray-400 uppercase font-bold tracking-wide">Ilimitado</span>
                        <input type="checkbox" v-model="isUnlimitedParticipants" class="accent-blue-500 w-4 h-4 cursor-pointer" />
                    </div>
                </div>
                <input 
                    v-model.number="form.maxParticipants" 
                    type="number" 
                    step="1" 
                    min="2" 
                    :disabled="isUnlimitedParticipants"
                    :class="{'opacity-50 cursor-not-allowed': isUnlimitedParticipants}"
                    placeholder="Ex: 100" 
                />
            </div>

            <div class="form-group">
              <label>Fichas Iniciais</label>
              <input v-model.number="form.initialFantasyBalance" type="number" step="1" min="100" placeholder="1000" required />
            </div>

            <div class="form-group full-width">
              <label class="highlight-label">Regra de Premiação 👑</label>
              <div class="select-wrapper">
                <select v-model="form.prizeRuleId" required class="custom-select prize-select">
                  <option v-for="rule in prizeOptions" :key="rule.id" :value="rule.id">
                    {{ rule.name }}
                  </option>
                </select>
                <span class="chevron text-gold">▼</span>
              </div>
            </div>

            <div class="form-group">
              <label>Início</label>
              <input v-model="form.startDate" type="datetime-local" required class="date-input" />
            </div>
            <div class="form-group">
              <label>Término</label>
              <input v-model="form.endDate" type="datetime-local" required class="date-input" />
            </div>

            <div class="form-group full-width">
              <label>Descrição (Opcional)</label>
              <textarea v-model="form.description" rows="2" placeholder="Detalhes ou regras específicas..."></textarea>
            </div>
          </div>
        </div>

        <div class="form-footer">
           <button type="submit" class="btn-primary" :disabled="isLoading || !selectedTemplateId">
            <span v-if="isLoading" class="loader"></span>
            {{ isLoading ? 'Processando...' : 'Publicar Torneio' }}
          </button>
        </div>

      </form>
    </div>

    <transition name="modal-fade">
      <div v-if="showModal" class="modal-overlay">
        <div class="modal-content animate-scale-in">
            <div class="modal-header">
                <div class="modal-title">
                    <span class="icon">🛠️</span>
                    <h3>Novo Modelo</h3>
                </div>
                <button @click="showModal = false" class="btn-close">✕</button>
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
                                        <span class="truncate" :class="{'highlight': isMatch(team.name)}">{{ team.name }}</span>
                                        <div class="status-dot" :class="team.isActive ? 'on' : 'off'"></div>
                                    </div>
                                </div>
                            </transition>
                        </div>
                    </div>
                </div>
            </div>

            <div class="modal-footer">
                <span class="summary-text">
                    <strong>{{ countActiveSports() }}</strong> esportes selecionados
                </span>
                <div class="modal-actions">
                    <button @click="showModal = false" class="btn-ghost">Cancelar</button>
                    <button @click="saveNewTemplate" class="btn-primary small">Salvar Modelo</button>
                </div>
            </div>
        </div>
      </div>
    </transition>

  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import tournamentService from "../../../services/Tournament/TournamentService";
import tournamentTemplateService, { type TournamentTemplate } from "../../../services/Tournament/TournamentTemplateService";
// ✅ CORREÇÃO 1: Usar o SportsService em vez do apiSports direto
import SportsService from "../../../services/SportsService"; 
import Swal from 'sweetalert2';

const coversModules = import.meta.glob('/src/assets/tournament_covers/*.{png,jpg,jpeg,svg,webp}', { eager: true });

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
      
      existingCategories: ['Destaques', 'Todo os torneios', 'Torneios turbo', 'Top da semana'],
      isCreatingCategory: false,

      availableCovers: {} as Record<string, string>,

      prizeType: 'dynamic', 
      isUnlimitedParticipants: true, 

      prizeOptions: [
        { id: "PREMIO_1", name: "🥇 Clássico Top 3 (50%, 30%, 20%)" },
        { id: "PREMIO_2", name: "🥈 Estendido Top 5 (1º 40%|2º 25%|3º 10%|4º 10%|5º 5%)" },
        { id: "PREMIO_3", name: "🥉 Double Up (50% Ganha / 50% Perde)" },
        { id: "WINNER_TAKES_ALL", name: "🏆 Vencedor Leva Tudo (100%)" }
      ],

      form: {
        name: '', 
        description: '', 
        entryFee: 50.00, 
        initialFantasyBalance: 1000,
        startDate: '', 
        endDate: '', 
        sport: 'Futebol', 
        isActive: true, 
        houseFeePercent: 10,
        
        fixedPrize: 0,
        maxParticipants: 0,

        prizeRuleId: 'PREMIO_1',
        category: 'Destaques',
        coverImage: '' 
      },

      showModal: false,
      newTemplateName: '',
      sportsData: [] as Sport[],
      selectedSportKey: null as string | null,
      searchQuery: '',
    };
  },
  computed: {
    hasImages(): boolean {
        return Object.keys(this.availableCovers).length > 0;
    },
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
      await this.loadTemplates();
      this.loadImages();
  },
  methods: {
    toggleCategoryMode() {
        this.isCreatingCategory = !this.isCreatingCategory;
        if (this.isCreatingCategory) {
            this.form.category = ''; 
        } else {
            this.form.category = this.existingCategories[0] || ''; 
        }
    },
    loadImages() {
        const covers: Record<string, string> = {};
        if (coversModules) {
            for (const path in coversModules) {
                const mod = coversModules[path] as any;
                const fileName = path.split('/').pop() || 'unknown';
                covers[fileName] = mod.default;
            }
        }
        this.availableCovers = covers;
        const keys = Object.keys(covers);
        if (keys.length > 0 && !this.form.coverImage) {
            this.form.coverImage = String(keys[0]); 
        }
    },
    selectCover(fileName: string) {
        this.form.coverImage = fileName;
    },

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
    async loadTemplates() {
        try {
            const res = await tournamentTemplateService.getAll();
            console.log("Templates carregados do banco:", res.data);
            this.templates = Array.isArray(res.data) ? res.data : (Array.isArray(res) ? res : []);
        } catch (e) { 
            console.error("Erro ao buscar templates:", e);
        }
    },
    async openModelCreator() {
        this.newTemplateName = '';
        this.searchQuery = ''; 
        this.showModal = true;
        if (this.sportsData && this.sportsData.length > 0) {
            // cache
        } else {
            await this.loadFullConfiguration();
        }
    },
    // ✅ CORREÇÃO 2: Método atualizado para usar SportsService.getAdminConfig()
    async loadFullConfiguration() {
        try {
            // Chama o serviço que já tem a baseURL correta (/sportbook/api/admin)
            const data = await SportsService.getAdminConfig();
            
            // O serviço retorna response.data, então podemos usar direto
            const rawData = Array.isArray(data) ? data : (data.data || []);
            const safeData = (Array.isArray(rawData) ? rawData : []) as any[];

            this.sportsData = safeData.map((s: any) => ({
                key: s.key || 'unknown',
                name: s.name || 'Unknown',
                icon: s.icon || '',
                isActive: true, 
                leagues: (Array.isArray(s.leagues) ? s.leagues : []).map((l: any) => ({
                    id: String(l.league_id || l.id || l.name),
                    name: l.name || '',
                    isActive: true, 
                    isExpanded: false,
                    teams: (Array.isArray(l.teams) ? l.teams : []).map((t: any) => ({
                        id: String(t.team_id || t.id || t.name), 
                        name: t.name || '',
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
            Swal.fire({ icon: 'error', title: 'Erro', text: 'Falha ao carregar esportes. Verifique a conexão com o Admin API.', background: '#1e293b', color: '#fff' });
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
                            return { id: String(l.id || l.name), name: l.name, teams: activeTeams.length === teams.length ? [] : selectedIds };
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
      const template = this.templates.find(t => (t as any).id === this.selectedTemplateId || (t as any).Id === this.selectedTemplateId);
      if (!template) {
          Swal.fire({ title: 'Atenção', text: 'Selecione um Modelo de Jogos válido.', icon: 'warning', background: '#1e293b', color: '#fff' });
          return;
      }
      this.isLoading = true;
      try {
        const payload: any = { 
            id: 0, 
            name: this.form.name,
            description: this.form.description,
            entryFee: this.form.entryFee,
            initialFantasyBalance: this.form.initialFantasyBalance,
            startDate: new Date(this.form.startDate).toISOString(),
            endDate: new Date(this.form.endDate).toISOString(),
            sport: this.form.sport,
            isActive: this.form.isActive,
            isFinished: false,
            filterRules: template?.filterRules || '[]',
            prizePool: 0,
            participantsCount: 0,
            isJoined: false,
            prizeRuleId: this.form.prizeRuleId,
            category: this.form.category,
            coverImage: this.form.coverImage,

            // ✅ LÓGICA DE NEGÓCIO DO FRONTEND PARA OS NOVOS CAMPOS
            // Se for Fixo, a taxa é 0 (ou irrelevante) e passamos o valor fixo
            houseFeePercent: this.prizeType === 'fixed' ? 0 : this.form.houseFeePercent,
            fixedPrize: this.prizeType === 'fixed' ? this.form.fixedPrize : null,
            // Se Ilimitado, manda 0
            maxParticipants: this.isUnlimitedParticipants ? 0 : this.form.maxParticipants,
        };
        
        await tournamentService.createTournament(payload);
        
        Swal.fire({ title: 'Sucesso!', text: `Torneio criado!`, icon: 'success', background: '#1e293b', color: '#fff' });
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
/* ESTILOS DA CATEGORIA */
.category-wrapper {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}
.select-mode, .create-mode {
    display: flex;
    gap: 0.5rem;
    align-items: center;
}
.btn-create-cat {
    background: #3b82f6;
    color: white;
    border: none;
    padding: 0.7rem 1rem;
    border-radius: 6px;
    font-weight: 700;
    cursor: pointer;
    white-space: nowrap;
    transition: 0.2s;
}
.btn-create-cat:hover { background: #2563eb; }

.btn-cancel-cat {
    background: #ef4444;
    color: white;
    border: none;
    padding: 0.7rem 1rem;
    border-radius: 6px;
    font-weight: 700;
    cursor: pointer;
    white-space: nowrap;
}
.input-highlight {
    border-color: #fbbf24 !important;
    background: #18181b;
    color: #fff;
    font-weight: bold;
}

/* ESTILOS DA GRADE DE CAPAS */
.covers-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
    gap: 1rem;
    max-height: 250px;
    overflow-y: auto;
    background: #00000030;
    padding: 15px;
    border-radius: 8px;
    border: 1px solid #27272a;
}

.cover-item {
    position: relative;
    border-radius: 8px;
    overflow: hidden;
    cursor: pointer;
    border: 2px solid transparent;
    transition: all 0.2s;
    aspect-ratio: 2/3;
    display: flex;
    flex-direction: column;
}

.cover-item img {
    width: 100%;
    height: 100%;
    object-fit: cover;
}

.file-name {
    position: absolute;
    bottom: 0;
    left: 0;
    width: 100%;
    background: rgba(0,0,0,0.7);
    color: #a1a1aa;
    font-size: 0.6rem;
    text-align: center;
    padding: 2px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.cover-item:hover {
    transform: scale(1.05);
    z-index: 1;
}

.cover-item.selected {
    border-color: #fbbf24;
    box-shadow: 0 0 15px rgba(251, 191, 36, 0.3);
}

.selection-overlay {
    position: absolute;
    inset: 0;
    background: rgba(251, 191, 36, 0.2);
    display: none;
    align-items: center;
    justify-content: center;
}

.cover-item.selected .selection-overlay {
    display: flex;
}

.check-icon {
    background: #fbbf24;
    color: black;
    border-radius: 50%;
    width: 24px;
    height: 24px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: bold;
    font-size: 14px;
}

.empty-covers {
    text-align: center;
    padding: 2rem;
    border: 2px dashed #27272a;
    border-radius: 8px;
    color: #52525b;
}
.empty-covers .icon { font-size: 2rem; margin-bottom: 0.5rem; }
.empty-covers code { background: #18181b; padding: 2px 5px; border-radius: 4px; color: #fbbf24; }

/* REAPROVEITANDO ESTILOS GERAIS */
.admin-container {
  max-width: 1000px;
  margin: 2rem auto;
  padding: 0 1.5rem;
  font-family: 'Inter', system-ui, -apple-system, sans-serif;
  color: #e4e4e7;
}

.header-actions {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 2rem;
  background: rgba(24, 24, 27, 0.6);
  padding: 1rem;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
}

.title-section { display: flex; align-items: center; gap: 1rem; }
.icon-wrapper {
  font-size: 2rem;
  background: rgba(251, 191, 36, 0.1);
  width: 50px; height: 50px;
  display: flex; align-items: center; justify-content: center;
  border-radius: 12px;
  border: 1px solid rgba(251, 191, 36, 0.2);
}
.main-title { font-size: 1.25rem; font-weight: 800; color: #fff; margin: 0; line-height: 1.2; }
.subtitle { font-size: 0.85rem; color: #a1a1aa; margin: 0; }

.btn-back {
  display: flex; align-items: center; gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: transparent; border: 1px solid #3f3f46;
  color: #a1a1aa; border-radius: 8px;
  font-size: 0.8rem; font-weight: 600; cursor: pointer;
  transition: all 0.2s;
}
.btn-back:hover { background: #27272a; color: #fff; border-color: #52525b; }

.main-form { display: flex; flex-direction: column; gap: 1.5rem; }
.form-card {
  background: #18181b;
  border: 1px solid #27272a;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
}
.card-header {
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #27272a;
  background: #202024;
  display: flex; align-items: center; gap: 0.75rem;
}
.step-indicator {
  font-size: 0.7rem; font-weight: 900; color: #000;
  background: #fbbf24; padding: 2px 6px; border-radius: 4px;
}
.card-header h3 {
  font-size: 0.9rem; font-weight: 700; color: #f4f4f5; margin: 0;
  text-transform: uppercase; letter-spacing: 0.05em;
}
.card-body { padding: 1.5rem; }

.grid-layout { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1.25rem; }
.full-width { grid-column: span 3; }

.form-group { display: flex; flex-direction: column; gap: 0.4rem; }
.form-group label {
  font-size: 0.75rem; font-weight: 600; color: #a1a1aa; text-transform: uppercase;
}
.highlight-label { color: #fbbf24 !important; }

select { color-scheme: dark; } 
select option { background-color: #18181b; color: #fff; padding: 10px; }

input, select, textarea {
  background: #09090b;
  border: 1px solid #3f3f46;
  color: #f4f4f5;
  padding: 0.65rem 0.75rem;
  border-radius: 6px;
  font-size: 0.85rem;
  outline: none;
  transition: all 0.2s;
  width: 100%;
  font-family: inherit;
  appearance: none;
  -webkit-appearance: none;
}
input:focus, select:focus, textarea:focus {
  border-color: #fbbf24;
  box-shadow: 0 0 0 2px rgba(251, 191, 36, 0.1);
}

::-webkit-calendar-picker-indicator {
    filter: invert(1);
    opacity: 0.6;
    cursor: pointer;
    transition: opacity 0.2s;
}
::-webkit-calendar-picker-indicator:hover {
    opacity: 1;
}

.select-wrapper { position: relative; display: flex; align-items: center; }
.chevron {
  position: absolute; right: 15px; pointer-events: none;
  font-size: 0.7rem; color: #71717a;
  top: 50%; transform: translateY(-50%);
}
.chevron.text-gold { color: #fbbf24; }

.input-group { display: flex; gap: 0.5rem; }

.btn-secondary {
  background: #2563eb; color: white; border: none; padding: 0 1rem;
  border-radius: 6px; font-weight: 600; font-size: 0.8rem; cursor: pointer;
  white-space: nowrap; transition: background 0.2s;
}
.btn-secondary:hover { background: #1d4ed8; }

.input-icon-wrapper { position: relative; }
.input-icon-wrapper input.pl-8 { padding-left: 2.2rem; }
.input-icon-wrapper input.pr-8 { padding-right: 2rem; }
.icon-prefix, .icon-suffix {
  position: absolute; top: 50%; transform: translateY(-50%);
  color: #71717a; font-size: 0.8rem; font-weight: bold;
}
.icon-prefix { left: 0.75rem; }
.icon-suffix { right: 0.75rem; }
.icon-prefix.text-gold { color: #fbbf24; }

/* ESTILO DO CAMPO DOURADO */
.input-gold {
    border-color: #fbbf24 !important;
    color: #fbbf24 !important;
    font-weight: bold;
}

.helper-text { font-size: 0.7rem; color: #52525b; margin-top: 0.2rem; }
.helper-warning { font-size: 0.75rem; color: #fbbf24; margin-top: 0.5rem; }
.optional { font-weight: 400; text-transform: none; color: #52525b; }

.form-footer { margin-top: 1rem; }
.btn-primary {
  width: 100%; padding: 1rem;
  background: linear-gradient(135deg, #fbbf24 0%, #d97706 100%);
  color: #000; border: none; border-radius: 8px;
  font-weight: 800; font-size: 0.95rem; text-transform: uppercase;
  letter-spacing: 0.05em; cursor: pointer;
  transition: transform 0.1s, box-shadow 0.2s;
  display: flex; justify-content: center; align-items: center; gap: 0.5rem;
  box-shadow: 0 10px 15px -3px rgba(251, 191, 36, 0.3);
}
.btn-primary:hover { transform: translateY(-1px); box-shadow: 0 15px 20px -3px rgba(251, 191, 36, 0.4); }
.btn-primary:disabled { opacity: 0.6; cursor: not-allowed; background: #52525b; box-shadow: none; transform: none; }

.loader {
  width: 16px; height: 16px; border: 2px solid #000;
  border-bottom-color: transparent; border-radius: 50%;
  display: inline-block; animation: rotation 1s linear infinite;
}
@keyframes rotation { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }

/* Modal */
.modal-overlay {
  position: fixed; inset: 0; background: rgba(0, 0, 0, 0.85);
  backdrop-filter: blur(4px); z-index: 1000;
  display: flex; align-items: center; justify-content: center;
}
.modal-content {
  background: #09090b; width: 95%; max-width: 900px; height: 85vh;
  border-radius: 12px; border: 1px solid #27272a;
  display: flex; flex-direction: column;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5); overflow: hidden;
}
.modal-header {
  padding: 1rem 1.5rem; border-bottom: 1px solid #27272a;
  display: flex; justify-content: space-between; align-items: center;
  background: #121214;
}
.modal-title { display: flex; align-items: center; gap: 0.5rem; }
.modal-title .icon { font-size: 1.2rem; }
.modal-title h3 { margin: 0; font-size: 1rem; color: #fff; font-weight: 700; }
.btn-close { background: none; border: none; color: #52525b; font-size: 1.2rem; cursor: pointer; }
.btn-close:hover { color: #fff; }

.modal-body { flex: 1; padding: 1.5rem; overflow-y: auto; background: #09090b; }

.sports-container { margin-bottom: 1.5rem; }
.section-label { font-size: 0.75rem; font-weight: 700; color: #71717a; text-transform: uppercase; display: block; margin-bottom: 0.5rem; }
.sports-scroll { display: flex; gap: 0.75rem; overflow-x: auto; padding-bottom: 0.5rem; }
.sport-card {
  min-width: 110px; background: #18181b; border: 1px solid #27272a;
  border-radius: 8px; padding: 0.75rem; cursor: pointer;
  transition: all 0.2s; display: flex; flex-direction: column;
  justify-content: space-between; height: 70px;
}
.sport-card:hover { border-color: #52525b; background: #202024; }
.sport-card.active { border-color: #3b82f6; background: rgba(59, 130, 246, 0.1); }
.sport-header { display: flex; justify-content: space-between; }
.custom-checkbox { accent-color: #fbbf24; width: 14px; height: 14px; cursor: pointer; }
.sport-name { font-size: 0.75rem; font-weight: 600; color: #e4e4e7; margin-top: auto; }

.leagues-panel {
  background: #121214; border: 1px solid #27272a; border-radius: 8px;
  display: flex; flex-direction: column; overflow: hidden; min-height: 300px;
}
.leagues-header {
  padding: 0.75rem; background: #18181b; border-bottom: 1px solid #27272a;
  display: flex; justify-content: space-between; align-items: center;
}
.badge { background: #27272a; color: #a1a1aa; font-size: 0.65rem; padding: 1px 6px; border-radius: 4px; margin-left: 0.5rem; }
.btn-link { background: none; border: none; color: #3b82f6; font-size: 0.7rem; font-weight: 600; cursor: pointer; margin-left: 1rem; }
.btn-link:hover { text-decoration: underline; }

.search-box { position: relative; width: 200px; }
.search-box input { padding-left: 2rem; font-size: 0.8rem; background: #09090b; }
.search-icon { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); font-size: 0.8rem; color: #52525b; }

.leagues-list { flex: 1; overflow-y: auto; padding: 0.5rem; }
.empty-state { padding: 2rem; text-align: center; font-size: 0.8rem; color: #52525b; font-style: italic; }

.league-item {
  border: 1px solid #27272a; border-radius: 6px; margin-bottom: 0.5rem;
  background: #18181b; overflow: hidden; transition: opacity 0.2s;
}
.league-item.inactive { opacity: 0.6; border-color: #ef444430; background: #2f151520; }

.league-row {
  display: flex; justify-content: space-between; align-items: center;
  padding: 0.6rem 0.75rem; cursor: pointer; transition: background 0.2s;
}
.league-row:hover { background: #202024; }
.lr-content { display: flex; align-items: center; gap: 0.75rem; flex: 1; }
.arrow { font-size: 0.6rem; color: #52525b; transition: transform 0.2s; }
.arrow.rotated { transform: rotate(90deg); }
.league-name { font-size: 0.85rem; font-weight: 600; color: #e4e4e7; }
.league-name.highlight { color: #fbbf24; }
.count-badge { font-size: 0.7rem; background: #27272a; color: #a1a1aa; padding: 1px 5px; border-radius: 4px; }

.switch { position: relative; display: inline-block; width: 34px; height: 18px; }
.switch input { opacity: 0; width: 0; height: 0; }
.slider {
  position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0;
  background-color: #3f3f46; transition: .4s; border-radius: 34px;
}
.slider:before {
  position: absolute; content: ""; height: 16px; width: 16px; left: 2px; bottom: 2px;
  background-color: white; transition: .4s; border-radius: 50%;
}
input:checked + .slider { background-color: #10b981; }
input:checked + .slider:before { transform: translateX(16px); }

.teams-grid {
  padding: 0.75rem; background: #00000030; border-top: 1px solid #27272a;
  display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 0.75rem;
}
.empty-teams { font-size: 0.8rem; color: #52525b; font-style: italic; }
.team-chip {
  background: #27272a; padding: 0.5rem 0.75rem; border-radius: 6px; border: 1px solid transparent;
  display: flex; justify-content: space-between; align-items: center;
  cursor: pointer; transition: all 0.2s; user-select: none;
}
.team-chip:hover { background: #3f3f46; color: #fff; }
.team-chip.selected { background: rgba(37, 99, 235, 0.15); border-color: #2563eb; }
.team-chip.selected .tname { color: #60a5fa; font-weight: 600; }
.tname { font-size: 0.8rem; color: #a1a1aa; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 85%; }
.tname.highlight { text-decoration: underline; text-decoration-color: #fbbf24; }
.status-indicator { width: 6px; height: 6px; border-radius: 50%; background: #52525b; }
.status-indicator.on { background: #10b981; box-shadow: 0 0 4px #10b981; }

.modal-footer {
  padding: 1rem 1.5rem; background: #18181b; border-top: 1px solid #27272a;
  display: flex; justify-content: space-between; align-items: center;
}
.mf-info { font-size: 0.85rem; color: #a1a1aa; }
.mf-info strong { color: #fff; }
.mf-actions { display: flex; gap: 0.75rem; }
.btn-ghost {
  background: transparent; color: #a1a1aa; border: 1px solid #3f3f46;
  padding: 0.6rem 1.2rem; border-radius: 6px; font-weight: 600; cursor: pointer; transition: 0.2s;
}
.btn-ghost:hover { border-color: #52525b; color: #fff; }
.btn-primary.small { width: auto; font-size: 0.85rem; padding: 0.6rem 1.5rem; box-shadow: none; }

.custom-scrollbar::-webkit-scrollbar { width: 6px; height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #52525b; }

.animate-scale-in { animation: scaleIn 0.25s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes scaleIn { from { transform: scale(0.95); opacity: 0; } to { transform: scale(1); opacity: 1; } }

.animate-fade-in { animation: fadeIn 0.4s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }

@media (max-width: 768px) {
  .grid-3, .grid-2 { grid-template-columns: 1fr; }
  .full-width { grid-column: span 1; }
  .modal-panel { width: 100%; height: 100%; border-radius: 0; }
  .teams-grid { grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); }
}
</style>