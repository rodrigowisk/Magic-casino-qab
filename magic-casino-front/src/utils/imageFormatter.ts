// Dicionário de Exceções: [Nome que vem da API] : [Nome do seu arquivo sem extensão]
// A chave deve estar sempre em minúsculo para facilitar a busca.
const teamNameExceptions: Record<string, string> = {
  // --- INGLATERRA (Premier League costuma vir abreviada) ---
  'man utd': 'manchester-united',
  'manchester utd': 'manchester-united',
  'man city': 'manchester-city',
  'manchester city fc': 'manchester-city',
  'spurs': 'tottenham-hotspur',
  'tottenham': 'tottenham-hotspur',
  'wolves': 'wolverhampton-wanderers',
  'wolverhampton': 'wolverhampton-wanderers',
  'nottm forest': 'nottingham-forest',
  'leicester': 'leicester-city',
  'leeds': 'leeds-united',
  'newcastle': 'newcastle-united',
  'brighton': 'brighton-and-hove-albion',
  'west ham': 'west-ham-united',
  'qpr': 'queens-park-rangers',

  // --- BRASIL (Diferenciar estados) ---
  'botafogo rj': 'botafogo',       // Seu arquivo é botafogo.png
  'botafogo fr': 'botafogo',
  'athletico pr': 'athletico-paranaense',
  'atlético paranaense': 'athletico-paranaense',
  'atlético mg': 'atletico-mineiro',
  'atletico mineiro': 'atletico-mineiro',
  'atletico go': 'atletico-goianiense',
  'sport recife': 'sport-recife', // Confirme se seu arquivo é sport-recife.png ou sport.png
  'vasco': 'vasco-da-gama',
  'vasco da gama': 'vasco-da-gama',
  'america mg': 'america-mineiro',
  
  // --- INTERNACIONAIS (Casos comuns) ---
  'psg': 'paris-sg', // Ou 'paris-saint-germain', verifique sua pasta
  'real madrid fc': 'real-madrid',
  'inter milan': 'inter-milan', // Ou 'internazionale'
  'ac milan': 'ac-milan',
  'bayern munich': 'bayern-munich', // API pode mandar 'Bayern München'
  'bayern munchen': 'bayern-munich'
};

export function getTeamLogoPath(teamName: string): string {
  if (!teamName) return '/images/teams/default.png';

  // 1. Limpeza inicial: minúsculo e remove espaços extras
  let cleanName = teamName.toLowerCase().trim();

  // 2. Verifica se está no dicionário de exceções ANTES de formatar tudo
  if (teamNameExceptions[cleanName]) {
    return `/images/teams/${teamNameExceptions[cleanName]}.png`;
  }

  // 3. Se não achou exceção, aplica a lógica padrão (Slugify)
  const slug = cleanName
    .normalize("NFD").replace(/[\u0300-\u036f]/g, "") // Remove acentos (São Paulo -> Sao Paulo)
    .replace(/[^a-z0-9\s-]/g, '') // Remove caracteres especiais (pontos, aspas)
    .replace(/\s+/g, '-'); // Troca espaços por hifens

  return `/images/teams/${slug}.png`;
}