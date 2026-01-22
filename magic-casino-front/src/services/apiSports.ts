import axios from "axios";

const apiSports = axios.create({
  // ✅ CORREÇÃO FORÇADA:
  // Definimos direto "/sportbook/api".
  // Assim, quando o botão de aposta chamar "/bets/place",
  // a URL final será exata: "/sportbook/api/bets/place".
  baseURL: "/sportbook/api", 
  timeout: 15000,
});

apiSports.interceptors.request.use((config) => {
  // 🛡️ MODO BLINDADO: Pega o token limpo
  let token = localStorage.getItem('token');

  if (token) {
    // Remove aspas, espaços e garante que não é null/undefined string
    token = token.replace(/['"]+/g, '').trim();
    
    if (token && token !== 'null' && token !== 'undefined') {
        config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
}, (error) => {
    return Promise.reject(error);
});

export default apiSports;