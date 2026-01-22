import axios from "axios";
import { useAuthStore } from "../stores/useAuthStore";

const apiCore = axios.create({
  // ✅ CORREÇÃO: Usa o Proxy do Nginx (/core) em vez de localhost direto.
  // Isso garante que funcione tanto no seu PC quanto no domínio .bet
  baseURL: import.meta.env.VITE_API_URL_CORE || "/core/api", 
  timeout: 10000,
});

apiCore.interceptors.request.use((config) => {
  const auth = useAuthStore();
  // Limpeza de segurança no token (igual fizemos no apiSports)
  let token = auth?.token;
  
  if (token) {
    token = token.replace(/['"]+/g, '').trim();
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export default apiCore;