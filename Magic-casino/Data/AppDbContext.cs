using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;

namespace Magic_casino.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tabelas existentes
        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }

        // --- ADICIONADO: Tabela de Transações ---
        // Sem isso, o Controller não consegue salvar o histórico
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            // CONFIGURAÇÃO DE USUÁRIOS
            // ============================================================
            modelBuilder.Entity<User>()
                .HasKey(u => u.Cpf);

            // ============================================================
            // CONFIGURAÇÃO DE CARTEIRAS (1 Usuário : 1 Carteira)
            // ============================================================
            modelBuilder.Entity<Wallet>()
                .HasKey(w => w.UserCpf);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Wallet)
                .WithOne(w => w.User)
                .HasForeignKey<Wallet>(w => w.UserCpf);

            // ============================================================
            // CONFIGURAÇÃO DE TRANSAÇÕES (1 Usuário : Muitas Transações)
            // ============================================================
            modelBuilder.Entity<Transaction>()
                .HasKey(t => t.Id); // A chave primária é o ID

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.User)      // Uma transação tem um usuário
                .WithMany()               // Um usuário tem muitas transações
                .HasForeignKey(t => t.UserCpf); // A ligação é pelo CPF
        }
    }
}