import { createApp } from 'vue'
import { createPinia } from 'pinia' // Importe o Pinia para gerenciar as apostas
import './style.css'
import App from './App.vue'
import router from './router'

const app = createApp(App)
const pinia = createPinia() // Instancie o Pinia

app.use(pinia) // Ative o Pinia ANTES do router
app.use(router) 
app.mount('#app')