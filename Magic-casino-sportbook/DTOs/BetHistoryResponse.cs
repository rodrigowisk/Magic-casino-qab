using System;
using System.Collections.Generic;

namespace Magic_casino_sportbook.DTOs
{
    public class BetHistoryResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal TotalOdd { get; set; }
        public decimal PotentialReturn { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public List<BetSelectionResponse> Selections { get; set; } = new();
    }

    public class BetSelectionResponse
    {
        public string MatchName { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;
        public decimal Odd { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CommenceTime { get; set; }

        // ✅ NOVOS CAMPOS ENVIADOS PARA O FRONTEND
        public string? FinalScore { get; set; } // O Vue usará isso para mostrar o placar
        public bool? IsWinner { get; set; }     // O Vue usará isso para colorir o placar (Verde/Vermelho)
    }
}