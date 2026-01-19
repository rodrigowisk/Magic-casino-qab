using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/LiveEvents")]
    public class LiveEventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LiveEventsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLiveEvents()
        {
            var agora = DateTime.UtcNow;

            // Busca jogos marcados como LIVE no banco
            var rawLiveGames = await _context.SportsEvents
                .AsNoTracking()
                .Where(e => e.Status == "Live")
                .Where(e => e.CommenceTime > agora.AddHours(-48)) // Segurança para não pegar lixo antigo
                .ToListAsync();

            // Faz o processamento em memória para garantir o parse correto
            var liveGamesDto = rawLiveGames.Select(e =>
            {
                // 1. Tenta pegar o Placar da string "Score" (Ex: "2-1") da tabela principal
                // Essa é a fonte mais confiável baseada nos seus prints
                int hScore = 0;
                int aScore = 0;

                if (!string.IsNullOrEmpty(e.Score) && e.Score.Contains("-"))
                {
                    var parts = e.Score.Split('-');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out hScore);
                        int.TryParse(parts[1], out aScore);
                    }
                }
                else
                {
                    // Fallback: Se não tiver string, tenta buscar na tabela auxiliar (que hoje está zerada, mas pode funcionar no futuro)
                    // Nota: Isso exigiria uma query separada ou Include, mas como a string Score já vem preenchida pelo robô, focamos nela.
                }

                // 2. Tenta pegar o Tempo da string "GameTime" (Ex: "45'", "HT")
                string tempoDisplay = e.GameTime ?? "0'";

                return new LiveGameDto
                {
                    GameId = e.ExternalId,
                    SportKey = e.SportKey,
                    HomeTeam = e.HomeTeam,
                    AwayTeam = e.AwayTeam,
                    League = e.League,

                    HomeTeamLogo = !string.IsNullOrEmpty(e.HomeTeamId) ? $"https://assets.b365api.com/images/team/m/{e.HomeTeamId}.png" : null,
                    AwayTeamLogo = !string.IsNullOrEmpty(e.AwayTeamId) ? $"https://assets.b365api.com/images/team/m/{e.AwayTeamId}.png" : null,

                    CountryCode = e.CountryCode,
                    FlagUrl = !string.IsNullOrEmpty(e.CountryCode) ? $"https://assets.b365api.com/images/flags/{e.CountryCode}.svg" : null,

                    CommenceTime = e.CommenceTime,

                    // ✅ AQUI ESTÁ A CORREÇÃO: Usamos as variáveis tratadas acima
                    HomeScore = hScore,
                    AwayScore = aScore,
                    CurrentMinute = tempoDisplay,
                    Period = "Live", // Ou mapear de e.GameTime se necessário

                    // Odds diretas da tabela principal
                    RawOddsHome = e.RawOddsHome,
                    RawOddsDraw = e.RawOddsDraw,
                    RawOddsAway = e.RawOddsAway
                };
            }).ToList();

            return Ok(liveGamesDto);
        }
    }
}