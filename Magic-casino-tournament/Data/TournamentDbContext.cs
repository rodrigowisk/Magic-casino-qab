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

        public DbSet<TournamentBetSelection> TournamentBetSelections { get; set; }

        // 👇 ADICIONADO: Tabela de Favoritos
        public DbSet<TournamentFavorite> TournamentFavorites { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configura Aposta (Pai) -> Seleções (Filhos)
            // Um bilhete tem várias seleções. Se apagar o bilhete, apaga as seleções (Cascade).
            modelBuilder.Entity<TournamentBet>()
                .HasMany(b => b.Selections)
                .WithOne(s => s.TournamentBet)
                .HasForeignKey(s => s.TournamentBetId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Configura Participante -> Apostas
            // Um participante faz várias apostas.
            modelBuilder.Entity<TournamentParticipant>()
                .HasMany(p => p.Bets)
                .WithOne(b => b.Participant)
                .HasForeignKey(b => b.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            // 👇 ADICIONADO: Configura Favoritos (Chave Composta)
            // Define que a chave primária é a combinação de UserId + TournamentId
            modelBuilder.Entity<TournamentFavorite>()
                .HasKey(f => new { f.UserId, f.TournamentId });
        }
    }
}