using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_slot.Data
{
    [Table("providers_cache")]
    public class Provider
    {
        [Key]
        [Column("code")]
        public string Code { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("status")]
        public int Status { get; set; } = 1;

        [Column("game_count")]
        public int GameCount { get; set; } = 0;

        [Column("last_update")]
        public long LastUpdate { get; set; }
    }
}