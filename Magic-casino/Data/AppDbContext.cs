using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;
using Magic_casino.Models;

namespace Magic_casino.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tabelas existentes
        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }

        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageRecipient> MessageRecipients { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // ============================================================
            // CONFIGURAÇÃO DE MENSAGENS (Mapeamento Manual para Postgres)
            // ============================================================

            // 1. Tabela messages
            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("messages"); // Nome exato no banco

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Subject).HasColumnName("subject");
                entity.Property(e => e.Body).HasColumnName("body");
                entity.Property(e => e.TargetType).HasColumnName("target_type");
                entity.Property(e => e.TargetValue).HasColumnName("target_value");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // 2. Tabela message_recipients
            modelBuilder.Entity<MessageRecipient>(entity =>
            {
                entity.ToTable("message_recipients"); // Nome exato no banco

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.MessageId).HasColumnName("message_id");
                entity.Property(e => e.UserId).HasColumnName("user_id"); // CPF string
                entity.Property(e => e.IsRead).HasColumnName("is_read");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.ReadAt).HasColumnName("read_at");

                // Relacionamentos
                entity.HasOne(d => d.Message)
                    .WithMany()
                    .HasForeignKey(d => d.MessageId)
                    .HasConstraintName("fk_messages"); // Nome da FK que criamos no SQL
            });


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