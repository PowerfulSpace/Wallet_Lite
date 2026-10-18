using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", t =>
        {
            t.HasCheckConstraint("CK_LedgerEntries_Amount_Range", "[Amount] <> 0 AND ABS([Amount]) <= 1000000.00");
            t.HasCheckConstraint(
                "CK_LedgerEntries_ExactlyOneOperation",
                "([DepositId] IS NOT NULL AND [TransferId] IS NULL) OR ([DepositId] IS NULL AND [TransferId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WalletId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne<WalletAccount>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Deposit>().WithMany().HasForeignKey(x => x.DepositId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.DepositId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => new { x.WalletId, x.CreatedAtUtc, x.Id });
    }
}
