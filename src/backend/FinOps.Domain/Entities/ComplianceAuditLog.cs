namespace FinOps.Domain.Entities;

// Append-only row in the SQLite compliance audit trail (see AuditDbContext).
// Written by both AuditFunctionFilter (one row per SK tool call, EventType
// "ToolInvocation") and MultiAgentRiskCoordinator (one row per agent turn,
// EventType "AgentTurn" or "SarDecision"). This is the audit evidence that a
// human/regulator would review to see what each agent said and why a SAR was
// or wasn't filed for a given TransactionId.
public class ComplianceAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AgentName { get; set; } = "SarComplianceAgent";
    public string EventType { get; set; } = string.Empty; // "PromptRender", "ToolInvocation", "SarDecision"
    public string PluginName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string InputParameters { get; set; } = string.Empty;
    public string OutputResult { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public double ExecutionDurationMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}