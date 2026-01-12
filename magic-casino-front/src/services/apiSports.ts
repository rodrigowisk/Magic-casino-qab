import axios from "axios";

const apiSports = axios.create({
  baseURL: "http://localhost:8090/api",
  timeout: 10000,
});

apiSports.interceptors.request.use((config) => {
  // 🛡️ MODO BLINDADO: Lê direto do navegador, ignorando atrasos da Store
  let token = localStorage.getItem('token');

  if (token) {
    // 1. Limpeza Pesada: Remove aspas, espaços e caracteres estranhos
    token = token.replace(/['"]+/g, '').trim();
    
    // 2. Validação final antes de anexar
    if (token && token !== 'null' && token !== 'undefined') {
        config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

export default apiSports;