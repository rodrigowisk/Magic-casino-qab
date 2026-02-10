import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import axios from 'axios'; 

export const useAuthStore = defineStore('auth', () => {
    // 1. Carrega o que está na memória do navegador
    const user = ref<any>(JSON.parse(localStorage.getItem('user') || 'null'));
    const token = ref<string | null>(localStorage.getItem('token'));

    const isAuthenticated = computed(() => !!token.value);

    // --- AÇÕES ---

    function setLogin(userData: any, tokenValue: string) {
        // 1. Atualiza o estado na memória
        user.value = userData;
        token.value = tokenValue;

        // 2. Grava no navegador (Persistência)
        localStorage.setItem('user', JSON.stringify(userData));
        localStorage.setItem('token', tokenValue);

        fetchBalance();
    }

    function logout() {
        // 1. Limpa estado
        user.value = null;
        token.value = null;

        // 2. Limpa navegador
        localStorage.clear();
        sessionStorage.clear(); 

        // 3. 🚀 Redireciona para a HOME (/) em vez de /login
        window.location.href = '/'; 
    }

    // ✅ NOVA AÇÃO: Atualiza dados do usuário (Avatar, Nome) sem deslogar
    function updateUser(updatedData: any) {
        if (user.value) {
            // Mescla os dados atuais com os novos (ex: mantém o saldo, muda só o avatar)
            user.value = { ...user.value, ...updatedData };
            
            // Atualiza no localStorage para não perder ao dar F5
            localStorage.setItem('user', JSON.stringify(user.value));
        }
    }

    function updateBalance(newBalance: number) {
        if (user.value) {
            user.value.balance = newBalance;
            localStorage.setItem('user', JSON.stringify(user.value));
        }
    }

    async function fetchBalance() {
        if (!token.value) return;

        try {
            // 🧹 Limpeza de segurança no token
            const cleanToken = token.value.replace(/['"]+/g, '');

            // ✅ Usamos '/core' para ativar o Proxy do Vite
            const response = await axios.get('/core/api/user/my-balance', {
                headers: {
                    Authorization: `Bearer ${cleanToken}`
                }
            });
            
            if (response.data && typeof response.data.balance === 'number') {
                updateBalance(response.data.balance);
            }
        } catch (error) {
            //console.error("Erro ao sincronizar saldo no Core:", error);
        }
    }

    // =========================================================================
    // 🚀 AUTO-ATUALIZAÇÃO AO INICIAR
    // =========================================================================
    if (token.value) {
        fetchBalance();
    }

    return { 
        user, 
        token, 
        isAuthenticated, 
        setLogin, 
        logout, 
        updateUser,    // <--- ✅ Não esqueça de exportar aqui!
        updateBalance,
        fetchBalance 
    };
});