using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Magic_casino_sportbook.Services
{
    public class OddsService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly string _apiKey = "b6dc4a379005a00cf6da4945f1252aa7";

        public OddsService(HttpClient httpClient, AppDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task SyncAllSportsToDatabase()
        {
            try
            {
                // 1. Busca a lista de esportes ativos para saber quais chaves (keys) existem
                var sportsJson = await _httpClient.GetStringAsync($"https://api.the-odds-api.com/v4/sports/?apiKey={_apiKey}");
                var sports = JsonDocument.Parse(sportsJson).RootElement.EnumerateArray()
                    .Select(s => s.GetProperty("key").GetString())
                    .Where(k => k != null)
                    .ToList();

                foreach (var sportKey in sports)
                {
                    // 2. Define mercados dinâmicos para evitar o Erro 422
                    // Esportes que não são futebol não aceitam 'corners' ou 'btts'
                    string markets = "h2h,totals,spreads";
                    if (sportKey!.Contains("soccer"))
                    {
                        // Para futebol, tentamos btts. 
                        // Nota: Se a Pinnacle não tiver 'corners' para uma liga específica, a API pode dar 422.
                        markets = "h2h,totals,spreads,btts";
                    }

                    var url = $"https://api.the-odds-api.com/v4/sports/{sportKey}/odds/?apiKey={_apiKey}&regions=us,eu,uk,au&bookmakers=pinnacle&markets={markets}";

                    var response = await _httpClient.GetAsync(url);

                    // Se ainda assim der erro (ex: mercado btts não disponível na Pinnacle para esta liga), ignora e pula
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    var apiData = JsonSerializer.Deserialize<List<OddsResponse>>(jsonString);

                    if (apiData == null) continue;

                    foreach (var item in apiData)
                    {
                        var pinnacle = item.bookmakers?.FirstOrDefault(b => b.key.ToLower() == "pinnacle");
                        if (pinnacle == null || pinnacle.markets == null) continue;

                        // Busca evento existente ou cria um novo
                        var existingEvent = await _context.SportsEvents
                            .Include(e => e.Odds)
                            .FirstOrDefaultAsync(e => e.ExternalId == item.id);

                        if (existingEvent == null)
                        {
                            existingEvent = new SportsEvent
                            {
                                ExternalId = item.id,
                                HomeTeam = item.home_team,
                                AwayTeam = item.away_team,
                                SportKey = item.sport_key,
                                League = item.sport_key.Contains("_")
                                    ? item.sport_key.Split('_', 2)[1].Replace("_", " ")
                                    : item.sport_key,
                                CommenceTime = item.commence_time.ToUniversalTime()
                            };
                            _context.SportsEvents.Add(existingEvent);
                        }

                        // 3. Atualização das Odds: Remove as antigas e insere as novas (Sincronização limpa)
                        if (existingEvent.Odds != null && existingEvent.Odds.Any())
                        {
                            _context.MarketOdds.RemoveRange(existingEvent.Odds);
                            existingEvent.Odds.Clear();
                        }
                        else
                        {
                            existingEvent.Odds = new List<MarketOdd>();
                        }

                        foreach (var market in pinnacle.markets)
                        {
                            foreach (var outcome in market.outcomes)
                            {
                                existingEvent.Odds.Add(new MarketOdd
                                {
                                    MarketName = market.key,
                                    OutcomeName = outcome.name,
                                    Point = outcome.point,
                                    Price = outcome.price,
                                    SportsEventId = existingEvent.ExternalId
                                });
                            }
                        }
                    }
                    // Salva por esporte para não sobrecarregar a transação
                    await _context.SaveChangesAsync();
                }

                // 4. LÓGICA DE MANUTENÇÃO: Remove jogos muito antigos (iniciados há mais de 24h)
                var limitePassado = DateTime.UtcNow.AddDays(-1);
                var jogosParaRemover = _context.SportsEvents
                    .Where(e => e.CommenceTime < limitePassado);

                if (await jogosParaRemover.AnyAsync())
                {
                    _context.SportsEvents.RemoveRange(jogosParaRemover);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log de erro básico para debug no console do Docker
                Console.WriteLine($"Erro no OddsService: {ex.Message}");
            }
        }
    }
}