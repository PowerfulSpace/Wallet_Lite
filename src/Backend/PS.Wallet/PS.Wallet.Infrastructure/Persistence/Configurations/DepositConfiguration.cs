using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.ToTable("Deposits", t =>
        {
            t.HasCheckConstraint("CK_Deposits_Amount_Range", "[Amount] > 0 AND [Amount] <= 1000000.00");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WalletId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne<WalletAccount>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
