import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import AuthService from '../services/AuthService';
import axios from 'axios'; 

export const useAuthStore = defineStore('auth', () => {
    // 1. Carrega o que está na memória do navegador
    const user = ref<any>(JSON.parse(localStorage.getItem('user') || 'null'));
    const token = ref<string | null>(localStorage.getItem('token'));

    const isAuthenticated = computed(() => !!token.value);

    // --- AÇÕES ---

    function setLogin(userData: any, tokenValue: string) {
        user.value = userData;
        token.value = tokenValue;
        localStorage.setItem('user', JSON.stringify(userData));
        localStorage.setItem('token', tokenValue);
    }

    function logout() {
        user.value = null;
        token.value = null;
        AuthService.logout();
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

            console.log(">>>>> [STORE] Buscando saldo via Proxy (/core)...");

            // ✅ CORREÇÃO:
            // Usamos '/core' para ativar o Proxy do Vite (que manda para o Nginx 8888).
            // O Nginx então remove '/core' e manda para o container na porta 8080.
            const response = await axios.get('/core/api/user/my-balance', {
                headers: {
                    Authorization: `Bearer ${cleanToken}`
                }
            });
            
            if (response.data && typeof response.data.balance === 'number') {
                console.log(">>>>> [STORE] Saldo Sincronizado:", response.data.balance);
                updateBalance(response.data.balance);
            }
        } catch (error) {
            console.error("Erro ao sincronizar saldo no Core:", error);
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
        updateBalance,
        fetchBalance 
    };
});