using System.ComponentModel.DataAnnotations;

namespace Magic_casino_sportbook.Models
{
    public class FavoriteLeague
    {
        [Key]
        public int Id { get; set; }
        public string LeagueName { get; set; } = string.Empty;
        public string SportKey { get; set; } = string.Empty; // ex: soccer, basketball
        // Se tiver sistema de login, adicione: public string UserId { get; set; }
    }
}