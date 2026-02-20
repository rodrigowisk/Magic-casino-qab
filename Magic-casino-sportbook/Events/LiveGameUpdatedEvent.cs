namespace Magic_casino_sportbook.Events
{
    // Este evento carrega o "Snapshot" do jogo a cada 10s
    public class LiveGameUpdatedEvent
    {
        // ✅ CORREÇÃO 1: Inicialize as strings para evitar erro CS8618 (Non-nullable property)
        public string GameId { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
        public string? GameTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }

        // ✅ CORREÇÃO 2: Adicione os Scores numéricos (O Worker está tentando preencher isso)
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        // Odds (para manter histórico)
        public decimal? RawOddsHome { get; set; }
        public decimal? RawOddsDraw { get; set; }
        public decimal? RawOddsAway { get; set; }

        // Estatísticas
        public int? HomeRedCards { get; set; }
        public int? AwayRedCards { get; set; }
        public int? HomeYellowCards { get; set; }
        public int? AwayYellowCards { get; set; }
        public int? HomeCorners { get; set; }
        public int? AwayCorners { get; set; }

        // Novos dados
        public int HomeAttacks { get; set; }
        public int AwayAttacks { get; set; }
        public int HomeDangerousAttacks { get; set; }
        public int AwayDangerousAttacks { get; set; }
        public int HomeOnTarget { get; set; }
        public int AwayOnTarget { get; set; }
        public int HomeOffTarget { get; set; }
        public int AwayOffTarget { get; set; }
        public int HomePossession { get; set; }
        public int AwayPossession { get; set; }
    }
}