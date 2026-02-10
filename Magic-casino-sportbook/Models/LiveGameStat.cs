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

        // ✅ NOVOS CAMPOS: ESTATÍSTICAS DO JOGO
        public int HomeCorners { get; set; } = 0;
        public int AwayCorners { get; set; } = 0;

        public int HomeYellowCards { get; set; } = 0;
        public int AwayYellowCards { get; set; } = 0;

        public int HomeRedCards { get; set; } = 0;
        public int AwayRedCards { get; set; } = 0;

        public int HomeDangerousAttacks { get; set; } = 0;
        public int AwayDangerousAttacks { get; set; } = 0;

        public int HomeAttacks { get; set; } = 0;
        public int AwayAttacks { get; set; } = 0;

        public int HomePossession { get; set; } = 0;
        public int AwayPossession { get; set; } = 0;

        public int HomeOnTarget { get; set; } = 0;
        public int AwayOnTarget { get; set; } = 0;

        public int HomeOffTarget { get; set; } = 0;
        public int AwayOffTarget { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}