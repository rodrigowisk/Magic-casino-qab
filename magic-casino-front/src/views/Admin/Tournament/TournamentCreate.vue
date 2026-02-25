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
                
                <div class="flex flex-wrap gap-2 bg-[#0f172a] p-1.5 rounded-lg border border-white/10 w-fit mb-3">
                    <button 
                        v-for="tab in coverTabs" 
                        :key="tab.id"
                        type="button"
                        @click="activeCoverTab = tab.id"
                        :class="[
                            'px-4 py-1.5 rounded-md font-bold text-xs uppercase tracking-wider transition-all duration-200',
                            activeCoverTab === tab.id 
                                ? 'bg-blue-600 text-white shadow-[0_0_15px_rgba(37,99,235,0.4)]' 
                                : 'bg-transparent text-slate-400 hover:text-white hover:bg-white/5'
                        ]"
                    >
                        {{ tab.label }}
                    </button>
                </div>
                
                <div v-if="hasImages" class="covers-grid custom-scrollbar">
                    <div v-if="filteredCovers.length === 0" class="col-span-full py-6 text-center text-slate-500 font-medium w-full" style="grid-column: 1 / -1;">
                        Nenhuma capa nesta categoria.
                    </div>

                    <div 
                        v-for="cover in filteredCovers" 
                        :key="cover.id"
                        @click="selectCover(cover.id)"
                        class="cover-item"
                        :class="{ 'selected': form.coverImage === cover.id }"
                    >
                        <img :src="cover.url" loading="lazy" />
                        <div class="selection-overlay">
                            <span class="check-icon">✔</span>
                        </div>
                        <span class="file-name">{{ cover.fileName }}</span>
                    </div>
                </div>

                <div v-else class="empty-covers">
                    <div class="icon">📁</div>
                    <p>Nenhuma imagem encontrada.</p>
                    <small>Adicione imagens nas subpastas em: <br> <code>src/assets/tournament_covers/</code></small>
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
                <button type="button" @click="showModal = true" class="btn-secondary">
                  <span>+</span> Novo
                </button>
                <button v-if="selectedTemplateId" type="button" @click="deleteSelectedTemplate" class="btn-danger flex items-center justify-center px-3 rounded-md transition-all" title="Excluir Modelo Selecionado">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                  </svg>
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
                        <input type="checkbox" v-model="isUnlimitedParticipants" class="custom-checkbox" />
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
              <input v-model.number="form.initialFantasyBalance" type="number" step="1" min="100" placeholder="100000" required />
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

    <TemplateCreationModal 
      v-if="showModal" 
      @close="showModal = false" 
      @saved="handleTemplateSaved" 
    />

  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import tournamentService from "../../../services/Tournament/TournamentService";
import tournamentTemplateService, { type TournamentTemplate } from "../../../services/Tournament/TournamentTemplateService";
import Swal from 'sweetalert2';

import TemplateCreationModal from './TemplateCreationModal.vue';

const coversModules = import.meta.glob('/src/assets/tournament_covers/**/*.{png,jpg,jpeg,svg,webp}', { eager: true });

