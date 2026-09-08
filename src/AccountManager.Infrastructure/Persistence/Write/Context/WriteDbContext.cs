using AccountManager.Infrastructure.Persistence.Write.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountManager.Infrastructure.Persistence.Write.Context;

public sealed class WriteDbContext : DbContext
{
    public WriteDbContext(DbContextOptions<WriteDbContext> options) : base(options)
    {
    }

    public DbSet<AccountModel> Accounts => Set<AccountModel>();
    public DbSet<LedgerEntryModel> LedgerEntries => Set<LedgerEntryModel>();
    public DbSet<IdempotencyModel> IdempotencyRecords => Set<IdempotencyModel>();
    public DbSet<AuditEventModel> AuditEvents => Set<AuditEventModel>();
    public DbSet<UserModel> Users => Set<UserModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WriteDbContext).Assembly);
    }
}
