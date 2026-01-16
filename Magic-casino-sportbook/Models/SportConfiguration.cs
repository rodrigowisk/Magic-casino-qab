using System.ComponentModel.DataAnnotations;

namespace Magic_casino_sportbook.Data.Models
{
    public class SportConfiguration
    {
        [Key]
        public int Id { get; set; }

        // Tipo: "SPORT", "LEAGUE" ou "TEAM"
        [Required]
        public string Type { get; set; }

        // Identificador: "soccer_epl", "10023", etc.
        [Required]
        public string Identifier { get; set; }

        // 1. Controle do ROBÔ (Ingestão)
        // Se FALSE: O robô ignora e não salva no banco (economiza recurso).
        public bool IsEnabled { get; set; } = true;

        // 2. Controle do SITE (Exibição)
        // Se FALSE: O robô baixa, mas o site principal esconde (útil para torneios exclusivos).
        public bool IsVisible { get; set; } = true;
    }
}