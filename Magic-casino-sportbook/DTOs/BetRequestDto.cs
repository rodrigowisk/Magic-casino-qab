using System;
using System.Collections.Generic;

namespace Magic_casino_sportbook.DTOs
{
    public class BetRequestDto
    {
        public decimal Amount { get; set; }
        public decimal TotalOdd { get; set; }
        public decimal PotentialReturn { get; set; }
        public string? RequestId { get; set; }

        public List<BetSelectionDto> Selections { get; set; } = new();
    }

    public class BetSelectionDto
    {
        public string MatchId { get; set; } = string.Empty;
        public string MatchName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;
        public decimal Odd { get; set; }
        public DateTime CommenceTime { get; set; }
    }
}