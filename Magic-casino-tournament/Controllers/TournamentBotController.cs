using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Magic_casino_tournament.Controllers
{
    // ROTA: /api/tournaments/bot/generate
    [Route("api/tournaments/bot")]
    [ApiController]
    public class TournamentBotController : ControllerBase
    {
        private readonly ITournamentService _service;
        private readonly Random _rnd = new Random();

        // ✅ REGRAS (JSON) ESPECÍFICAS PARA CADA ESPORTE NO TEMPLATE
        private const string RULES_SOCCER = "[{\"key\":\"soccer\",\"leagues\":[{\"id\":\"USA MLS\",\"name\":\"USA MLS\",\"teams\":[]}]}]";
        private const string RULES_BASKETBALL = "[{\"key\":\"basketball\",\"leagues\":[{\"id\":\"NBA\",\"name\":\"NBA\",\"teams\":[]}]}]";
        private const string RULES_TENNIS = "[{\"key\":\"tennis\",\"leagues\":[{\"id\":\"ATP Dubai\",\"name\":\"ATP Dubai\",\"teams\":[]}]}]";
        private const string RULES_MIX = "[{\"key\":\"soccer\",\"leagues\":[]},{\"key\":\"basketball\",\"leagues\":[]}]";

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

        private readonly string[] _categories = { "Destaques", "Todo os torneios", "Torneios turbo", "Top da semana" };
        private readonly string[] _prizeRules = { "PREMIO_1", "PREMIO_2", "PREMIO_3", "WINNER_TAKES_ALL" };

        public TournamentBotController(ITournamentService service)
        {
            _service = service;
        }

        // GET /api/tournaments/bot/generate?sport=1
        [HttpGet("generate")]
        public async Task<IActionResult> GenerateTournaments([FromQuery] int sport = 1)
        {
            var createdLog = new List<string>();

            string folder = "";
            string sportName = "";
            string rules = "";
            int totalCovers = 0;

            // ✅ SELETOR BLINDADO POR PARÂMETRO
            switch (sport)
            {
                case 1:
                    folder = "soccer";
                    sportName = "Futebol";
                    rules = RULES_SOCCER;
                    totalCovers = 108;
                    break;
                case 2:
                    folder = "basketball";
                    sportName = "Basquete";
                    rules = RULES_BASKETBALL;
                    totalCovers = 88;
                    break;
                case 3:
                    folder = "tenis";
                    sportName = "Tênis";
                    rules = RULES_TENNIS;
                    totalCovers = 31;
                    break;
                case 4:
                    folder = "mix";
                    sportName = "Misto";
                    rules = RULES_MIX;
                    totalCovers = 22;
                    break;
                default:
                    return BadRequest(new { Message = "Parâmetro inválido. Use 1 (Futebol), 2 (Basquete), 3 (Tênis) ou 4 (Misto)." });
            }

            int totalCreated = 0;

            // Este loop garante que a capa NUNCA se repita
            for (int i = 1; i <= totalCovers; i++)
            {
                bool isFree = _rnd.NextDouble() < 0.3; // 30% chance de ser grátis

                // Salva EXATAMENTE a pasta + arquivo (Ex: soccer/1.svg ou basketball/1.svg)
                string coverPath = $"{folder}/{i}.svg";

                var t = CreateRandomTournament(isFree, coverPath, sportName, rules);

                await _service.CreateTournamentAsync(t);

                string typeStr = isFree ? "[GRÁTIS]" : "[PAGO]";
                createdLog.Add($"{typeStr} {t.Name} | Esporte: {sportName} | Capa: {t.CoverImage}");
                totalCreated++;
            }

            return Ok(new
            {
                Message = $"Sucesso Absoluto! {totalCreated} torneios criados para a categoria {sportName}.",
                Log = createdLog
            });
        }

        private Tournament CreateRandomTournament(bool isFree, string coverImage, string sport, string filterRules)
        {
            string name = $"{sport} - {_tournamentNames[_rnd.Next(_tournamentNames.Length)]}";

            string category;
            int rollCat = _rnd.Next(1, 101);
            if (rollCat <= 20) category = "Destaques";
            else category = _categories[_rnd.Next(1, 4)];

            decimal entryFee = 0;
            if (!isFree)
            {
                int roll = _rnd.Next(1, 101);
                if (roll <= 35) entryFee = 50;
                else if (roll <= 70) entryFee = 100;
                else if (roll <= 90) entryFee = 200;
                else if (roll <= 98) entryFee = 500;
                else entryFee = 1000;
            }

            string prizeRule = _prizeRules[_rnd.Next(_prizeRules.Length)];
            bool isFixedPrize = false;
            decimal? fixedPrizeAmount = null;
            int houseFee = 10;

            if (isFree)
            {
                isFixedPrize = true;
                fixedPrizeAmount = _rnd.Next(1, 6) * 1000;
                houseFee = 0;
            }
            else
            {
                if (_rnd.NextDouble() < 0.3)
                {
                    isFixedPrize = true;
                    fixedPrizeAmount = _rnd.Next(1, 21) * 1000;
                    houseFee = 0;
                }
                else
                {
                    houseFee = _rnd.Next(1, 4) * 5;
                }
            }

            int[] limits = { 50, 100, 500, 1000 };
            int maxParticipants = _rnd.NextDouble() < 0.5 ? limits[_rnd.Next(limits.Length)] : 0;

            return new Tournament
            {
                Name = name,
                Description = "Torneio gerado automaticamente pelo sistema.",
                EntryFee = entryFee,
                InitialFantasyBalance = 100000m,
                StartDate = DateTime.UtcNow.AddMinutes(_rnd.Next(10, 4320)),
                EndDate = DateTime.UtcNow.AddHours(_rnd.Next(1, 168)),
                Sport = sport,             // ✅ Salva Basquete, Tênis, Futebol, etc
                IsActive = true,
                IsFinished = false,
                FilterRules = filterRules, // ✅ Salva as regras corretas do JSON
                PrizePool = fixedPrizeAmount ?? 0,
                ParticipantsCount = 0,
                PrizeRuleId = prizeRule,
                Category = category,
                CoverImage = coverImage,   // ✅ Salva com a pasta inclusa
                HouseFeePercent = houseFee,
                FixedPrize = fixedPrizeAmount,
                MaxParticipants = maxParticipants
            };
        }
    }
}