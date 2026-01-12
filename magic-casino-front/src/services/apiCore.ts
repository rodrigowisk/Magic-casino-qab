import axios from "axios";
import { useAuthStore } from "../stores/useAuthStore";

const apiCore = axios.create({
  baseURL: "http://localhost:8080/api",
  timeout: 10000,
});

// sempre mandar JWT pro CORE
apiCore.interceptors.request.use((config) => {
  const auth = useAuthStore();
  const token = auth?.token;

  if (token) {
    config.headers = config.headers || {};
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export default apiCore;
