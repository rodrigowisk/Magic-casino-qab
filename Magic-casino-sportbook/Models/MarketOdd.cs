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
        public string SportsEventId { get; set; }

        public string MarketName { get; set; }
        public string OutcomeName { get; set; }
        public decimal? Point { get; set; }
        public decimal Price { get; set; }
        public decimal ProfitMargin { get; set; } = 0.95m;

        public decimal FinalPrice => Math.Round(Price * ProfitMargin, 2);
    }
}