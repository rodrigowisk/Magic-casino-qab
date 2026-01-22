import axios from 'axios';

// ✅ CORREÇÃO:
// Mudamos de 'http://localhost:8080/api/user' para '/core/api/user'.
// 1. O '/core' avisa o Nginx/Vite para mandar para o container "core".
// 2. O '/api/user' é o caminho que o backend espera receber.
const authApi = axios.create({
    baseURL: '/core/api/user', 
    timeout: 10000
});

// (Opcional) Adicione o interceptor do Token aqui também se precisar de autenticação nas rotas de usuário
authApi.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token.replace(/['"]+/g, '')}`;
    }
    return config;
});

export default {
    async login(cpf: string, password: string) {
        // Agora vai bater em: /core/api/user/login
        const response = await authApi.post('/login', { 
            code: cpf,
            password: password 
        });
        
        console.log("LOGIN RESPONSE:", response.data);

        if (response.data.token) {
            localStorage.setItem('token', response.data.token);
            
            // 1. Achar o nome correto
            const userName = 
                response.data.name || 
                response.data.fullName || 
                response.data.Name || 
                response.data.code || 
                "Apostador";

            // 2. Achar o saldo correto
            const qab = response.data.balance_qab ?? response.data.BalanceQab ?? response.data.balanceQab;
            const fiver = response.data.balance_fiver ?? response.data.BalanceFiver ?? response.data.balanceFiver;
            const finalBalance = qab !== undefined ? qab : (fiver !== undefined ? fiver : 0);

            // Objeto formatado padrão para o Front
            const userFormatted = {
                name: userName,
                balance: finalBalance
            };
            
            localStorage.setItem('user', JSON.stringify(userFormatted));

            return {
                ...response.data,
                user: userFormatted 
            };
        }
        return response.data;
    },

    async register(userData: any) {
        const response = await authApi.post('/register', {
            name: userData.name,
            email: userData.email,
            password: userData.password,
            cpf: userData.cpf,
            phone: userData.phone
        });
        return response.data;
    },

    logout() {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        // Opcional: Recarregar a página para limpar estados da memória
        // window.location.reload(); 
    },

    getCurrentUser() {
        const userStr = localStorage.getItem('user');
        return userStr ? JSON.parse(userStr) : null;
    }
}