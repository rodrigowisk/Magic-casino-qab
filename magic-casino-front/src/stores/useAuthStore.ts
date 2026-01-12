import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import AuthService from '../services/AuthService';
import axios from 'axios'; // ✅ IMPORTANTE: Usamos axios puro para ir em outra porta

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
            // 🧹 Limpeza de segurança no token (remove aspas extras se tiver)
            const cleanToken = token.value.replace(/['"]+/g, '');

            console.log(">>>>> [STORE] Buscando saldo direto no Core (8080)...");

            // ✅ MUDANÇA CRUCIAL:
            // Em vez de usar apiSports (8090), vamos direto no CORE (8080)
            // Isso evita o erro 401 do Sportbook e qualquer problema de comunicação entre containers
            const response = await axios.get('http://localhost:8080/api/user/my-balance', {
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