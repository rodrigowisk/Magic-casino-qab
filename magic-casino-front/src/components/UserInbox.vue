<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import axios from 'axios';
import { useAuthStore } from '../stores/useAuthStore';
import { 
  ArrowLeft, 
  Trash2, 
  Mail, 
  Inbox, 
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  X
} from 'lucide-vue-next';

interface Message {
  id: number;
  subject: string;
  body: string;
  isRead: boolean;
  createdAt: string;
}

const router = useRouter();
const authStore = useAuthStore();
const messages = ref<Message[]>([]);
const loading = ref(false);
const selectedMessage = ref<Message | null>(null);
const showDeleteModal = ref(false);
const msgToDeleteId = ref<number | null>(null);

const getAuthHeader = () => ({ headers: { Authorization: `Bearer ${authStore.token}` } });

const fetchMessages = async () => {
  if (!authStore.token) return;
  loading.value = true;
  try {
    const res = await axios.get('/api/messages/inbox', getAuthHeader());
    messages.value = res.data;
  } catch (error) {
    console.error("Erro ao buscar mensagens", error);
  } finally {
    loading.value = false;
  }
};

const selectMessage = async (msg: Message) => {
  selectedMessage.value = msg;
  if (!msg.isRead) {
    msg.isRead = true;
    try {
      await axios.put(`/api/messages/${msg.id}/read`, {}, getAuthHeader());
    } catch (error) {
      console.error("Erro ao marcar como lida", error);
    }
  }
};

const openDeleteModal = (id: number) => {
  msgToDeleteId.value = id;
  showDeleteModal.value = true;
};

const closeDeleteModal = () => {
  showDeleteModal.value = false;
  msgToDeleteId.value = null;
};

const confirmDelete = async () => {
  if (msgToDeleteId.value === null) return;
  const id = msgToDeleteId.value;
  messages.value = messages.value.filter((m) => m.id !== id);
  if (selectedMessage.value?.id === id) selectedMessage.value = null;
  closeDeleteModal();
  try {
    await axios.delete(`/api/messages/${id}`, getAuthHeader());
  } catch (error) {
    console.error("Erro ao apagar", error);
    fetchMessages();
  }
};

const goBack = () => router.back();

onMounted(fetchMessages);
</script>

<template>
  <div class="page-container">
    
    <div class="header-section">
      <button @click="goBack" class="back-btn-circle">
        <ArrowLeft class="w-5 h-5" />
      </button>
      <div>
        <h1 class="page-title">Mensagens</h1>
        <p class="page-subtitle">Central de notificações</p>
      </div>
    </div>

    <div class="main-card">
      
      <div class="sidebar">
        <div class="sidebar-header">
  
          <button @click="fetchMessages" class="refresh-btn">
            <RefreshCw class="w-4 h-4" :class="{ 'animate-spin': loading }" />
          </button>
        </div>

        <div class="message-list custom-scroll">
          <div v-if="loading" class="state-container">
            <span class="text-sm">Carregando...</span>
          </div>
          <div v-else-if="messages.length === 0" class="state-container">
            <Inbox class="w-8 h-8 text-slate-600 mb-2" />
            <span class="text-sm">Nenhuma mensagem</span>
          </div>

          <div 
            v-else
            v-for="msg in messages" 
            :key="msg.id" 
            class="list-item"
            :class="{ 'active': selectedMessage?.id === msg.id, 'unread': !msg.isRead }"
            @click="selectMessage(msg)"
          >
            <div class="item-header">
              <span class="sender">Suporte</span>
              <span class="date">{{ new Date(msg.createdAt).toLocaleDateString(undefined, { day: '2-digit', month: '2-digit' }) }}</span>
            </div>
            <div class="item-subject">
              <span v-if="!msg.isRead" class="status-dot"></span>
              {{ msg.subject }}
            </div>
            <div class="item-preview mobile-hide-preview">{{ msg.body }}</div>
          </div>
        </div>
      </div>

      <div class="content-pane">
        <div v-if="!selectedMessage" class="empty-selection">
          <div class="icon-circle">
            <Mail class="w-8 h-8 text-slate-400" />
          </div>
          <h3>Selecione uma mensagem</h3>
        </div>

        <div v-else class="message-view">
          <div class="view-header">
            <div class="view-meta">
              <h2>{{ selectedMessage.subject }}</h2>
              <div class="meta-row">
                <span class="badge-sender">Sistema</span>
                <span class="meta-date">{{ new Date(selectedMessage.createdAt).toLocaleString() }}</span>
              </div>
            </div>
            <button @click="openDeleteModal(selectedMessage.id)" class="action-btn-danger">
              <Trash2 class="w-4 h-4" />
            </button>
          </div>

          <div class="view-body custom-scroll">
            {{ selectedMessage.body }}
          </div>
          
          <div class="view-footer">
            <CheckCircle2 class="w-4 h-4 text-emerald-500" />
            <span>Verificado</span>
          </div>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <Transition name="fade">
        <div v-if="showDeleteModal" class="modal-backdrop" @click.self="closeDeleteModal">
          <div class="modal-content">
            <div class="modal-icon-header">
              <div class="icon-danger-bg"><AlertTriangle class="w-6 h-6 text-red-500" /></div>
              <button @click="closeDeleteModal" class="close-modal-btn"><X class="w-5 h-5" /></button>
            </div>
            <div class="modal-body">
              <h3>Excluir mensagem?</h3>
              <p>Esta ação não pode ser desfeita.</p>
            </div>
            <div class="modal-actions">
              <button @click="closeDeleteModal" class="btn-cancel">Cancelar</button>
              <button @click="confirmDelete" class="btn-confirm-delete">Excluir</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

  </div>
