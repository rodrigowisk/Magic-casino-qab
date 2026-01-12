using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.Models
{
    public class BetSelection
    {
        [Key]
        public int Id { get; set; }

        // Vincula com a Aposta Pai (Foreign Key)
        public int BetId { get; set; }

        [JsonIgnore] // Evita ciclo infinito no JSON
        public Bet Bet { get; set; } = null!; // Inicializado como null! para evitar aviso CS8618

        // Dados do Jogo no momento da aposta
        public string MatchId { get; set; } = string.Empty;   // Inicializado para evitar aviso CS8618
        public string MatchName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Odd { get; set; }

        // Status individual da seleção (pending, won, lost)
        public string Status { get; set; } = "pending";

        // Data e hora de início do jogo
        public DateTime CommenceTime { get; set; }

        // ✅ NOVOS CAMPOS PARA O RESULTADO (Passo 1)
        public string? FinalScore { get; set; } // Armazena o placar final (ex: "2-1")
        public bool? IsWinner { get; set; }     // Define se esta seleção foi a vencedora (null=pendente, true=green, false=red)
    }
}