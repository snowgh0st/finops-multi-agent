using FinOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinOps.Infrastructure.Persistence;

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