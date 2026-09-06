using Microsoft.EntityFrameworkCore;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence;

public class TuguDbContext : DbContext
{
    public TuguDbContext(DbContextOptions<TuguDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
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

        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("companies");
            e.Property(c => c.Name).IsRequired().HasMaxLength(150);
            e.Property(c => c.Nit).IsRequired().HasMaxLength(20);
            e.Property(c => c.Email).HasMaxLength(200);
            e.Property(c => c.PhoneNumber).HasMaxLength(20);
            e.Property(c => c.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(c => c.Nit).IsUnique();
        });

        modelBuilder.Entity<CompanyMember>(e =>
        {
            e.ToTable("company_members");
            e.Property(m => m.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(m => new { m.CompanyId, m.UserId }).IsUnique();
            e.HasOne(m => m.Company)
                .WithMany(c => c.Members)
                .HasForeignKey(m => m.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets", t => t.HasCheckConstraint(
                "CK_wallets_exactly_one_owner",
                "(\"UserId\" IS NOT NULL) <> (\"CompanyId\" IS NOT NULL)"));
            // Regla no negociable: montos en decimal con precisión explícita.
            e.Property(w => w.Balance).HasPrecision(18, 2);
            e.Property(w => w.Currency).IsRequired().HasMaxLength(3);
            e.Property(w => w.CreatedBy).IsRequired().HasMaxLength(50);
            e.HasIndex(w => w.UserId).IsUnique();     // 1 billetera por usuario
            e.HasIndex(w => w.CompanyId).IsUnique();  // 1 billetera por comercio
            e.HasOne(w => w.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<Wallet>(w => w.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Company)
                .WithOne(c => c.Wallet)
                .HasForeignKey<Wallet>(w => w.CompanyId)
                .IsRequired(false)
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
            e.HasIndex(d => d.CompanyId);
            e.HasOne(d => d.Company)
                .WithMany()
                .HasForeignKey(d => d.CompanyId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
