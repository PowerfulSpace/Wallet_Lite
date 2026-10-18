using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class WalletConfiguration : IEntityTypeConfiguration<WalletAccount>
{
    public void Configure(EntityTypeBuilder<WalletAccount> builder)
    {
        builder.ToTable("Wallets", t =>
        {
            t.HasCheckConstraint("CK_Wallets_Balance_Range", "[Balance] >= 0 AND [Balance] <= 1000000.00");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.Property(x => x.Balance).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
