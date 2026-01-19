// ==========================================
// 1. DICIONÁRIOS (Mapeamentos Manuais)
// ==========================================

// Mapeia Nome do País (extraído da liga) -> Nome do Arquivo na pasta flags
// A chave deve ser sempre minúscula.
const countryToCode: Record<string, string> = {
  // Reino Unido (Separado)
  'england': 'gb-eng',
  'scotland': 'gb-sct',
  'wales': 'gb-wls',
  'northern ireland': 'gb-nir',

  // Europa Principal
  'spain': 'es',
  'italy': 'it',
  'germany': 'de',
  'france': 'fr',
  'portugal': 'pt',
  'netherlands': 'nl',
  'holanda': 'nl',
  'belgium': 'be',
  'switzerland': 'ch',
  'austria': 'at',
  'turkey': 'tr',
  'greece': 'gr',
  'russia': 'ru',
  'ukraine': 'ua',
  'croatia': 'hr',
  'czech republic': 'cz',
  'denmark': 'dk',
  'sweden': 'se',
  'norway': 'no',
  'poland': 'pl',
  'ireland': 'ie', // República da Irlanda

  // Américas
  'brazil': 'br',
  'argentina': 'ar',
  'uruguay': 'uy',
  'colombia': 'co',
  'usa': 'us',
  'america': 'us', // "America Friendlies"
  'mexico': 'mx',
  'chile': 'cl',
  'venezuela': 've',
  'peru': 'pe',
  'ecuador': 'ec',
  'paraguay': 'py',
  'bolivia': 'bo',
  'canada': 'ca',
  'costa rica': 'cr',
  'panama': 'pa',
  'jamaica': 'jm',

  // Ásia / Oceania
  'australia': 'au',
  'japan': 'jp',
  'south korea': 'kr',
  'china': 'cn',
  'india': 'in',
  'indonesia': 'id',
  'vietnam': 'vn',
  'saudi arabia': 'sa',

  // Outros
  'iceland': 'is',
  'europe': 'eu', // Para "Europe Friendlies"
  'world': 'un'   // Para "World Club Friendlies" (use un.svg ou globe.svg)
};

// Exceções de Times (Nome Limpo -> Nome do Arquivo exato sem extensão)
const teamNameExceptions: Record<string, string> = {
  // Ingleses
  'man utd': 'manchester-united',
  'man city': 'manchester-city',
  'spurs': 'tottenham-hotspur',
  'wolves': 'wolverhampton-wanderers',
  'nottm forest': 'nottingham-forest',
  'leeds': 'leeds-united',
  'west ham': 'west-ham-united',
  
  // Alemães (Comuns em Esoccer)
  'bayer 04': 'bayer-leverkusen',
  'dortmund': 'borussia-dortmund',
  'bayern': 'bayern-munich',
  'rb leipzig': 'rb-leipzig',
  'eintracht': 'eintracht-frankfurt',

  // Italianos (Comuns em Esoccer)
  'milan': 'ac-milan',
  'inter': 'inter-milan',
  'juve': 'juventus',
  'roma': 'roma',
  'lazio': 'lazio',
  'napoli': 'napoli',

  // Internacionais
  'psg': 'paris-sg',
  'real madrid': 'real-madrid',
  'barcelona': 'barcelona',
  'atlético madrid': 'atletico-madrid',

  // Brasileiros
  'atlético mg': 'atletico-mineiro',
  'athletico pr': 'athletico-paranaense',
  'sport recife': 'sport-recife',
  'botafogo rj': 'botafogo',
  'vasco': 'vasco-da-gama',
  'goias': 'goias',
  'ceara': 'ceara',
  'fortaleza': 'fortaleza'
};

// ==========================================
// 2. FUNÇÕES DE LIMPEZA
// ==========================================

/**
 * Remove sufixos de E-sports e Base
 * Ex: "Arsenal (ODYSSEY)" -> "Arsenal"
 * Ex: "Palmeiras U20" -> "Palmeiras"
 */
function cleanTeamName(rawName: string): string {
    if (!rawName) return '';
    return rawName
        .replace(/\s*\(.*?\)$/, '') // Remove (Texto) no final (ODYSSEY), (Koss), etc
        .replace(/\s+U\d+$/, '')    // Remove U20, U19, U23 no final
        .replace(/\s+Reserves$/, '') // Remove "Reserves"
        .replace(/\s+\(W\)$/, '')   // Remove (W)
        .trim();
}

// ==========================================
// 3. EXPORTS PRINCIPAIS
// ==========================================

export function getTeamLogo(teamName: string): string {
  if (!teamName) return '/images/teams/default.png';

  // 1. Limpa o lixo do nome
  const nameCleaned = cleanTeamName(teamName);
  const nameLower = nameCleaned.toLowerCase();

  // 2. Verifica exceções manuais
  if (teamNameExceptions[nameLower]) {
    return `/images/teams/${teamNameExceptions[nameLower]}.png`;
  }

  // 3. Slugify Padrão
  const slug = nameLower
    .normalize("NFD").replace(/[\u0300-\u036f]/g, "") // Remove acentos
    .replace(/[^a-z0-9\s-]/g, '') // Remove caracteres especiais
    .replace(/\s+/g, '-'); // Espaços -> Hifens

  return `/images/teams/${slug}.png`;
}

/**
 * Extrai a bandeira baseado no NOME DA LIGA
 * Ex: "England Premier League" -> detecta "England" -> retorna "gb-eng.svg"
 */
export function getFlagFromLeague(leagueName: string): string {
    if (!leagueName) return '/images/flags/globe.svg';
    
    const leagueLower = leagueName.toLowerCase();

    // 1. Tenta encontrar país na string da liga
    for (const country in countryToCode) {
        if (leagueLower.includes(country)) {
            return `/images/flags/${countryToCode[country]}.svg`;
        }
    }

    // 2. Casos Especiais (Esoccer, World, etc)
    if (leagueLower.includes('esoccer')) {
        return '/images/flags/esports.svg'; // Certifique-se de ter esse ícone ou use globe
    }
    if (leagueLower.includes('world') || leagueLower.includes('intl')) {
        return '/images/flags/globe.svg';
    }

    // 3. Padrão se não achar nada
    return '/images/flags/globe.svg';
}