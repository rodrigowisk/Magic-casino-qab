using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Magic_casino_tournament.Controllers
{
    // ROTA: /api/tournaments/bot/generate
    [Route("api/tournaments/bot")]
    [ApiController]
    public class TournamentBotController : ControllerBase
    {
        private readonly ITournamentService _service;
        private readonly Random _rnd = new Random();

        // Template Fixo ("Todos os Jogos")
        private const string FILTER_RULES_TEMPLATE = "[{\"key\":\"soccer\",\"leagues\":[{\"id\":\"Cyprus Division 1\",\"name\":\"Cyprus Division 1\",\"teams\":[]},{\"id\":\"Türkiye 1 Lig\",\"name\":\"Türkiye 1 Lig\",\"teams\":[]},{\"id\":\"Copa Libertadores Qualification\",\"name\":\"Copa Libertadores Qualification\",\"teams\":[]},{\"id\":\"Belgium Cup\",\"name\":\"Belgium Cup\",\"teams\":[]},{\"id\":\"Bangladesh League Women\",\"name\":\"Bangladesh League Women\",\"teams\":[]},{\"id\":\"England National League\",\"name\":\"England National League\",\"teams\":[]},{\"id\":\"Argentina Liga Profesional\",\"name\":\"Argentina Liga Profesional\",\"teams\":[]},{\"id\":\"Japan J-League\",\"name\":\"Japan J-League\",\"teams\":[]},{\"id\":\"Bulgaria Cup\",\"name\":\"Bulgaria Cup\",\"teams\":[]},{\"id\":\"England Championship\",\"name\":\"England Championship\",\"teams\":[]},{\"id\":\"CONCACAF Champions Cup\",\"name\":\"CONCACAF Champions Cup\",\"teams\":[]},{\"id\":\"UEFA Champions League\",\"name\":\"UEFA Champions League\",\"teams\":[]},{\"id\":\"Thailand Division 3\",\"name\":\"Thailand Division 3\",\"teams\":[]},{\"id\":\"Netherlands Eredivisie\",\"name\":\"Netherlands Eredivisie\",\"teams\":[]},{\"id\":\"Italy Serie A\",\"name\":\"Italy Serie A\",\"teams\":[]},{\"id\":\"Portugal Segunda Liga\",\"name\":\"Portugal Segunda Liga\",\"teams\":[]},{\"id\":\"Slovenia Prva Liga\",\"name\":\"Slovenia Prva Liga\",\"teams\":[]},{\"id\":\"World Cup 2026\",\"name\":\"World Cup 2026\",\"teams\":[]},{\"id\":\"France Ligue 2\",\"name\":\"France Ligue 2\",\"teams\":[]},{\"id\":\"Peru Liga 1\",\"name\":\"Peru Liga 1\",\"teams\":[]},{\"id\":\"Portugal Primeira Liga\",\"name\":\"Portugal Primeira Liga\",\"teams\":[]},{\"id\":\"Uruguay Apertura\",\"name\":\"Uruguay Apertura\",\"teams\":[]},{\"id\":\"Germany Bundesliga I\",\"name\":\"Germany Bundesliga I\",\"teams\":[]},{\"id\":\"Thailand Premier League\",\"name\":\"Thailand Premier League\",\"teams\":[]},{\"id\":\"Scotland Championship\",\"name\":\"Scotland Championship\",\"teams\":[]},{\"id\":\"Albania Cup\",\"name\":\"Albania Cup\",\"teams\":[]},{\"id\":\"Israel Premier League\",\"name\":\"Israel Premier League\",\"teams\":[]},{\"id\":\"Chile Liga de Primera\",\"name\":\"Chile Liga de Primera\",\"teams\":[]},{\"id\":\"England Premier League\",\"name\":\"England Premier League\",\"teams\":[]},{\"id\":\"Israel Leumit Liga\",\"name\":\"Israel Leumit Liga\",\"teams\":[]},{\"id\":\"Croatia HNL\",\"name\":\"Croatia HNL\",\"teams\":[]},{\"id\":\"Türkiye Super Lig\",\"name\":\"Türkiye Super Lig\",\"teams\":[]},{\"id\":\"Scotland League Two\",\"name\":\"Scotland League Two\",\"teams\":[]},{\"id\":\"Saudi Arabia Division 1\",\"name\":\"Saudi Arabia Division 1\",\"teams\":[]},{\"id\":\"Mexico Liga MX Femenil\",\"name\":\"Mexico Liga MX Femenil\",\"teams\":[]},{\"id\":\"Indonesia Super League\",\"name\":\"Indonesia Super League\",\"teams\":[]},{\"id\":\"France Ligue 1\",\"name\":\"France Ligue 1\",\"teams\":[]},{\"id\":\"Europe Friendlies\",\"name\":\"Europe Friendlies\",\"teams\":[]},{\"id\":\"Mexico Liga MX\",\"name\":\"Mexico Liga MX\",\"teams\":[]},{\"id\":\"England League 2\",\"name\":\"England League 2\",\"teams\":[]},{\"id\":\"Greece Super League 1\",\"name\":\"Greece Super League 1\",\"teams\":[]},{\"id\":\"Scotland League Challenge Cup\",\"name\":\"Scotland League Challenge Cup\",\"teams\":[]},{\"id\":\"Switzerland Super League\",\"name\":\"Switzerland Super League\",\"teams\":[]},{\"id\":\"Burkina Faso Cup\",\"name\":\"Burkina Faso Cup\",\"teams\":[]},{\"id\":\"Brazil Serie A\",\"name\":\"Brazil Serie A\",\"teams\":[]},{\"id\":\"Germany DFB Pokal\",\"name\":\"Germany DFB Pokal\",\"teams\":[]},{\"id\":\"Bosnia & Herzegovina Cup\",\"name\":\"Bosnia & Herzegovina Cup\",\"teams\":[]},{\"id\":\"Argentina Cup\",\"name\":\"Argentina Cup\",\"teams\":[]},{\"id\":\"Northern Ireland Premier\",\"name\":\"Northern Ireland Premier\",\"teams\":[]},{\"id\":\"Bahrain Premier League\",\"name\":\"Bahrain Premier League\",\"teams\":[]},{\"id\":\"Brazil Campeonato Gaucho\",\"name\":\"Brazil Campeonato Gaucho\",\"teams\":[]},{\"id\":\"Coppa Italia\",\"name\":\"Coppa Italia\",\"teams\":[]},{\"id\":\"Kenya Premier League\",\"name\":\"Kenya Premier League\",\"teams\":[]},{\"id\":\"Denmark Cup\",\"name\":\"Denmark Cup\",\"teams\":[]},{\"id\":\"England League 1\",\"name\":\"England League 1\",\"teams\":[]},{\"id\":\"Scotland Premiership\",\"name\":\"Scotland Premiership\",\"teams\":[]},{\"id\":\"Brazil Campeonato Brasiliense\",\"name\":\"Brazil Campeonato Brasiliense\",\"teams\":[]},{\"id\":\"Colombia Primera B\",\"name\":\"Colombia Primera B\",\"teams\":[]},{\"id\":\"Belgium First Division A\",\"name\":\"Belgium First Division A\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paulista A4\",\"name\":\"Brazil Campeonato Paulista A4\",\"teams\":[]},{\"id\":\"Northern Ireland Cup\",\"name\":\"Northern Ireland Cup\",\"teams\":[]},{\"id\":\"UEFA Women's Europa Cup\",\"name\":\"UEFA Women's Europa Cup\",\"teams\":[]},{\"id\":\"Thailand Division 2\",\"name\":\"Thailand Division 2\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paulista A2\",\"name\":\"Brazil Campeonato Paulista A2\",\"teams\":[]},{\"id\":\"Cambodia Cup\",\"name\":\"Cambodia Cup\",\"teams\":[]},{\"id\":\"Italy Serie B\",\"name\":\"Italy Serie B\",\"teams\":[]},{\"id\":\"England Southern Premier League Central\",\"name\":\"England Southern Premier League Central\",\"teams\":[]},{\"id\":\"Saudi Arabia Pro League\",\"name\":\"Saudi Arabia Pro League\",\"teams\":[]},{\"id\":\"AFC Champions League Elite\",\"name\":\"AFC Champions League Elite\",\"teams\":[]},{\"id\":\"England Isthmian Premier Division\",\"name\":\"England Isthmian Premier Division\",\"teams\":[]},{\"id\":\"Hong Kong U22 League\",\"name\":\"Hong Kong U22 League\",\"teams\":[]},{\"id\":\"Europe - World Cup Qualifying\",\"name\":\"Europe - World Cup Qualifying\",\"teams\":[]},{\"id\":\"Saudi Arabia U21 League\",\"name\":\"Saudi Arabia U21 League\",\"teams\":[]},{\"id\":\"Jamaica Premier League\",\"name\":\"Jamaica Premier League\",\"teams\":[]},{\"id\":\"Argentina Nacional B\",\"name\":\"Argentina Nacional B\",\"teams\":[]},{\"id\":\"AFC Champions League Two\",\"name\":\"AFC Champions League Two\",\"teams\":[]},{\"id\":\"Copa Sudamericana\",\"name\":\"Copa Sudamericana\",\"teams\":[]},{\"id\":\"Mexico Liga de Expansion\",\"name\":\"Mexico Liga de Expansion\",\"teams\":[]},{\"id\":\"Netherlands Eerste Divisie\",\"name\":\"Netherlands Eerste Divisie\",\"teams\":[]},{\"id\":\"South American U20 Championship Women\",\"name\":\"South American U20 Championship Women\",\"teams\":[]},{\"id\":\"Bahrain Division 2\",\"name\":\"Bahrain Division 2\",\"teams\":[]},{\"id\":\"Spain Copa del Rey\",\"name\":\"Spain Copa del Rey\",\"teams\":[]},{\"id\":\"Austria Bundesliga\",\"name\":\"Austria Bundesliga\",\"teams\":[]},{\"id\":\"Azerbaijan Division 1\",\"name\":\"Azerbaijan Division 1\",\"teams\":[]},{\"id\":\"Spain La Liga\",\"name\":\"Spain La Liga\",\"teams\":[]},{\"id\":\"Brazil Campeonato Maranhense\",\"name\":\"Brazil Campeonato Maranhense\",\"teams\":[]},{\"id\":\"Denmark Superligaen\",\"name\":\"Denmark Superligaen\",\"teams\":[]},{\"id\":\"Australia A-League\",\"name\":\"Australia A-League\",\"teams\":[]},{\"id\":\"Spain Segunda\",\"name\":\"Spain Segunda\",\"teams\":[]},{\"id\":\"England Southern Premier League South\",\"name\":\"England Southern Premier League South\",\"teams\":[]},{\"id\":\"Greece Cup\",\"name\":\"Greece Cup\",\"teams\":[]},{\"id\":\"England FA Cup\",\"name\":\"England FA Cup\",\"teams\":[]},{\"id\":\"Colombia Primera A\",\"name\":\"Colombia Primera A\",\"teams\":[]},{\"id\":\"Cameroon Elite One\",\"name\":\"Cameroon Elite One\",\"teams\":[]},{\"id\":\"Azerbaijan Premier League\",\"name\":\"Azerbaijan Premier League\",\"teams\":[]},{\"id\":\"England National League South\",\"name\":\"England National League South\",\"teams\":[]},{\"id\":\"Brazil Campeonato Acreano\",\"name\":\"Brazil Campeonato Acreano\",\"teams\":[]},{\"id\":\"Germany Bundesliga II\",\"name\":\"Germany Bundesliga II\",\"teams\":[]},{\"id\":\"Brazil Campeonato Capixaba\",\"name\":\"Brazil Campeonato Capixaba\",\"teams\":[]},{\"id\":\"Scotland League One\",\"name\":\"Scotland League One\",\"teams\":[]},{\"id\":\"England Northern Premier League\",\"name\":\"England Northern Premier League\",\"teams\":[]},{\"id\":\"Australia A-League Women\",\"name\":\"Australia A-League Women\",\"teams\":[]},{\"id\":\"Venezuela Primera Division\",\"name\":\"Venezuela Primera Division\",\"teams\":[]},{\"id\":\"Rwanda National League\",\"name\":\"Rwanda National League\",\"teams\":[]},{\"id\":\"Belgium First Division B\",\"name\":\"Belgium First Division B\",\"teams\":[]},{\"id\":\"England Super League Women\",\"name\":\"England Super League Women\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paraibano\",\"name\":\"Brazil Campeonato Paraibano\",\"teams\":[]},{\"id\":\"Zanzibar Premier League\",\"name\":\"Zanzibar Premier League\",\"teams\":[]},{\"id\":\"Greece Super League 2\",\"name\":\"Greece Super League 2\",\"teams\":[]},{\"id\":\"Senegal Premier League\",\"name\":\"Senegal Premier League\",\"teams\":[]},{\"id\":\"England U21 Premier League Cup\",\"name\":\"England U21 Premier League Cup\",\"teams\":[]},{\"id\":\"England National League North\",\"name\":\"England National League North\",\"teams\":[]},{\"id\":\"Brazil Campeonato Cearense\",\"name\":\"Brazil Campeonato Cearense\",\"teams\":[]},{\"id\":\"UEFA Women's Champions League\",\"name\":\"UEFA Women's Champions League\",\"teams\":[]},{\"id\":\"Bulgaria First League\",\"name\":\"Bulgaria First League\",\"teams\":[]},{\"id\":\"Republic of Ireland Premier Division\",\"name\":\"Republic of Ireland Premier Division\",\"teams\":[]},{\"id\":\"Czechia First League\",\"name\":\"Czechia First League\",\"teams\":[]},{\"id\":\"Women’s Friendly\",\"name\":\"Women’s Friendly\",\"teams\":[]},{\"id\":\"England EFL Trophy\",\"name\":\"England EFL Trophy\",\"teams\":[]},{\"id\":\"England Premier League 2\",\"name\":\"England Premier League 2\",\"teams\":[]},{\"id\":\"England Southern League Div One\",\"name\":\"England Southern League Div One\",\"teams\":[]},{\"id\":\"Brazil Campeonato Catarinense\",\"name\":\"Brazil Campeonato Catarinense\",\"teams\":[]},{\"id\":\"UEFA Conference League\",\"name\":\"UEFA Conference League\",\"teams\":[]},{\"id\":\"Recopa Sudamericana\",\"name\":\"Recopa Sudamericana\",\"teams\":[]},{\"id\":\"UEFA Europa League\",\"name\":\"UEFA Europa League\",\"teams\":[]},{\"id\":\"USA MLS\",\"name\":\"USA MLS\",\"teams\":[]},{\"id\":\"Mexico Segunda Division\",\"name\":\"Mexico Segunda Division\",\"teams\":[]},{\"id\":\"Ecuador LigaPro Serie A\",\"name\":\"Ecuador LigaPro Serie A\",\"teams\":[]},{\"id\":\"Mauritania Division 1\",\"name\":\"Mauritania Division 1\",\"teams\":[]},{\"id\":\"Israel Youth League\",\"name\":\"Israel Youth League\",\"teams\":[]},{\"id\":\"Costa Rica Primera Division\",\"name\":\"Costa Rica Primera Division\",\"teams\":[]},{\"id\":\"Türkiye 3.Lig Group 4\",\"name\":\"Türkiye 3.Lig Group 4\",\"teams\":[]},{\"id\":\"Paraguay Division Profesional\",\"name\":\"Paraguay Division Profesional\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paulista\",\"name\":\"Brazil Campeonato Paulista\",\"teams\":[]},{\"id\":\"Switzerland Challenge League\",\"name\":\"Switzerland Challenge League\",\"teams\":[]},{\"id\":\"England EFL Cup\",\"name\":\"England EFL Cup\",\"teams\":[]},{\"id\":\"Romania Liga I\",\"name\":\"Romania Liga I\",\"teams\":[]},{\"id\":\"Finalissima\",\"name\":\"Finalissima\",\"teams\":[]},{\"id\":\"Hungary NB I\",\"name\":\"Hungary NB I\",\"teams\":[]},{\"id\":\"France National\",\"name\":\"France National\",\"teams\":[]},{\"id\":\"Poland Ekstraklasa\",\"name\":\"Poland Ekstraklasa\",\"teams\":[]},{\"id\":\"Republic of Ireland First Division\",\"name\":\"Republic of Ireland First Division\",\"teams\":[]},{\"id\":\"Costa Rica Segunda\",\"name\":\"Costa Rica Segunda\",\"teams\":[]},{\"id\":\"Egypt Division 1\",\"name\":\"Egypt Division 1\",\"teams\":[]},{\"id\":\"Northern Ireland Reserve League\",\"name\":\"Northern Ireland Reserve League\",\"teams\":[]},{\"id\":\"Myanmar National League\",\"name\":\"Myanmar National League\",\"teams\":[]},{\"id\":\"Portugal U23 League\",\"name\":\"Portugal U23 League\",\"teams\":[]},{\"id\":\"Gulf Club Championships\",\"name\":\"Gulf Club Championships\",\"teams\":[]},{\"id\":\"North Macedonia First League\",\"name\":\"North Macedonia First League\",\"teams\":[]},{\"id\":\"Premier League International Cup\",\"name\":\"Premier League International Cup\",\"teams\":[]},{\"id\":\"Bulgaria Second League\",\"name\":\"Bulgaria Second League\",\"teams\":[]},{\"id\":\"England Isthmian Division One South\",\"name\":\"England Isthmian Division One South\",\"teams\":[]},{\"id\":\"Northern Ireland Premier Intermediate League\",\"name\":\"Northern Ireland Premier Intermediate League\",\"teams\":[]},{\"id\":\"England Northern League Division One\",\"name\":\"England Northern League Division One\",\"teams\":[]},{\"id\":\"England Isthmian Division One North\",\"name\":\"England Isthmian Division One North\",\"teams\":[]},{\"id\":\"Uganda Cup\",\"name\":\"Uganda Cup\",\"teams\":[]},{\"id\":\"Cyprus Cup\",\"name\":\"Cyprus Cup\",\"teams\":[]},{\"id\":\"Spain Tercera Group 5\",\"name\":\"Spain Tercera Group 5\",\"teams\":[]},{\"id\":\"Bahrain U21 League\",\"name\":\"Bahrain U21 League\",\"teams\":[]},{\"id\":\"Romania Cup\",\"name\":\"Romania Cup\",\"teams\":[]},{\"id\":\"Italy Serie C Group C\",\"name\":\"Italy Serie C Group C\",\"teams\":[]},{\"id\":\"Israel Youth Cup\",\"name\":\"Israel Youth Cup\",\"teams\":[]},{\"id\":\"Egypt Second Division B\",\"name\":\"Egypt Second Division B\",\"teams\":[]},{\"id\":\"San Marino Campionato\",\"name\":\"San Marino Campionato\",\"teams\":[]},{\"id\":\"Slovakia Super Liga\",\"name\":\"Slovakia Super Liga\",\"teams\":[]},{\"id\":\"Brazil Copa Rio Women\",\"name\":\"Brazil Copa Rio Women\",\"teams\":[]},{\"id\":\"Gambia Division 2\",\"name\":\"Gambia Division 2\",\"teams\":[]},{\"id\":\"Iceland Cup Women\",\"name\":\"Iceland Cup Women\",\"teams\":[]},{\"id\":\"Algeria Elite Championship Women\",\"name\":\"Algeria Elite Championship Women\",\"teams\":[]},{\"id\":\"Brazil Amazonense\",\"name\":\"Brazil Amazonense\",\"teams\":[]},{\"id\":\"El Salvador Liga Feminina\",\"name\":\"El Salvador Liga Feminina\",\"teams\":[]},{\"id\":\"England Development League 2\",\"name\":\"England Development League 2\",\"teams\":[]},{\"id\":\"South Africa Cup\",\"name\":\"South Africa Cup\",\"teams\":[]},{\"id\":\"Kosovo Cup\",\"name\":\"Kosovo Cup\",\"teams\":[]},{\"id\":\"Iraq Premier Division\",\"name\":\"Iraq Premier Division\",\"teams\":[]},{\"id\":\"Mauritania Division 2\",\"name\":\"Mauritania Division 2\",\"teams\":[]},{\"id\":\"Senegal FA Cup\",\"name\":\"Senegal FA Cup\",\"teams\":[]},{\"id\":\"Mexico Liga TDP\",\"name\":\"Mexico Liga TDP\",\"teams\":[]},{\"id\":\"Rwanda Cup\",\"name\":\"Rwanda Cup\",\"teams\":[]},{\"id\":\"Algeria D1 Championship Women\",\"name\":\"Algeria D1 Championship Women\",\"teams\":[]},{\"id\":\"Honduras Reserve League\",\"name\":\"Honduras Reserve League\",\"teams\":[]},{\"id\":\"Campeonato Roraimense\",\"name\":\"Campeonato Roraimense\",\"teams\":[]},{\"id\":\"Malaysia Liga A1 Semi Pro\",\"name\":\"Malaysia Liga A1 Semi Pro\",\"teams\":[]},{\"id\":\"Hungary Cup\",\"name\":\"Hungary Cup\",\"teams\":[]},{\"id\":\"South Africa Premier\",\"name\":\"South Africa Premier\",\"teams\":[]},{\"id\":\"Tanzania Premier League\",\"name\":\"Tanzania Premier League\",\"teams\":[]},{\"id\":\"Malta Super Cup\",\"name\":\"Malta Super Cup\",\"teams\":[]},{\"id\":\"Wales Championship North\",\"name\":\"Wales Championship North\",\"teams\":[]},{\"id\":\"India U21 Development League\",\"name\":\"India U21 Development League\",\"teams\":[]},{\"id\":\"Panama Liga Prom\",\"name\":\"Panama Liga Prom\",\"teams\":[]},{\"id\":\"Argentina Liga Profesional Reserves\",\"name\":\"Argentina Liga Profesional Reserves\",\"teams\":[]},{\"id\":\"Brazil Campeonato Sergipano\",\"name\":\"Brazil Campeonato Sergipano\",\"teams\":[]},{\"id\":\"Brazil Campeonato Baiano\",\"name\":\"Brazil Campeonato Baiano\",\"teams\":[]},{\"id\":\"Costa Rica Cup\",\"name\":\"Costa Rica Cup\",\"teams\":[]},{\"id\":\"Poland I Liga\",\"name\":\"Poland I Liga\",\"teams\":[]},{\"id\":\"Singapore Premier League 2\",\"name\":\"Singapore Premier League 2\",\"teams\":[]},{\"id\":\"Brazil Campeonato Carioca\",\"name\":\"Brazil Campeonato Carioca\",\"teams\":[]},{\"id\":\"Panama LPF\",\"name\":\"Panama LPF\",\"teams\":[]},{\"id\":\"Honduras Liga Nacional\",\"name\":\"Honduras Liga Nacional\",\"teams\":[]},{\"id\":\"Argentina Primera B Metropolitana\",\"name\":\"Argentina Primera B Metropolitana\",\"teams\":[]},{\"id\":\"El Salvador Clausura\",\"name\":\"El Salvador Clausura\",\"teams\":[]},{\"id\":\"Scotland Lowland League Cup\",\"name\":\"Scotland Lowland League Cup\",\"teams\":[]},{\"id\":\"Angola Girabola\",\"name\":\"Angola Girabola\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paranaense\",\"name\":\"Brazil Campeonato Paranaense\",\"teams\":[]},{\"id\":\"Guatemala Primera Division\",\"name\":\"Guatemala Primera Division\",\"teams\":[]},{\"id\":\"Germany Bundesliga Women\",\"name\":\"Germany Bundesliga Women\",\"teams\":[]},{\"id\":\"India Mumbai Elite League\",\"name\":\"India Mumbai Elite League\",\"teams\":[]},{\"id\":\"England Reserve Matches\",\"name\":\"England Reserve Matches\",\"teams\":[]},{\"id\":\"Brazil Paraense\",\"name\":\"Brazil Paraense\",\"teams\":[]},{\"id\":\"Brazil Copa Alagoas\",\"name\":\"Brazil Copa Alagoas\",\"teams\":[]},{\"id\":\"Brazil Campeonato Goiano\",\"name\":\"Brazil Campeonato Goiano\",\"teams\":[]},{\"id\":\"Türkiye U19 League\",\"name\":\"Türkiye U19 League\",\"teams\":[]},{\"id\":\"Israel Liga Bet North\",\"name\":\"Israel Liga Bet North\",\"teams\":[]},{\"id\":\"Philippines PFL\",\"name\":\"Philippines PFL\",\"teams\":[]},{\"id\":\"Lebanon Cup\",\"name\":\"Lebanon Cup\",\"teams\":[]},{\"id\":\"Brazil Campeonato Paulista A3\",\"name\":\"Brazil Campeonato Paulista A3\",\"teams\":[]},{\"id\":\"Italy Serie D\",\"name\":\"Italy Serie D\",\"teams\":[]},{\"id\":\"Liberia National League Women\",\"name\":\"Liberia National League Women\",\"teams\":[]},{\"id\":\"Spain Segunda Division RFEF Group 2\",\"name\":\"Spain Segunda Division RFEF Group 2\",\"teams\":[]},{\"id\":\"Spain Segunda Division RFEF Group 3\",\"name\":\"Spain Segunda Division RFEF Group 3\",\"teams\":[]}]}]";

        // Lista de 50+ Nomes Variados
        private readonly string[] _tournamentNames = new[]
        {
            "Torneio da Alegria", "Torneio da Felicidade", "Torneio do Alemão", "Torneio da Dobra",
            "Torneio da Forra", "Torneio da Loucura", "Torneio dos Profissa", "Torneio do Amador",
            "Copa dos Campeões", "Liga dos Mitos", "Desafio da Madrugada", "Rei da Bet",
            "Torneio da Sorte", "Copa do Mundo Bet", "Liga dos Lendários", "Torneio Turbo",
            "Desafio Supremo", "Torneio dos Vencedores", "Copa Ouro", "Liga Prata",
            "Torneio Bronze", "Desafio Elite", "Torneio Master", "Copa Diamante",
            "Liga dos Craques", "Torneio Relâmpago", "Desafio Infinito", "Torneio Global",
            "Copa Internacional", "Liga Nacional", "Torneio Regional", "Desafio Local",
            "Torneio dos Amigos", "Copa da Galera", "Liga da Zueira", "Torneio Sério",
            "Desafio Profissional", "Torneio Amador", "Copa Iniciante", "Liga Intermediária",
            "Torneio Avançado", "Desafio Expert", "Torneio Mestre", "Copa Grão-Mestre",
            "Liga Lendária", "Torneio Épico", "Desafio Mítico", "Torneio Divino",
            "Copa Imortal", "Liga Eterna", "Torneio do Fim de Semana", "Sabadão da Forra",
            "Domingo de Glória", "Segunda da Sorte", "Terça do Green", "Quarta do Gol",
            "Quinta da Zebrinha", "Sexta da Maldade", "Arena dos Destemidos", "Clube da Aposta"
        };

        // Categorias Disponíveis
        private readonly string[] _categories = { "Destaques", "Todo os torneios", "Torneios turbo", "Top da semana" };

        private readonly string[] _prizeRules = { "PREMIO_1", "PREMIO_2", "PREMIO_3", "WINNER_TAKES_ALL" };

        public TournamentBotController(ITournamentService service)
        {
            _service = service;
        }

        // ✅ GET (Funciona no Navegador)
        // GET /api/tournaments/bot/generate?paid=70&free=30
        [HttpGet("generate")]
        public async Task<IActionResult> GenerateTournaments([FromQuery] int paid = 70, [FromQuery] int free = 30)
        {
            var createdLog = new List<string>();
            int currentImageIndex = 1; // 👈 Adicionado o contador de imagens

            // 1. Criar Pagos
            for (int i = 0; i < paid; i++)
            {
                var t = CreateRandomTournament(isFree: false, currentImageIndex);
                await _service.CreateTournamentAsync(t);
                createdLog.Add($"[PAGO] {t.Name} - R$ {t.EntryFee} (Capa: {t.CoverImage})");
                currentImageIndex++;
            }

            // 2. Criar Grátis
            for (int i = 0; i < free; i++)
            {
                var t = CreateRandomTournament(isFree: true, currentImageIndex);
                await _service.CreateTournamentAsync(t);
                createdLog.Add($"[GRÁTIS] {t.Name} (Capa: {t.CoverImage})");
                currentImageIndex++;
            }

            return Ok(new
            {
                Message = $"Sucesso! {paid + free} torneios criados.",
                Log = createdLog
            });
        }

        // 👈 Modificado para receber o índice da imagem
        private Tournament CreateRandomTournament(bool isFree, int imageIndex)
        {
            string name = _tournamentNames[_rnd.Next(_tournamentNames.Length)];

            // SELEÇÃO DE CATEGORIA COM PESO (20% DESTAQUES)
            string category;
            int rollCat = _rnd.Next(1, 101); // 1 a 100

            if (rollCat <= 20) // 20% de chance
            {
                category = "Destaques";
            }
            else // 80% de chance
            {
                // Escolhe entre as outras 3 categorias (índices 1 a 3)
                category = _categories[_rnd.Next(1, 4)];
            }

            decimal entryFee = 0;
            if (!isFree)
            {
                int roll = _rnd.Next(1, 101);
                if (roll <= 35) entryFee = 50;        // 35% chance
                else if (roll <= 70) entryFee = 100; // 35% chance
                else if (roll <= 90) entryFee = 200; // 20% chance
                else if (roll <= 98) entryFee = 500; // 8% chance
                else entryFee = 1000;                // 2% chance
            }

            string prizeRule = _prizeRules[_rnd.Next(_prizeRules.Length)];
            bool isFixedPrize = false;
            decimal? fixedPrizeAmount = null;
            int houseFee = 10;

            if (isFree)
            {
                isFixedPrize = true;

                // ✅ CORREÇÃO 1: GRÁTIS TAMBÉM SEGUE A REGRA DE 1000 a 5000 (Múltiplos de 1000)
                // Se preferir manter baixo, mude aqui. Mas para seguir sua regra "Sempre múltiplos de 1000":
                fixedPrizeAmount = _rnd.Next(1, 6) * 1000; // 1000, 2000, 3000, 4000, 5000

                houseFee = 0;
            }
            else
            {
                if (_rnd.NextDouble() < 0.3)
                {
                    isFixedPrize = true;
                    // Gera um número entre 1 e 20, e multiplica por 1000.
                    // Resultado: 1.000, 2.000, ... até 20.000
                    fixedPrizeAmount = _rnd.Next(1, 21) * 1000;

                    houseFee = 0;
                }
                else
                {
                    houseFee = _rnd.Next(1, 4) * 5; // 5, 10, 15
                }
            }

            int maxParticipants = 0;
            if (_rnd.NextDouble() < 0.5)
            {
                int[] limits = { 50, 100, 500, 1000 };
                maxParticipants = limits[_rnd.Next(limits.Length)];
            }

            var now = DateTime.UtcNow;
            var start = now.AddMinutes(_rnd.Next(10, 4320));
            var end = start.AddHours(_rnd.Next(1, 168));

            // ✅ A MATEMÁTICA DA IMAGEM: 
            // Subtraímos 1 do index para fazer o módulo (1 a 108 viram 0 a 107), 
            // dividimos por 108 pegando o resto, e somamos 1 novamente.
            // Exemplo: se chegar no 109: (109 - 1) % 108 = 0. 0 + 1 = 1.svg!
            int calculatedImageNumber = ((imageIndex - 1) % 108) + 1;
            string coverImage = $"{calculatedImageNumber}.svg";

            // ✅ SALDO FICTÍCIO SEMPRE 100.000
            decimal initialBalance = 100000m;

            return new Tournament
            {
                Name = name,
                Description = "Torneio gerado automaticamente pelo sistema. Boa sorte a todos!",
                EntryFee = entryFee,
                InitialFantasyBalance = initialBalance,
                StartDate = start,
                EndDate = end,
                Sport = "Futebol",
                IsActive = true,
                IsFinished = false,
                FilterRules = FILTER_RULES_TEMPLATE,
                // ✅ CORREÇÃO 2: INICIALIZA O POOL COM O VALOR FIXO (SE HOUVER)
                // Isso evita que apareça "R$ 0,00" na tela inicial
                PrizePool = fixedPrizeAmount ?? 0,
                ParticipantsCount = 0,
                PrizeRuleId = prizeRule,
                Category = category,
                CoverImage = coverImage,
                HouseFeePercent = houseFee,
                FixedPrize = fixedPrizeAmount,
                MaxParticipants = maxParticipants
            };
        }
    }
}