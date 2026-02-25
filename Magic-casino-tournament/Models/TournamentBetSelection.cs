using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization; // <--- 1. ADICIONE ESTE IMPORT

namespace Magic_casino_tournament.Models
{
    public class TournamentBetSelection
    {
        [Key]
        public int Id { get; set; }

        public int TournamentBetId { get; set; }

        // <--- 2. ADICIONE O ATRIBUTO [JsonIgnore] AQUI
        // Isso impede que o JSON tente voltar para a aposta pai e crie o loop
        [ForeignKey("TournamentBetId")]
        [JsonIgnore]
        public virtual TournamentBet TournamentBet { get; set; }

        // Dados do Jogo
        public string GameId { get; set; } = string.Empty;
        public string SportKey { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string? FinalScore { get; set; }
        public DateTime? CommenceTime { get; set; }

        // Dados da Aposta Específica

        public string MarketName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Odds { get; set; }

        public string Status { get; set; } = "Pending";
    }
}