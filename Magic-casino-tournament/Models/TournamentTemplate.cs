using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; // Ex: "Futebol Completo", "Só Premier League"

        [Column(TypeName = "jsonb")] // Ou "text" se não usar Postgres
        public string FilterRules { get; set; } = "{}"; // O JSON com as ligas selecionadas
    }
}