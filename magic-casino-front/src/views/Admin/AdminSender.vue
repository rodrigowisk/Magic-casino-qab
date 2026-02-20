<template>
  <div class="sender-container">
    <div class="header-area">
      <h2>📢 Central de Mensagens</h2>
      <p class="subtitle">Envie comunicados oficiais para seus usuários.</p>
    </div>

    <div class="form-card">
      <div class="form-group">
        <label>Quem deve receber?</label>
        <select v-model="form.type" class="input-dark">
          <option value="all">📢 Todos os Usuários</option>
          <option value="level">🏆 Por Nível (VIP)</option>
          <option value="user">👤 Usuário Único</option>
        </select>
      </div>

      <transition name="slide-fade">
        <div v-if="form.type === 'level'" class="form-group">
          <label>Selecione o Nível:</label>
          <select v-model="form.value" class="input-dark">
            <option value="Bronze">🥉 Bronze</option>
            <option value="Silver">🥈 Prata</option>
            <option value="Gold">🥇 Ouro</option>
          </select>
        </div>
      </transition>

      <transition name="slide-fade">
        <div v-if="form.type === 'user'" class="form-group">
          <label>CPF do Usuário:</label>
          <input 
            v-model="form.value" 
            placeholder="Digite o CPF (apenas números)" 
            class="input-dark"
          />
        </div>
      </transition>

      <div class="form-group">
        <label>Assunto:</label>
        <input 
          v-model="form.subject" 
          placeholder="Ex: Bônus de Depósito disponível!" 
          class="input-dark"
        />
      </div>

      <div class="form-group">
        <label>Mensagem:</label>
        <textarea 
          v-model="form.body" 
          placeholder="Escreva o conteúdo da mensagem aqui..." 
          rows="5"
          class="input-dark"
        ></textarea>
      </div>
      
      <div class="actions">
        <button @click="send" :disabled="loading" class="btn-bet">
          <span v-if="!loading">🚀 Enviar Mensagem</span>
          <span v-else>Enviando...</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import axios from 'axios';

const loading = ref(false);

const form = reactive({
  type: 'all',
  value: '',
  subject: '',
  body: ''
});

const send = async () => {
  if (!form.subject || !form.body) {
    alert("Preencha o assunto e a mensagem!");
    return;
  }

  loading.value = true;
  try {
    await axios.post('/api/messages/send', form);
    alert('✅ Mensagem enviada com sucesso!');
    
    // Limpar campos
    form.subject = '';
    form.body = '';
    if(form.type === 'user') form.value = '';
    
  } catch (error) {
    alert('❌ Erro ao enviar mensagem.');
    console.error(error);
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
/* Container Principal */
.sender-container {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
  color: #e1e1e6;
}

.header-area {
  margin-bottom: 30px;
}

.header-area h2 {
  color: #ffd700; /* Dourado */
  font-size: 1.5rem;
  margin-bottom: 5px;
}

.subtitle {
  color: #a8a8b3;
  font-size: 0.9rem;
}

/* Card do Formulário */
.form-card {
  background-color: #202024; /* Fundo cinza escuro */
  padding: 30px;
  border-radius: 8px;
  border: 1px solid #323238;
}

/* Grupos de Input */
.form-group {
  margin-bottom: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

label {
  font-size: 0.9rem;
  font-weight: 600;
  color: #ccc;
}

/* Inputs Estilizados Dark */
.input-dark {
  background-color: #121214; /* Fundo preto/cinza muito escuro */
  border: 1px solid #323238;
  color: #fff;
  padding: 12px 16px;
  border-radius: 6px;
  font-size: 1rem;
  outline: none;
  transition: border-color 0.2s;
  width: 100%; /* Ocupa a largura toda */
  box-sizing: border-box; /* Garante que o padding não estoure a largura */
}

.input-dark:focus {
  border-color: #ffd700; /* Borda dourada ao clicar */
}

.input-dark::placeholder {
  color: #555;
}

textarea.input-dark {
  resize: vertical;
  min-height: 100px;
}

/* Botão Estilo Bet */
.btn-bet {
  background-color: #ffd700;
  color: #121214;
  font-weight: 700;
  border: none;
  padding: 14px 24px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 1rem;
  width: 100%;
  transition: filter 0.2s;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.btn-bet:hover:not(:disabled) {
  filter: brightness(0.9);
}

.btn-bet:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Animação Suave para os campos condicionais */
.slide-fade-enter-active {
  transition: all 0.3s ease-out;
}
.slide-fade-leave-active {
  transition: all 0.3s ease-in;
}
.slide-fade-enter-from,
.slide-fade-leave-to {
  transform: translateY(-10px);
  opacity: 0;
}
</style>