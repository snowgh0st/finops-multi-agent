using FinOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinOps.Infrastructure.Persistence;

// EF Core context for the append-only compliance audit trail (SQLite file
// "compliance_audit.db" in the API's content root, created on boot in
// Program.cs). Written to by both AuditFunctionFilter (tool-call audits) and
// MultiAgentRiskCoordinator (agent-turn audits). This is the first place to
// check when a compliance/support request asks "what did the agent do for
// transaction X" - query AuditLogs by TransactionId.
public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<ComplianceAuditLog> AuditLogs => Set<ComplianceAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}