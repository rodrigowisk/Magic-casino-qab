using Microsoft.EntityFrameworkCore;
using Magic_casino_sportbook.Models;

namespace Magic_casino_sportbook.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Bet> Bets { get; set; }
        public DbSet<BetSelection> BetSelections { get; set; } // ✅ FALTAVA
        public DbSet<SportsEvent> SportsEvents { get; set; }
        public DbSet<MarketOdd> MarketOdds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Bet>().ToTable("Bets", "public");
            modelBuilder.Entity<BetSelection>().ToTable("BetSelections", "public");
            modelBuilder.Entity<SportsEvent>().ToTable("SportsEvents", "public");
            modelBuilder.Entity<MarketOdd>().ToTable("MarketOdds", "public");

            // ✅ relacionamento explícito
            modelBuilder.Entity<BetSelection>()
                .HasOne(bs => bs.Bet)
                .WithMany(b => b.Selections)
                .HasForeignKey(bs => bs.BetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
