using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Models
{
    [Table("LiveGameStat", Schema = "public")]
    [Index(nameof(GameId), IsUnique = true)]
    public class LiveGameStat
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string GameId { get; set; } = string.Empty;

        public int HomeScore { get; set; } = 0;
        public int AwayScore { get; set; } = 0;

        [Required]
        public string CurrentMinute { get; set; } = "0'";

        [Required]
        public string Period { get; set; } = "1H";

        public bool IsFinished { get; set; } = false;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
