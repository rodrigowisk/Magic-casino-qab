// src/utils/countryTranslations.ts

// 1. Dicionário de Tradução (Chave = Nome na API, Valor = Nome em Português)
export const countryMap: Record<string, string> = {
    // Principais
    'Brazil': 'Brasil',
    'England': 'Inglaterra',
    'Spain': 'Espanha',
    'Italy': 'Itália',
    'Germany': 'Alemanha',
    'France': 'França',
    'Portugal': 'Portugal',
    'Netherlands': 'Holanda',
    'Argentina': 'Argentina',
    'USA': 'EUA',
    'United States': 'EUA',
    
    // Europa
    'Turkey': 'Turquia',
    'Russia': 'Rússia',
    'Belgium': 'Bélgica',
    'Austria': 'Áustria',
    'Switzerland': 'Suíça',
    'Sweden': 'Suécia',
    'Norway': 'Noruega',
    'Denmark': 'Dinamarca',
    'Poland': 'Polônia',
    'Ukraine': 'Ucrânia',
    'Greece': 'Grécia',
    'Croatia': 'Croácia',
    'Serbia': 'Sérvia',
    'Czech Republic': 'República Tcheca',
    'Czechia': 'República Tcheca',
    'Scotland': 'Escócia',
    'Wales': 'País de Gales',
    'Ireland': 'Irlanda',
    'Northern Ireland': 'Irlanda do Norte',
    'Iceland': 'Islândia',
    'Finland': 'Finlândia',
    'Romania': 'Romênia',
    'Bulgaria': 'Bulgária',
    'Hungary': 'Hungria',

    // Américas
    'Mexico': 'México',
    'Colombia': 'Colômbia',
    'Chile': 'Chile',
    'Uruguay': 'Uruguai',
    'Paraguay': 'Paraguai',
    'Peru': 'Peru',
    'Bolivia': 'Bolívia',
    'Ecuador': 'Equador',
    'Venezuela': 'Venezuela',
    'Canada': 'Canadá',
    'Costa Rica': 'Costa Rica',

    // Ásia / Oceania
    'China': 'China',
    'Japan': 'Japão',
    'South Korea': 'Coreia do Sul',
    'Australia': 'Austrália',
    'Saudi Arabia': 'Arábia Saudita',
    'India': 'Índia',

    // África
    'Egypt': 'Egito',
    'Morocco': 'Marrocos',
    'Nigeria': 'Nigéria',
    'South Africa': 'África do Sul',
    'Senegal': 'Senegal',
    'Cameroon': 'Camarões',
    
    // Termos Genéricos
    'World': 'Internacional',
    'International': 'Internacional',
    'Europe': 'Europa',
    'South America': 'América do Sul',
    'North America': 'América do Norte',
    'Asia': 'Ásia',
    'Africa': 'África'
};

// 2. Função Inteligente de Normalização
export const normalizeCountryName = (rawCountry: string, rawLeague: string): { country: string, league: string } => {
    if (!rawCountry) return { country: 'Internacional', league: rawLeague };

    // Remove espaços extras
    let cleanCountry = rawCountry.trim();
    let cleanLeague = rawLeague ? rawLeague.trim() : '';

    // CASO ESPECIAL: A API às vezes manda "Internacional" mas o nome do país está na liga
    // Ex: Country: "International", League: "Spanish Cup"
    // Aqui tentamos detectar isso.
    if (cleanCountry === 'International' || cleanCountry === 'Internacional' || cleanCountry === 'World') {
        // Tenta achar um país conhecido dentro do nome da liga
        for (const [englishName, ptName] of Object.entries(countryMap)) {
            if (cleanLeague.includes(englishName)) {
                return { country: ptName, league: cleanLeague };
            }
        }
    }

    // Verifica se existe tradução direta no mapa
    if (countryMap[cleanCountry]) {
        return { country: countryMap[cleanCountry], league: cleanLeague };
    }

    // Se a API mandou "Spain", mas não achou no mapa (impossível se o mapa estiver completo, mas por segurança)
    // Retorna o original.
    return { country: cleanCountry, league: cleanLeague };
};