import axios from 'axios';

const api = axios.create({
    // IMPORTANTE: http (sem S) e porta 8090
    baseURL: 'http://localhost:8090/api/sports', 
    timeout: 10000,
});

export default api;