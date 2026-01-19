using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ✅ Tabela Principal de Eventos
        public DbSet<SportsEvent> SportsEvents { get; set; } = default!;

        // ✅ NOVA Tabela de Odds (Substitui MarketOdds)
        public DbSet<EventMarket> EventMarkets { get; set; } = default!;

        public DbSet<Bet> Bets { get; set; } = default!;
        public DbSet<BetSelection> BetSelections { get; set; } = default!;

        // ✅ Tabelas Singulares (Convenção)
        public DbSet<LiveGameStat> LiveGameStat { get; set; } = default!;
        public DbSet<SportConfiguration> SportConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeamento da tabela de estatísticas ao vivo
            modelBuilder.Entity<LiveGameStat>().ToTable("LiveGameStat", "public");

            // --- CONFIGURAÇÃO DAS ODDS (EVENT MARKETS) ---
            modelBuilder.Entity<EventMarket>(entity =>
            {
                // Nome da tabela no banco
                entity.ToTable("EventMarkets");

                // Índice para busca rápida (Muito importante para performance!)
                entity.HasIndex(e => new { e.SportsEventId, e.MarketName });
            });

            // --- RELACIONAMENTOS ---
            modelBuilder.Entity<SportsEvent>()
                .HasMany(e => e.Odds)       // Um Jogo tem Muitas Odds
                .WithOne()                  // Uma Odd pertence a Um Jogo
                .HasForeignKey(o => o.SportsEventId) // A chave estrangeira na tabela de Odds
                .OnDelete(DeleteBehavior.Cascade);   // Se apagar o jogo, apaga as odds
        }
    }
}