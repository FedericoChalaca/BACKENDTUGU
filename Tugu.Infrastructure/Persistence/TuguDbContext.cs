using Microsoft.EntityFrameworkCore;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence;

public class TuguDbContext : DbContext
{
    public TuguDbContext(DbContextOptions<TuguDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Biometric> Biometrics => Set<Biometric>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.DocumentNumber).IsRequired().HasMaxLength(30);
            e.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            e.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            e.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(20);
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(u => new { u.DocumentType, u.DocumentNumber }).IsUnique();
            e.HasIndex(u => u.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets");
            // Regla no negociable: montos en decimal con precisión explícita.
            e.Property(w => w.Balance).HasPrecision(18, 2);
            e.Property(w => w.Currency).IsRequired().HasMaxLength(3);
            e.Property(w => w.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(w => w.UserId).IsUnique(); // 1 billetera por usuario
            e.HasOne(w => w.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<Wallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.BalanceAfter).HasPrecision(18, 2);
            e.Property(t => t.Reference).HasMaxLength(100);
            e.Property(t => t.CreatedBy).IsRequired().HasMaxLength(50);
            // Regla no negociable: idempotencia garantizada por índice único.
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
            e.HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Device)
                .WithMany()
                .HasForeignKey(t => t.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Biometric>(e =>
        {
            e.ToTable("biometrics");
            e.Property(b => b.EncryptedTemplate).IsRequired();
            e.Property(b => b.TemplateFormat).IsRequired().HasMaxLength(50);
            e.Property(b => b.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(b => b.UserId).IsUnique(); // 1 huella por usuario
            e.HasOne(b => b.User)
                .WithOne(u => u.Biometric)
                .HasForeignKey<Biometric>(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.Property(d => d.SerialNumber).IsRequired().HasMaxLength(100);
            e.Property(d => d.Alias).IsRequired().HasMaxLength(100);
            e.Property(d => d.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(d => d.SerialNumber).IsUnique();
        });
    }
}
