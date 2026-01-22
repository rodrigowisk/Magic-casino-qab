// src/utils/flags.ts

// Mapeamento: Palavra-chave (minúsculo) -> Nome do arquivo SVG (sem extensão)
const COUNTRY_MAP: Record<string, string> = {
    // --- Ligas Específicas e Conflitantes (Prioridade Alta) ---
    'jamaica premier league': 'jm',
    'singapore premier league': 'sg', 'singapore': 'sg', // ✅ Adicionado
    'malaysia super league': 'my', 'malaysia': 'my',
    'indonesia liga': 'id', 'indonesia': 'id',
    'brazil serie a': 'br', 'brasileirao': 'br',
    'italy serie a': 'it',
    'ecuador serie a': 'ec',
    'norway division': 'no',
    'vietnam': 'vn', 'viet': 'vn',

    // --- América do Sul ---
    'brazil': 'br', 'brasil': 'br',
    'argentina': 'ar',
    'uruguay': 'uy', 'uruguai': 'uy',
    'chile': 'cl',
    'colombia': 'co',
    'peru': 'pe',
    'paraguay': 'py', 'paraguai': 'py',
    'venezuela': 've',
    'bolivia': 'bo',
    'ecuador': 'ec', 'equador': 'ec',

    // --- América do Norte/Central ---
    'usa': 'us', 'america': 'us', 'united states': 'us', 'mls': 'us', 'nba': 'us', 'nfl': 'us',
    'mexico': 'mx',
    'canada': 'ca',
    'costa rica': 'cr',
    'panama': 'pa',
    'jamaica': 'jm',
    'honduras': 'hn',
    'el salvador': 'sv',
    'guatemala': 'gt',

    // --- Europa ---
    'england': 'gb', 'inglesa': 'gb', 'premier league': 'gb', 'kingdom': 'gb',
    'spain': 'es', 'espanha': 'es', 'laliga': 'es',
    'germany': 'de', 'alemanha': 'de', 'bundesliga': 'de',
    'italy': 'it', 'italia': 'it', 'serie a': 'it',
    'france': 'fr', 'franca': 'fr', 'ligue 1': 'fr',
    'portugal': 'pt',
    'netherlands': 'nl', 'holanda': 'nl', 'eredivisie': 'nl',
    'belgium': 'be', 'belgica': 'be',
    'russia': 'ru',
    'turkey': 'tr', 'turquia': 'tr',
    'greece': 'gr', 'grecia': 'gr',
    'ukraine': 'ua', 'ucrania': 'ua',
    'croatia': 'hr', 'croacia': 'hr',
    'sweden': 'se', 'suecia': 'se',
    'denmark': 'dk', 'dinamarca': 'dk',
    'norway': 'no', 'noruega': 'no',
    'poland': 'pl', 'polonia': 'pl',
    'switzerland': 'ch', 'suica': 'ch',
    'austria': 'at',
    'czech': 'cz',
    
    // --- Ásia/Oceania ---
    'japan': 'jp', 'japao': 'jp',
    'china': 'cn',
    'korea': 'kr', 'coreia': 'kr',
    'australia': 'au',
    'saudi': 'sa', 'arabia': 'sa',
    'india': 'in',
    'thailand': 'th',

    // --- Internacional / Outros ---
    'world': 'un', 'mundo': 'un', 'fifa': 'un', 'uefa': 'eu', 'libertadores': 'un',
    'friendly': 'un', 'amistoso': 'un'
};

/**
 * Retorna o caminho da bandeira.
 * Lógica: Ordena as chaves do mapa por TAMANHO (maiores primeiro).
 * Isso garante que "Singapore Premier League" seja encontrado ANTES de "Premier League".
 */
export const getFlag = (leagueName: string = '', countryCode?: string): string => {
    // 1. Prioridade absoluta: Código do país vindo da API
    if (countryCode && countryCode.length === 2) {
        return `/images/flags/${countryCode.toLowerCase()}.svg`;
    }

    const lowerName = leagueName.toLowerCase();
    
    // 2. Ordena as chaves por tamanho decrescente
    // (Isso é crucial: "Singapore Premier League" > "Premier League")
    const sortedKeys = Object.keys(COUNTRY_MAP).sort((a, b) => b.length - a.length);

    for (const key of sortedKeys) {
        if (lowerName.includes(key)) {
            return `/images/flags/${COUNTRY_MAP[key]}.svg`;
        }
    }

    // 3. Fallback
    return '/images/flags/un.svg';
};