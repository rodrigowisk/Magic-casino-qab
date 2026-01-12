import axios from 'axios';

const authApi = axios.create({
    baseURL: 'http://localhost:8080/api/user', 
    timeout: 10000
});

export default {
    async login(cpf: string, password: string) {
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

            // --- O TRUQUE ---
            // Retornamos o objeto 'user' formatado dentro da resposta
            // Assim o MainLayout pega o 'balance' certo imediatamente.
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
    },

    getCurrentUser() {
        const userStr = localStorage.getItem('user');
        return userStr ? JSON.parse(userStr) : null;
    }
}