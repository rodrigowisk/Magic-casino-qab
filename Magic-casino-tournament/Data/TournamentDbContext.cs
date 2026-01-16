using Microsoft.EntityFrameworkCore;
using Magic_casino_tournament.Models;

namespace Magic_casino_tournament.Data
{
    public class TournamentDbContext : DbContext
    {
        public TournamentDbContext(DbContextOptions<TournamentDbContext> options) : base(options) { }

        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentParticipant> TournamentParticipants { get; set; }

        // ADICIONE ESTA LINHA:
        public DbSet<TournamentBet> TournamentBets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}