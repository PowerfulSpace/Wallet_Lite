using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers", t =>
        {
            t.HasCheckConstraint("CK_Transfers_Amount_Range", "[Amount] > 0 AND [Amount] <= 1000000.00");
            t.HasCheckConstraint("CK_Transfers_DifferentWallets", "[SenderWalletId] <> [RecipientWalletId]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SenderWalletId).IsRequired();
        builder.Property(x => x.RecipientWalletId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne<WalletAccount>().WithMany().HasForeignKey(x => x.SenderWalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WalletAccount>().WithMany().HasForeignKey(x => x.RecipientWalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SenderWalletId);
        builder.HasIndex(x => x.RecipientWalletId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
