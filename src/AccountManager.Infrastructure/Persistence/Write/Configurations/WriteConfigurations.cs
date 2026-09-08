using AccountManager.Infrastructure.Persistence.Write.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManager.Infrastructure.Persistence.Write.Configurations;

public sealed class AccountModelConfiguration : IEntityTypeConfiguration<AccountModel>
{
    public void Configure(EntityTypeBuilder<AccountModel> builder)
    {
        builder.ToTable("accounts", t => t.HasCheckConstraint("CK_accounts_balance_nonnegative", "\"Balance\" >= 0"));
    }
}

public sealed class LedgerEntryModelConfiguration : IEntityTypeConfiguration<LedgerEntryModel>
{
    public void Configure(EntityTypeBuilder<LedgerEntryModel> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasIndex(x => new { x.AccountId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.OccurredAtUtc });
    }
}

public sealed class IdempotencyModelConfiguration : IEntityTypeConfiguration<IdempotencyModel>
{
    public void Configure(EntityTypeBuilder<IdempotencyModel> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasIndex(x => new { x.AccountId, x.IdempotencyKey }).IsUnique();
    }
}

public sealed class AuditEventModelConfiguration : IEntityTypeConfiguration<AuditEventModel>
{
    public void Configure(EntityTypeBuilder<AuditEventModel> builder)
    {
        builder.ToTable("audit_events");
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.CorrelationId);
    }
}

public sealed class UserModelConfiguration : IEntityTypeConfiguration<UserModel>
{
    public void Configure(EntityTypeBuilder<UserModel> builder)
    {
        builder.ToTable("users");
        builder.HasIndex(x => x.Username).IsUnique();
    }
}
