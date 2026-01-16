<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useConfigStore } from '../stores/useConfigStore';
import Swal from 'sweetalert2';

const configStore = useConfigStore();

const formName = ref('');
const formTitle = ref('');
const formLogo = ref('');

onMounted(() => {
    formName.value = configStore.siteName;
    formTitle.value = configStore.browserTitle;
    formLogo.value = configStore.siteLogo;
});

const handleSave = () => {
    configStore.saveConfig(formName.value, formLogo.value, formTitle.value);
    
    Swal.fire({
        title: 'Sucesso!',
        text: 'Configurações salvas com sucesso.',
        icon: 'success',
        background: '#202024',
        color: '#fff',
        confirmButtonColor: '#ffd700',
        confirmButtonText: '<span style="color: #000; font-weight: bold;">OK</span>'
    });
};
</script>

<template>
    <div class="page-container">
        
        <div class="page-header">
            <h2 class="page-title">⚙️ Configurações Gerais</h2>
            <p class="page-subtitle">Personalize a identidade visual e nome do seu site.</p>
        </div>

        <div class="form-card">
            
            <div class="preview-box">
                <span class="preview-label">Preview do Cabeçalho:</span>
                <div class="preview-content">
                    <img v-if="formLogo" :src="formLogo" class="logo-img" />
                    <span v-else class="logo-text">{{ formName }}</span>
                </div>
            </div>

            <div class="form-grid">
                <div class="form-group">
                    <label>Nome do Site (Marca)</label>
                    <input v-model="formName" type="text" placeholder="Ex: Brasil Game" />
                </div>

                <div class="form-group">
                    <label>Título da Aba (Browser)</label>
                    <input v-model="formTitle" type="text" placeholder="Ex: Apostas Esportivas" />
                    <span class="help-text">Texto que aparece na aba do navegador.</span>
                </div>

                <div class="form-group full-width">
                    <label>URL da Logo (Imagem)</label>
                    <input v-model="formLogo" type="text" placeholder="https://..." />
                    <span class="help-text">Cole o link da imagem. Deixe vazio para usar apenas texto.</span>
                </div>
            </div>

            <div class="form-actions">
                <button @click="handleSave" class="btn-save">
                    SALVAR ALTERAÇÕES
                </button>
            </div>

        </div>
    </div>
</template>

<style scoped>
/* Container Principal */
.page-container {
  max-width: 800px;
  margin: 0 auto;
  color: #e1e1e6;
}

/* Cabeçalho */
.page-header {
  margin-bottom: 20px;
}
.page-title {
  font-size: 1.5rem;
  font-weight: bold;
  color: #ffd700;
  margin-bottom: 5px;
}
.page-subtitle {
  color: #a8a8b3;
  font-size: 0.9rem;
}

/* Cartão do Formulário */
.form-card {
  background-color: #202024;
  border: 1px solid #323238;
  border-radius: 8px;
  padding: 30px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.2);
}

/* Preview Box */
.preview-box {
    background-color: #121214;
    border: 1px dashed #323238;
    border-radius: 8px;
    padding: 20px;
    margin-bottom: 30px;
    text-align: center;
}
.preview-label {
    display: block;
    font-size: 0.75rem;
    text-transform: uppercase;
    color: #7c7c8a;
    font-weight: bold;
    margin-bottom: 10px;
}
.preview-content {
    display: flex;
    justify-content: center;
    align-items: center;
    height: 60px;
}
.logo-img {
    height: 50px;
    object-fit: contain;
}
.logo-text {
    font-size: 1.5rem;
    font-weight: 900;
    color: #fff;
    font-style: italic;
}

/* Layout do Formulário */
.form-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 20px;
}
.form-group {
    flex: 1;
    min-width: 250px;
    display: flex;
    flex-direction: column;
}
.full-width {
    flex-basis: 100%;
}

/* Inputs e Labels */
label {
  margin-bottom: 8px;
  font-weight: 600;
  font-size: 0.85rem;
  color: #e1e1e6;
}

input { 
  padding: 12px;
  border-radius: 6px;
  border: 1px solid #323238;
  background: #121214;
  color: #fff; 
  font-size: 0.95rem;
  outline: none;
  transition: border-color 0.2s;
  width: 100%;
  box-sizing: border-box;
}

input:focus {
  border-color: #ffd700;
}

.help-text {
  font-size: 0.75rem;
  color: #7c7c8a;
  margin-top: 5px;
}

/* Botões */
.form-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 30px;
  padding-top: 20px;
  border-top: 1px solid #323238;
}

.btn-save { 
  padding: 12px 30px;
  background: #ffd700;
  color: #000;
  border: none; 
  font-weight: bold; 
  cursor: pointer; 
  border-radius: 6px;
  transition: background 0.2s, transform 0.1s;
}
.btn-save:hover { 
  background: #e6c200; 
}
.btn-save:active {
    transform: scale(0.98);
}
</style>