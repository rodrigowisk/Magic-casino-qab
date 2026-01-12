// src/utils/imageUtils.ts

export const getTeamLogo = (teamName: string) => {
    if (!teamName) return '/images/teams/default.png'; // Imagem padrão se vier vazio

    // Essa limpeza TEM QUE SER IGUAL a do robô de download
    const cleanName = teamName
        .toLowerCase()
        .normalize("NFD").replace(/[\u0300-\u036f]/g, "") // Tira acentos
        .replace(/\s+/g, '-') // Troca espaço por traço
        .replace(/[^a-z0-9-]/g, ''); // Remove caracteres especiais

    return `/images/teams/${cleanName}.png`;
};

// Se a imagem não existir (o robô falhou), mostra um escudo cinza genérico
export const handleImageError = (e: Event) => {
    (e.target as HTMLImageElement).src = '/images/teams/default.png';
};