using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<SportsEvent> SportsEvents { get; set; } = default!;
        public DbSet<MarketOdd> MarketOdds { get; set; } = default!;
        public DbSet<Bet> Bets { get; set; } = default!;
        public DbSet<BetSelection> BetSelections { get; set; } = default!;

        // ✅ singular
        public DbSet<LiveGameStat> LiveGameStat { get; set; } = default!;
        public DbSet<SportConfiguration> SportConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ tabela singular também
            modelBuilder.Entity<LiveGameStat>().ToTable("LiveGameStat", "public");

            modelBuilder.Entity<SportsEvent>()
                .HasMany(e => e.Odds)
                .WithOne()
                .HasForeignKey(o => o.SportsEventId)
                .HasPrincipalKey(e => e.ExternalId);
        }
    }
}
