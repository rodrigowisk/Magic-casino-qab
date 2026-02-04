using Microsoft.EntityFrameworkCore;
using Magic_casino_tournament.Models;

namespace Magic_casino_tournament.Data
{
    public class TournamentDbContext : DbContext
    {
        public TournamentDbContext(DbContextOptions<TournamentDbContext> options) : base(options) { }

        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentParticipant> TournamentParticipants { get; set; }
        public DbSet<TournamentTemplate> TournamentTemplates { get; set; }

        public DbSet<TournamentTransaction> TournamentTransactions { get; set; }
        public DbSet<TournamentBet> TournamentBets { get; set; }

        public DbSet<TournamentBetSelection> TournamentBetSelections { get; set; } // <--- ADICIONE ISSO

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}