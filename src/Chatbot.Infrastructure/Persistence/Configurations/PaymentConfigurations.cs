using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class TokenPackageConfiguration : IEntityTypeConfiguration<TokenPackage>
{
    public void Configure(EntityTypeBuilder<TokenPackage> b)
    {
        b.ToTable("TokenPackage", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Price).HasPrecision(18, 0); // VND không có thập phân
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class StudentTokenOrderConfiguration : IEntityTypeConfiguration<StudentTokenOrder>
{
    public void Configure(EntityTypeBuilder<StudentTokenOrder> b)
    {
        b.ToTable("StudentTokenOrder", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.Property(x => x.OrderRef).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.OrderRef).IsUnique().HasDatabaseName("UQ_StudentTokenOrder_OrderRef");

        b.Property(x => x.AmountPaid).HasPrecision(18, 0);
        b.Property(x => x.VnpayTransactionId).HasMaxLength(100);
        b.Property(x => x.VnpayResponseCode).HasMaxLength(10);
        b.Property(x => x.VnpayBankCode).HasMaxLength(20);
        b.Property(x => x.VnpayCardType).HasMaxLength(20);
        b.Property(x => x.VnpayRawResponse).HasColumnType(ColumnTypes.Json);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Package)
            .WithMany(p => p.Orders)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PaidAtUtc);
    }
}

public sealed class StudentTokenWalletConfiguration : IEntityTypeConfiguration<StudentTokenWallet>
{
    public void Configure(EntityTypeBuilder<StudentTokenWallet> b)
    {
        b.ToTable("StudentTokenWallet", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UQ_StudentTokenWallet_UserId");
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> b)
    {
        b.ToTable("TokenTransaction", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.WalletId);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Type);
        b.HasIndex(x => x.CreatedAtUtc);
    }
}
