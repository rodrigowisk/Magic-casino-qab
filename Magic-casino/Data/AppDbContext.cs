using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;

namespace Magic_casino.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Define o CPF como chave primária na tabela de usuários
            modelBuilder.Entity<User>()
                .HasKey(u => u.Cpf);

            // Define UserCpf como chave primária na carteira (relação 1:1)
            modelBuilder.Entity<Wallet>()
                .HasKey(w => w.UserCpf);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Wallet)
                .WithOne(w => w.User)
                .HasForeignKey<Wallet>(w => w.UserCpf);
        }
    }
}