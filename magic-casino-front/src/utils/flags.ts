// src/utils/flags.ts

// Mapeamento: Palavra-chave (minúsculo) -> Nome do arquivo SVG (sem extensão)
const COUNTRY_MAP: Record<string, string> = {
    // --- Ligas Específicas e Conflitantes (Prioridade Alta) ---
    'jamaica premier league': 'jm',
    'singapore premier league': 'sg', 'singapore': 'sg',
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
    'colombia': 'co', 'colômbia': 'co',
    'peru': 'pe',
    'paraguay': 'py', 'paraguai': 'py',
    'venezuela': 've',
    'bolivia': 'bo', 'bolívia': 'bo',
    'ecuador': 'ec', 'equador': 'ec',

    // --- América do Norte/Central ---
    'usa': 'us', 'america': 'us', 'united states': 'us', 'eua': 'us',
    'mls': 'us', 'nba': 'us', 'nfl': 'us',
    'mexico': 'mx', 'méxico': 'mx',
    'canada': 'ca', 'canadá': 'ca',
    'costa rica': 'cr',
    'panama': 'pa', 'panamá': 'pa',
    'jamaica': 'jm',
    'honduras': 'hn',
    'el salvador': 'sv',
    'guatemala': 'gt',

    // --- Europa ---
    'bulgária': 'bg',
    'england': 'gb-eng', 'inglesa': 'gb-eng', 'inglaterra': 'gb-eng', 'premier league': 'gb-eng', 
    'spain': 'es', 'espanha': 'es', 'laliga': 'es',
    'finlândia': 'fi',
    'germany': 'de', 'alemanha': 'de', 'bundesliga': 'de',
    'italy': 'it', 'italia': 'it', 'itália': 'it', 'serie a': 'it',
    'france': 'fr', 'franca': 'fr', 'frança': 'fr', 'ligue 1': 'fr',
    'portugal': 'pt',
    'netherlands': 'nl', 'holanda': 'nl', 'eredivisie': 'nl',
    'belgium': 'be', 'belgica': 'be', 'bélgica': 'be',
    'russia': 'ru', 'rússia': 'ru',
    'turkey': 'tr', 'turquia': 'tr',
    'greece': 'gr', 'grecia': 'gr', 'grécia': 'gr',
    'hungria': 'hu',
    'islândia': 'is',
    'irlanda': 'ie',
    'ukraine': 'ua', 'ucrania': 'ua', 'ucrânia': 'ua',
    'croatia': 'hr', 'croacia': 'hr', 'croácia': 'hr',
    'sweden': 'se', 'suecia': 'se', 'suécia': 'se',
    'denmark': 'dk', 'dinamarca': 'dk',
    'norway': 'no', 'noruega': 'no',
    'poland': 'pl', 'polonia': 'pl', 'polônia': 'pl', 
    'romania': 'ro','romênia': 'ro',
    'serbia': 'rs', 'sérvia': 'rs',
    
    'switzerland': 'ch', 'suica': 'ch', 'suíça': 'ch',
    'austria': 'at', 'áustria': 'at',
    'czech': 'cz', 'tcheca': 'cz',
    'scotland': 'gb-sct', 'escócia': 'gb-sct',
    'wales': 'gb-wls', 'gales': 'gb-wls',
    
    // --- Ásia/Oceania ---
    'japan': 'jp', 'japao': 'jp', 'japão': 'jp',
    'china': 'cn',
    'korea': 'kr', 'coreia': 'kr',
    'australia': 'au', 'austrália': 'au',
    'saudi': 'sa', 'arabia': 'sa', 'arábia': 'sa',
    'india': 'in', 'índia': 'in',
    'thailand': 'th', 'tailandia': 'th', 'tailândia': 'th',

    // --- África ---
    'egypt': 'eg', 'egito': 'eg',
    'morocco': 'ma', 'marrocos': 'ma',
    'nigeria': 'ng', 'nigéria': 'ng',
    'south africa': 'za', 'áfrica do sul': 'za',
    'cameroon': 'cm', 'camarões': 'cm',
    'ghana': 'gh', 'gana': 'gh',
    'senegal': 'sn',

    // --- Internacional / Outros ---
    'world': 'un', 'mundo': 'un', 'fifa': 'un', 'uefa': 'eu', 'libertadores': 'un',
    'internacional': 'un', 'international': 'un',
    'friendly': 'un', 'amistoso': 'un'
};

export const getFlag = (leagueName: string = '', countryCode?: string): string => {
    // 1. Prioridade absoluta: Código do país vindo da API
    if (countryCode && countryCode.length === 2) {
        return `/images/flags/${countryCode.toLowerCase()}.svg`;
    }

    if (!leagueName) return '/images/flags/un.svg';

    const lowerName = leagueName.toLowerCase();
    
    // 2. Ordena as chaves por tamanho decrescente
    const sortedKeys = Object.keys(COUNTRY_MAP).sort((a, b) => b.length - a.length);

    for (const key of sortedKeys) {
        if (lowerName.includes(key)) {
            return `/images/flags/${COUNTRY_MAP[key]}.svg`;
        }
    }

    // 3. Fallback
    return '/images/flags/un.svg';
};