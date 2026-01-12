using Microsoft.EntityFrameworkCore;
// Se suas classes User/Wallet estiverem em outra pasta (ex: Models), adicione: using Magic_casino_slot.Models;

namespace Magic_casino_slot.Data
{
    public class AppDbContext : DbContext
    {
        // O segredo está aqui: : base(options) passa a configuração para o sistema
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Tabelas do Banco de Dados
        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Provider> Providers { get; set; } // Verifique se a classe Provider existe
        public DbSet<Game> Games { get; set; }         // Verifique se a classe Game existe
    }
}