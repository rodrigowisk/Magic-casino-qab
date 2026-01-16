using System.Collections.Generic;

namespace Magic_casino_sportbook.DTOs
{
    // DTO principal para o Esporte
    public class SportConfigDto
    {
        public string Key { get; set; }        // Ex: "soccer"
        public string Name { get; set; }       // Ex: "Futebol"
        public string Icon { get; set; }       // Ex: "⚽"
        public bool IsActive { get; set; }     // Se o esporte aparece no site
        public List<LeagueConfigDto> Leagues { get; set; } = new List<LeagueConfigDto>();
    }

    // DTO para a Liga
    public class LeagueConfigDto
    {
        public string Id { get; set; }         // Identificador da liga
        public string Name { get; set; }       // Ex: "Premier League"
        public bool IsActive { get; set; }     // Se a liga aparece
        public List<TeamConfigDto> Teams { get; set; } = new List<TeamConfigDto>();
    }

    // DTO para o Time
    public class TeamConfigDto
    {
        public string Id { get; set; }         // ID do time
        public string Name { get; set; }       // Nome do time
        public bool IsActive { get; set; }     // Se o time aparece
    }
}