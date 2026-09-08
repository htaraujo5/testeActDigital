using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountManager.Infrastructure.Persistence.Write.Models;

[Table("accounts")]
public sealed class AccountModel
{
    [Key]
    public Guid Id { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal Balance { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

[Table("ledger_entries")]
public sealed class LedgerEntryModel
{
    [Key]
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(16)]
    public string Type { get; set; } = string.Empty;
    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [Column(TypeName = "numeric(18,2)")]
    public decimal BalanceAfter { get; set; }
}

[Table("idempotency_records")]
public sealed class IdempotencyModel
{
    [Key]
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(128)]
    public string RequestHash { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public bool Succeeded { get; set; }
    [MaxLength(32)]
    public string Outcome { get; set; } = string.Empty;
    [MaxLength(64)]
    public string? ReasonCode { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal? BalanceAfter { get; set; }
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

[Table("audit_events")]
public sealed class AuditEventModel
{
    [Key]
    public Guid EventId { get; set; }
    [MaxLength(64)]
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    [MaxLength(128)]
    public string Actor { get; set; } = string.Empty;
    [MaxLength(64)]
    public string? SourceIp { get; set; }
    [MaxLength(512)]
    public string? UserAgent { get; set; }
    [MaxLength(64)]
    public string ApiInstanceId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    [MaxLength(32)]
    public string Operation { get; set; } = string.Empty;
    [Column(TypeName = "numeric(18,2)")]
    public decimal? Amount { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal? BalanceBefore { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal? BalanceAfter { get; set; }
    [MaxLength(32)]
    public string Outcome { get; set; } = string.Empty;
    [MaxLength(64)]
    public string? ReasonCode { get; set; }
    public int HttpStatus { get; set; }
    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }
    [MaxLength(128)]
    public string? RequestHash { get; set; }
    [MaxLength(16)]
    public string DbTarget { get; set; } = string.Empty;
    [MaxLength(32)]
    public string GateDecision { get; set; } = string.Empty;
}

[Table("users")]
public sealed class UserModel
{
    [Key]
    public Guid Id { get; set; }
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
