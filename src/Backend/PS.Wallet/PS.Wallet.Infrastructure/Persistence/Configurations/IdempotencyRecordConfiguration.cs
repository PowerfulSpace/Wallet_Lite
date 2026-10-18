using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.OperationType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasColumnType("varbinary(32)").IsRequired();
        builder.Property(x => x.ResponsePayload).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(x => x.LeaseExpiresAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.OperationType, x.IdempotencyKey }).IsUnique();
    }
}
