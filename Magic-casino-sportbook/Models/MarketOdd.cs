using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_sportbook.Models
{
    public class MarketOdd
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SportsEventId { get; set; } = string.Empty; 
        public string Bookmaker { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;    
        public string OutcomeName { get; set; } = string.Empty;  

        public decimal? Point { get; set; }
        public decimal Price { get; set; }

        // Mantive sua lógica de margem de lucro
        public decimal ProfitMargin { get; set; } = 0.95m;

        public decimal FinalPrice => Math.Round(Price * ProfitMargin, 2);
    }
}