export default defineComponent({
  name: 'TournamentCreate',
  components: {
    TemplateCreationModal
  },
  data() {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60000;
    
    const localNow = new Date(now.getTime() - offset);
    const defaultStartDate = localNow.toISOString().slice(0, 16);
    
    const localTomorrow = new Date(now.getTime() + (24 * 60 * 60 * 1000) - offset);
    const defaultEndDate = localTomorrow.toISOString().slice(0, 16);

    return {
      isLoading: false,
      selectedTemplateId: null as number | null,
      templates: [] as TournamentTemplate[],
      
      existingCategories: ['Destaques', 'Todo os torneios', 'Torneios turbo', 'Top da semana'],
      isCreatingCategory: false,

      coverTabs: [
          { id: 'soccer', label: 'Futebol' },
          { id: 'basketball', label: 'Basquete' },
          { id: 'tenis', label: 'Tênis' },
          { id: 'mix', label: 'Misto' }
      ],
      activeCoverTab: 'soccer',
      allCovers: [] as Array<{ id: string; fileName: string; url: string; category: string }>,

      prizeType: 'dynamic', 
      isUnlimitedParticipants: true, 

      prizeOptions: [
        { id: "PREMIO_1", name: "🥇 Clássico Top 3 (50%, 30%, 20%)" },
        { id: "PREMIO_2", name: "🥈 Estendido Top 5 (1º 40%|2º 25%|3º 15%|4º 12%|5º 8%)" },
        { id: "PREMIO_3", name: "🥉 Double Up (50% Ganha / 50% Perde)" },
        { id: "WINNER_TAKES_ALL", name: "🏆 Vencedor Leva Tudo (100%)" }
      ],

      form: {
        name: '', 
        description: '', 
        entryFee: 50.00, 
        initialFantasyBalance: 100000, 
        startDate: defaultStartDate, 
        endDate: defaultEndDate, 
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
    };
  },
  computed: {
    filteredCovers(): Array<{ id: string; fileName: string; url: string; category: string }> {
        return this.allCovers.filter(c => c.category === this.activeCoverTab);
    },
    hasImages(): boolean {
        return this.allCovers.length > 0;
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
        const covers: Array<{ id: string; fileName: string; url: string; category: string }> = [];
        
        if (coversModules) {
            for (const path in coversModules) {
                const mod = coversModules[path] as any;
                const parts = path.split('/');
                const fileName = parts.pop() || 'unknown';
                
                let category = parts.pop() || 'mix'; 
                if (category === 'tournament_covers') {
                    category = 'mix'; 
                }

                const uniqueId = `${category}/${fileName}`;
                covers.push({ id: uniqueId, fileName, url: mod.default, category });
            }
        }
        
        this.allCovers = covers;
        
        if (covers.length > 0 && !this.form.coverImage) {
            const first = this.filteredCovers[0] || covers[0];
            if(first) this.form.coverImage = first.id;
        }
    },
    selectCover(id: string) {
        this.form.coverImage = id;
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
    async deleteSelectedTemplate() {
        if (!this.selectedTemplateId) return;

        const template = this.templates.find(t => (t as any).id === this.selectedTemplateId || (t as any).Id === this.selectedTemplateId);
        const templateName = template ? ((template as any).name || (template as any).Name) : 'este modelo';

        const result = await Swal.fire({
            title: 'Excluir Modelo?',
            text: `Tem certeza que deseja excluir "${templateName}"? Esta ação não pode ser desfeita e pode afetar torneios não finalizados vinculados a ele.`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#ef4444',
            cancelButtonColor: '#3f3f46',
            confirmButtonText: 'Sim, excluir',
            cancelButtonText: 'Cancelar',
            background: '#18181b',
            color: '#fff'
        });

        if (result.isConfirmed) {
            try {
                const service = tournamentTemplateService as any;
                if (typeof service.delete === 'function') {
                    await service.delete(this.selectedTemplateId);
                } else if (typeof service.remove === 'function') {
                    await service.remove(this.selectedTemplateId);
                } else if (typeof service.destroy === 'function') {
                    await service.destroy(this.selectedTemplateId);
                } else {
                    console.warn("Nenhum método de deleção padrão encontrado em tournamentTemplateService. O item será removido apenas da tela.");
                }

                this.templates = this.templates.filter(t => (t as any).id !== this.selectedTemplateId && (t as any).Id !== this.selectedTemplateId);
                this.selectedTemplateId = null;

                Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: 'Modelo excluído com sucesso!', background: '#18181b', color: '#fff', showConfirmButton: false, timer: 2000 });
            } catch (error) {
                console.error("Erro ao excluir modelo:", error);
                Swal.fire({ title: 'Erro', text: 'Não foi possível excluir o modelo.', icon: 'error', background: '#18181b', color: '#fff' });
            }
        }
    },
    handleTemplateSaved(newTemplate: any) {
        if (newTemplate) {
            this.templates.push(newTemplate);
            this.selectedTemplateId = newTemplate.id || null;
        }
    },
    async submitTournament() {
      const template = this.templates.find(t => (t as any).id === this.selectedTemplateId || (t as any).Id === this.selectedTemplateId);
      if (!template) {
          Swal.fire({ title: 'Atenção', text: 'Selecione um Modelo de Jogos válido.', icon: 'warning', background: '#1e293b', color: '#fff' });
          return;
      }

      let derivedSport = this.form.sport; 
      try {
          if ((template as any).filterRules) {
              const rulesStr = (template as any).filterRules;
              const rules = typeof rulesStr === 'string' ? JSON.parse(rulesStr) : rulesStr;
              
              if (rules && rules.sports && Array.isArray(rules.sports)) {
                  if (rules.sports.length > 1) {
                      derivedSport = 'Misto';
                  } else if (rules.sports.length === 1) {
                      const k = (rules.sports[0].key || '').toLowerCase();
                      if (k === 'soccer') derivedSport = 'Futebol';
                      else if (k === 'basketball') derivedSport = 'Basquete';
                      else if (k === 'tennis') derivedSport = 'Tênis';
                      else derivedSport = k.charAt(0).toUpperCase() + k.slice(1);
                  }
              }
          }
      } catch (e) {
          console.error("Erro ao ler regras para definir esporte", e);
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
            sport: derivedSport, 
            isActive: this.form.isActive,
            isFinished: false,
            filterRules: (template as any)?.filterRules || '[]',
            prizePool: 0,
            participantsCount: 0,
            isJoined: false,
            prizeRuleId: this.form.prizeRuleId,
            category: this.form.category,
            coverImage: this.form.coverImage,
            houseFeePercent: this.prizeType === 'fixed' ? 0 : this.form.houseFeePercent,
            fixedPrize: this.prizeType === 'fixed' ? this.form.fixedPrize : null,
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
.category-wrapper { display: flex; flex-direction: column; gap: 0.5rem; }
.select-mode, .create-mode { display: flex; gap: 0.5rem; align-items: center; }
.btn-create-cat { background: #3b82f6; color: white; border: none; padding: 0.7rem 1rem; border-radius: 6px; font-weight: 700; cursor: pointer; white-space: nowrap; transition: 0.2s; }
.btn-create-cat:hover { background: #2563eb; }
.btn-cancel-cat { background: #ef4444; color: white; border: none; padding: 0.7rem 1rem; border-radius: 6px; font-weight: 700; cursor: pointer; white-space: nowrap; }
.input-highlight { border-color: #fbbf24 !important; background: #18181b; color: #fff; font-weight: bold; }

/* ESTILOS DA GRADE DE CAPAS */
.covers-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(100px, 1fr)); gap: 1rem; max-height: 250px; overflow-y: auto; background: #00000030; padding: 15px; border-radius: 8px; border: 1px solid #27272a; }
.cover-item { position: relative; border-radius: 8px; overflow: hidden; cursor: pointer; border: 2px solid transparent; transition: all 0.2s; aspect-ratio: 2/3; display: flex; flex-direction: column; }
.cover-item img { width: 100%; height: 100%; object-fit: cover; }
.file-name { position: absolute; bottom: 0; left: 0; width: 100%; background: rgba(0,0,0,0.7); color: #a1a1aa; font-size: 0.6rem; text-align: center; padding: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.cover-item:hover { transform: scale(1.05); z-index: 1; }
.cover-item.selected { border-color: #fbbf24; box-shadow: 0 0 15px rgba(251, 191, 36, 0.3); }
.selection-overlay { position: absolute; inset: 0; background: rgba(251, 191, 36, 0.2); display: none; align-items: center; justify-content: center; }
.cover-item.selected .selection-overlay { display: flex; }
.check-icon { background: #fbbf24; color: black; border-radius: 50%; width: 24px; height: 24px; display: flex; align-items: center; justify-content: center; font-weight: bold; font-size: 14px; }
.empty-covers { text-align: center; padding: 2rem; border: 2px dashed #27272a; border-radius: 8px; color: #52525b; }
.empty-covers .icon { font-size: 2rem; margin-bottom: 0.5rem; }
.empty-covers code { background: #18181b; padding: 2px 5px; border-radius: 4px; color: #fbbf24; }

/* REAPROVEITANDO ESTILOS GERAIS */
.admin-container { max-width: 1000px; margin: 2rem auto; padding: 0 1.5rem; font-family: 'Inter', system-ui, -apple-system, sans-serif; color: #e4e4e7; }
.header-actions { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 2rem; background: rgba(24, 24, 27, 0.6); padding: 1rem; border-radius: 12px; border: 1px solid rgba(255, 255, 255, 0.05); backdrop-filter: blur(10px); }
.title-section { display: flex; align-items: center; gap: 1rem; }
.icon-wrapper { font-size: 2rem; background: rgba(251, 191, 36, 0.1); width: 50px; height: 50px; display: flex; align-items: center; justify-content: center; border-radius: 12px; border: 1px solid rgba(251, 191, 36, 0.2); }
.main-title { font-size: 1.25rem; font-weight: 800; color: #fff; margin: 0; line-height: 1.2; }
.subtitle { font-size: 0.85rem; color: #a1a1aa; margin: 0; }
.btn-back { display: flex; align-items: center; gap: 0.5rem; padding: 0.5rem 1rem; background: transparent; border: 1px solid #3f3f46; color: #a1a1aa; border-radius: 8px; font-size: 0.8rem; font-weight: 600; cursor: pointer; transition: all 0.2s; }
.btn-back:hover { background: #27272a; color: #fff; border-color: #52525b; }

.main-form { display: flex; flex-direction: column; gap: 1.5rem; }
.form-card { background: #18181b; border: 1px solid #27272a; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }
.card-header { padding: 1rem 1.5rem; border-bottom: 1px solid #27272a; background: #202024; display: flex; align-items: center; gap: 0.75rem; }
.step-indicator { font-size: 0.7rem; font-weight: 900; color: #000; background: #fbbf24; padding: 2px 6px; border-radius: 4px; }
.card-header h3 { font-size: 0.9rem; font-weight: 700; color: #f4f4f5; margin: 0; text-transform: uppercase; letter-spacing: 0.05em; }
.card-body { padding: 1.5rem; }
.grid-layout { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 1.25rem; }
.full-width { grid-column: span 3; }

.form-group { display: flex; flex-direction: column; gap: 0.4rem; }
.form-group label { font-size: 0.75rem; font-weight: 600; color: #a1a1aa; text-transform: uppercase; }
.highlight-label { color: #fbbf24 !important; }

select { color-scheme: dark; } 
select option { background-color: #18181b; color: #fff; padding: 10px; }
input, select, textarea { background: #09090b; border: 1px solid #3f3f46; color: #f4f4f5; padding: 0.65rem 0.75rem; border-radius: 6px; font-size: 0.85rem; outline: none; transition: all 0.2s; width: 100%; font-family: inherit; appearance: none; -webkit-appearance: none; }
input:focus, select:focus, textarea:focus { border-color: #fbbf24; box-shadow: 0 0 0 2px rgba(251, 191, 36, 0.1); }
::-webkit-calendar-picker-indicator { filter: invert(1); opacity: 0.6; cursor: pointer; transition: opacity 0.2s; }
::-webkit-calendar-picker-indicator:hover { opacity: 1; }

.select-wrapper { position: relative; display: flex; align-items: center; }
.chevron { position: absolute; right: 15px; pointer-events: none; font-size: 0.7rem; color: #71717a; top: 50%; transform: translateY(-50%); }
.chevron.text-gold { color: #fbbf24; }
.input-group { display: flex; gap: 0.5rem; }

.btn-secondary { background: #2563eb; color: white; border: none; padding: 0 1rem; border-radius: 6px; font-weight: 600; font-size: 0.8rem; cursor: pointer; white-space: nowrap; transition: background 0.2s; }
.btn-secondary:hover { background: #1d4ed8; }

.btn-danger { background: #ef4444; color: white; border: none; cursor: pointer; min-width: 40px; }
.btn-danger:hover { background: #dc2626; }

.input-icon-wrapper { position: relative; }
.input-icon-wrapper input.pl-8 { padding-left: 2.2rem; }
.input-icon-wrapper input.pr-8 { padding-right: 2rem; }
.icon-prefix, .icon-suffix { position: absolute; top: 50%; transform: translateY(-50%); color: #71717a; font-size: 0.8rem; font-weight: bold; }
.icon-prefix { left: 0.75rem; }
.icon-suffix { right: 0.75rem; }
.icon-prefix.text-gold { color: #fbbf24; }

.input-gold { border-color: #fbbf24 !important; color: #fbbf24 !important; font-weight: bold; }

/* 🔥 CHECKBOX CUSTOMIZADO (IGUAL À IMAGEM DO TEMPLATE) 🔥 */
.custom-checkbox { 
    appearance: none;
    -webkit-appearance: none;
    width: 22px; 
    height: 22px; 
    background-color: #18181b;
    border: 2px solid #3f3f46;
    border-radius: 4px;
    cursor: pointer;
    position: relative;
    transition: all 0.2s ease-in-out;
    outline: none;
    display: inline-block;
    margin: 0;
}
.custom-checkbox:hover {
    border-color: #52525b;
}
.custom-checkbox:checked {
    background-color: #fbbf24; /* Amarelo dourado */
    border-color: #fbbf24;
    box-shadow: 0 0 12px rgba(251, 191, 36, 0.5); /* Glow igual da imagem */
}
.custom-checkbox:checked::after {
    content: '';
    position: absolute;
    left: 6.5px;
    top: 2.5px;
    width: 5px;
    height: 11px;
    border: solid #000; /* V em preto */
    border-width: 0 2.5px 2.5px 0;
    transform: rotate(45deg);
}

.helper-text { font-size: 0.7rem; color: #52525b; margin-top: 0.2rem; }
.helper-warning { font-size: 0.75rem; color: #fbbf24; margin-top: 0.5rem; }

.form-footer { margin-top: 1rem; }
.btn-primary { width: 100%; padding: 1rem; background: linear-gradient(135deg, #fbbf24 0%, #d97706 100%); color: #000; border: none; border-radius: 8px; font-weight: 800; font-size: 0.95rem; text-transform: uppercase; letter-spacing: 0.05em; cursor: pointer; transition: transform 0.1s, box-shadow 0.2s; display: flex; justify-content: center; align-items: center; gap: 0.5rem; box-shadow: 0 10px 15px -3px rgba(251, 191, 36, 0.3); }
.btn-primary:hover { transform: translateY(-1px); box-shadow: 0 15px 20px -3px rgba(251, 191, 36, 0.4); }
.btn-primary:disabled { opacity: 0.6; cursor: not-allowed; background: #52525b; box-shadow: none; transform: none; }

.loader { width: 16px; height: 16px; border: 2px solid #000; border-bottom-color: transparent; border-radius: 50%; display: inline-block; animation: rotation 1s linear infinite; }
@keyframes rotation { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
.animate-fade-in { animation: fadeIn 0.4s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }

@media (max-width: 768px) {
  .grid-layout { grid-template-columns: 1fr; }
  .full-width { grid-column: span 1; }
}
</style>