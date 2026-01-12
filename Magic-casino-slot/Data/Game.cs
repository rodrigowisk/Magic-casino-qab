using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_slot.Data
{
    [Table("games_cache")]
    [PrimaryKey(nameof(GameCode), nameof(ProviderCode))] // Chave composta
    public class Game
    {
        [Column("game_code")]
        public string GameCode { get; set; }

        [Column("provider_code")]
        public string ProviderCode { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("banner")]
        public string? Banner { get; set; }

        [Column("status")]
        public int Status { get; set; } = 1;

        [Column("is_featured")]
        public int IsFeatured { get; set; } = 0;

        [Column("last_update")]
        public long LastUpdate { get; set; }
    }
}