</template>

<style scoped>
/* ==========================================================================
   DESKTOP (COMPACTO)
   ========================================================================== */

.page-container {
  width: 100%; 
  max-width: 1200px; 
  margin: 40px auto;
  padding: 0 20px;
  font-family: 'Inter', sans-serif;
  display: flex;
  flex-direction: column;
}

.header-section { display: flex; align-items: center; gap: 15px; margin-bottom: 20px; }
.back-btn-circle { width: 36px; height: 36px; border-radius: 50%; background-color: #1e293b; border: 1px solid #334155; color: #94a3b8; display: flex; align-items: center; justify-content: center; cursor: pointer; transition: 0.2s; }
.back-btn-circle:hover { background-color: #334155; color: #fff; }
.page-title { font-size: 1.4rem; font-weight: 700; color: #f8fafc; margin: 0; }
.page-subtitle { color: #64748b; font-size: 0.85rem; margin: 2px 0 0 0; }

/* CARD PRINCIPAL (Altura Reduzida: 500px) */
.main-card { 
  background-color: #1e293b; 
  border: 1px solid #334155; 
  border-radius: 12px; 
  box-shadow: 0 4px 20px rgba(0,0,0,0.2); 
  display: flex; 
  width: 100%; 
  height: 500px; /* <--- Mais compacto */
  overflow: hidden; 
}

/* SIDEBAR */
.sidebar { 
  width: 360px; 
  min-width: 360px; 
  background-color: #1a2436; 
  border-right: 1px solid #334155; 
  display: flex; 
  flex-direction: column; 
}

.sidebar-header { padding: 12px 16px; border-bottom: 1px solid #334155; display: flex; gap: 10px; align-items: center; flex-shrink: 0; }
.refresh-btn { background: transparent; border: 1px solid #334155; color: #94a3b8; width: 32px; height: 32px; border-radius: 6px; display: flex; align-items: center; justify-content: center; cursor: pointer; }
.message-list { flex: 1; overflow-y: auto; }
.state-container { padding: 30px; display: flex; flex-direction: column; align-items: center; justify-content: center; color: #64748b; text-align: center; }

/* List Items (Mais denso) */
.list-item { 
  padding: 12px 16px; /* <--- Padding reduzido */
  border-bottom: 1px solid #28364a; 
  cursor: pointer; 
  transition: 0.2s; 
}
.list-item:hover { background-color: #243042; }
.list-item.active { background-color: #263345; border-left: 3px solid #10b981; }
.item-header { display: flex; justify-content: space-between; font-size: 0.7rem; color: #64748b; margin-bottom: 4px; font-weight: 600; text-transform: uppercase; }
.item-subject { font-size: 0.9rem; color: #e2e8f0; margin-bottom: 2px; font-weight: 500; display: flex; align-items: center; gap: 6px; }
.status-dot { width: 6px; height: 6px; background-color: #10b981; border-radius: 50%; min-width: 6px; }
.list-item.unread .item-subject { color: #fff; font-weight: 700; }
.item-preview { font-size: 0.8rem; color: #94a3b8; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* CONTENT PANE */
.content-pane { flex: 1; background-color: #1e293b; display: flex; flex-direction: column; overflow: hidden; min-width: 0; }
.empty-selection { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; color: #64748b; }
.icon-circle { width: 60px; height: 60px; background-color: #161f2e; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin-bottom: 15px; border: 1px solid #334155; }

.message-view { display: flex; flex-direction: column; height: 100%; }
.view-header { padding: 16px 24px; border-bottom: 1px solid #334155; display: flex; justify-content: space-between; align-items: flex-start; gap: 15px; flex-shrink: 0; }
.view-meta { flex: 1; }
.view-meta h2 { font-size: 1.2rem; color: #fff; margin: 0 0 6px 0; font-weight: 700; line-height: 1.2; }
.meta-row { display: flex; align-items: center; gap: 10px; }
.badge-sender { background-color: #0f172a; border: 1px solid #334155; color: #10b981; padding: 2px 6px; border-radius: 4px; font-size: 0.7rem; font-weight: 700; text-transform: uppercase; }
.meta-date { color: #64748b; font-size: 0.8rem; }
.action-btn-danger { background: transparent; border: 1px solid #334155; color: #94a3b8; padding: 6px; border-radius: 6px; cursor: pointer; display: flex; align-items: center; justify-content: center; width: 32px; height: 32px; }
.action-btn-danger:hover { background-color: rgba(239, 68, 68, 0.1); border-color: #ef4444; color: #ef4444; }

.view-body { flex: 1; padding: 24px; color: #cbd5e1; line-height: 1.6; font-size: 0.95rem; white-space: pre-wrap; overflow-y: auto; }
.view-footer { padding: 12px 24px; border-top: 1px solid #334155; background-color: #1a2436; font-size: 0.75rem; color: #64748b; display: flex; align-items: center; gap: 8px; flex-shrink: 0; }

/* Scrollbar */
.custom-scroll::-webkit-scrollbar { width: 5px; }
.custom-scroll::-webkit-scrollbar-track { background: transparent; }
.custom-scroll::-webkit-scrollbar-thumb { background: #334155; border-radius: 10px; }

/* Modal */
.modal-backdrop { position: fixed; top: 0; left: 0; width: 100vw; height: 100vh; background-color: rgba(0, 0, 0, 0.75); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 9999; }
.modal-content { background-color: #1e293b; border: 1px solid #334155; width: 90%; max-width: 380px; border-radius: 12px; padding: 20px; box-shadow: 0 10px 40px rgba(0,0,0,0.5); }
.modal-icon-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px; }
.icon-danger-bg { width: 42px; height: 42px; background-color: rgba(239, 68, 68, 0.1); border-radius: 10px; display: flex; align-items: center; justify-content: center; }
.close-modal-btn { background: transparent; border: none; color: #64748b; cursor: pointer; }
.modal-body h3 { margin: 0 0 6px 0; color: #f8fafc; font-size: 1.05rem; }
.modal-body p { margin: 0 0 20px 0; color: #94a3b8; font-size: 0.85rem; }
.modal-actions { display: flex; gap: 10px; }
.btn-cancel { flex: 1; padding: 8px; border-radius: 6px; border: 1px solid #334155; background: transparent; color: #e2e8f0; font-size: 0.9rem; }
.btn-confirm-delete { flex: 1; padding: 8px; border-radius: 6px; border: none; background: #ef4444; color: #fff; font-size: 0.9rem; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* ==========================================================================
   MOBILE (Vertical Split Compacto)
   ========================================================================== */
@media (max-width: 768px) {
  
  .page-container {
    padding: 0;
    margin: 0;
    height: 100dvh;
    max-width: 100%;
  }

  .header-section {
    padding: 8px 15px;
    margin-bottom: 0;
    border-bottom: 1px solid #334155;
    background-color: #1e293b;
  }
  .page-title { font-size: 1.1rem; }
  .page-subtitle { display: none; }
  .back-btn-circle { width: 32px; height: 32px; }

  .main-card {
    flex-direction: column;
    border: none;
    border-radius: 0;
    height: auto;
    flex: 1;
  }

  /* LISTA: Um pouco menor para dar mais espaço à leitura */
  .sidebar {
    width: 100%;
    min-width: 0;
    height: 35vh; /* <--- Altura reduzida mobile */
    min-height: 200px;
    border-right: none;
    border-bottom: 1px solid #334155;
    background-color: #0f172a;
  }
  .sidebar-header { padding: 10px; }
  .mobile-hide-preview { display: none; }
  .list-item { padding: 10px 14px; } /* Padding mobile menor */

  /* LEITURA */
  .content-pane {
    width: 100%;
    flex: 1; 
    background-color: #1e293b;
  }

  .view-header { padding: 12px 16px; }
  .view-meta h2 { font-size: 1.1rem; }
  .view-body { padding: 12px 16px; font-size: 0.9rem; }
}
</style>