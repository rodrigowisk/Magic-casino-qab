import axios from 'axios';

const api = axios.create({
    // Ele vai escolher o link certo dependendo de onde você está rodando
    baseURL: import.meta.env.VITE_API_URL, 
    timeout: 10000,
});

// Opcional: Log para você conferir no console do navegador qual URL ele pegou
console.log("API conectada em:", import.meta.env.VITE_API_URL);

export default api;