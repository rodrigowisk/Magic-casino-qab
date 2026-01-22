# Estágio 1: Build da aplicação Vue
# ✅ Atualizado para Node 22 (LTS) para compatibilidade com Vite 7
FROM node:22-alpine AS build-stage
WORKDIR /app

# Copia os arquivos de configuração para instalação das dependências
COPY magic-casino-front/package*.json ./

# Instala as dependências
RUN npm install

# Copia todo o código fonte do frontend (Incluindo o novo nginx.conf)
COPY magic-casino-front/ .

# ✅ Executa o build (Com suporte total a crypto.hash e Vite 7)
RUN npm run build

# Estágio 2: Servir com Nginx na porta 80
FROM nginx:stable-alpine AS production-stage

# Copia os arquivos gerados no build para a pasta do Nginx
COPY --from=build-stage /app/dist /usr/share/nginx/html

# 👇👇👇 A CORREÇÃO MÁGICA ESTÁ AQUI 👇👇👇
# Copia o seu nginx.conf personalizado para dentro do servidor
COPY --from=build-stage /app/nginx.conf /etc/nginx/nginx.conf

# Expõe a porta 80 para o domínio quebrandoabanca.bet
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]