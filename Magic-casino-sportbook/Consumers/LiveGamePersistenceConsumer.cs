using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Events; // Certifique-se que este namespace tem a versão atualizada do Evento
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Magic_casino_sportbook.Consumers
{
    public class LiveGamePersistenceConsumer : IConsumer<Batch<LiveGameUpdatedEvent>>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LiveGamePersistenceConsumer> _logger;

        public LiveGamePersistenceConsumer(AppDbContext context, ILogger<LiveGamePersistenceConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Batch<LiveGameUpdatedEvent>> context)
        {
            // 1. DEDUPLICAÇÃO
            var uniqueUpdates = context.Message
                .OrderByDescending(x => x.Message.LastUpdate)
                .DistinctBy(x => x.Message.GameId)
                .Select(x => x.Message)
                .ToList();

            if (!uniqueUpdates.Any()) return;

            try
            {
                // 2. BULK UPDATE
                await ExecutePostgresBulkUpdate(_context, uniqueUpdates);
                // _logger.LogInformation($"💾 [SQL BATCH] {uniqueUpdates.Count} jogos salvos.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Erro ao salvar Batch SQL: {ex.Message}");
            }
        }

        private async Task ExecutePostgresBulkUpdate(AppDbContext context, List<LiveGameUpdatedEvent> games)
        {
            var sb = new StringBuilder();
            var parameters = new List<object>();
            var paramIndex = 0;

            // ✅ QUERY ATUALIZADA COM TODOS OS CAMPOS NOVOS
            sb.Append("UPDATE \"SportsEvents\" AS s SET ");
            sb.Append("\"Score\" = v.score, ");
            sb.Append("\"GameTime\" = COALESCE(v.time, s.\"GameTime\"), "); 
            sb.Append("\"Status\" = v.status, ");
            sb.Append("\"LastUpdate\" = CAST(v.updated AS timestamp with time zone), ");

            // Odds
            sb.Append("\"RawOddsHome\" = COALESCE(CAST(v.home AS decimal), s.\"RawOddsHome\"), ");
            sb.Append("\"RawOddsDraw\" = COALESCE(CAST(v.draw AS decimal), s.\"RawOddsDraw\"), ");
            sb.Append("\"RawOddsAway\" = COALESCE(CAST(v.away AS decimal), s.\"RawOddsAway\"), ");

            // Estatísticas (Cartões e Escanteios)
            sb.Append("\"HomeRedCards\" = COALESCE(CAST(v.h_red AS integer), s.\"HomeRedCards\"), ");
            sb.Append("\"AwayRedCards\" = COALESCE(CAST(v.a_red AS integer), s.\"AwayRedCards\"), ");
            sb.Append("\"HomeYellowCards\" = COALESCE(CAST(v.h_yellow AS integer), s.\"HomeYellowCards\"), ");
            sb.Append("\"AwayYellowCards\" = COALESCE(CAST(v.a_yellow AS integer), s.\"AwayYellowCards\"), ");
            sb.Append("\"HomeCorners\" = COALESCE(CAST(v.h_corner AS integer), s.\"HomeCorners\"), ");
            sb.Append("\"AwayCorners\" = COALESCE(CAST(v.a_corner AS integer), s.\"AwayCorners\") ");

            sb.Append("FROM (VALUES ");

            for (int i = 0; i < games.Count; i++)
            {
                var g = games[i];
                if (i > 0) sb.Append(", ");

                // 14 Parâmetros por jogo
                sb.Append($"(@p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, " +
                          $"@p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, " +
                          $"@p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++})");

                // Values
                parameters.Add(g.GameId);
                parameters.Add(g.Score ?? string.Empty);
                parameters.Add(g.GameTime ?? string.Empty);
                parameters.Add(g.Status ?? "Live");
                parameters.Add(g.LastUpdate);

                // Tratamento de nulos para Odds (Decimal)
                parameters.Add(g.RawOddsHome ?? (object)DBNull.Value);
                parameters.Add(g.RawOddsDraw ?? (object)DBNull.Value);
                parameters.Add(g.RawOddsAway ?? (object)DBNull.Value);


                // Tratamento de Inteiros (Estatísticas)
                parameters.Add(g.HomeRedCards ?? (object)DBNull.Value);
                parameters.Add(g.AwayRedCards ?? (object)DBNull.Value);
                parameters.Add(g.HomeYellowCards ?? (object)DBNull.Value);
                parameters.Add(g.AwayYellowCards ?? (object)DBNull.Value);
                parameters.Add(g.HomeCorners);
                parameters.Add(g.AwayCorners);
            }

            // Definição das colunas virtuais (v)
            sb.Append(") AS v(id, score, time, status, updated, home, draw, away, h_red, a_red, h_yellow, a_yellow, h_corner, a_corner) ");
            sb.Append("WHERE s.\"ExternalId\" = v.id");

            await context.Database.ExecuteSqlRawAsync(sb.ToString(), parameters.ToArray());
        }
    }
}