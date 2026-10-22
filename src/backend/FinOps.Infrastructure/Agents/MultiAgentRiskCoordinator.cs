using FinOps.Domain.Entities;
using FinOps.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FinOps.Infrastructure.Agents;

// EXECUTION PATH: the live SAR-generation pipeline, invoked by
// HighRiskTransactionListener whenever a transaction's anomaly score is >= 0.85.
// Simulates three agents as sequential turns on one shared ChatHistory (not
// separate Kernel instances or parallel calls):
//   1. Fraud Inspector  - technical risk flags from the ONNX score/velocity.
//   2. Market Risk/AML  - geographic/regulatory risk flags.
//   3. Compliance Director - final APPROVED/REJECTED determination + SAR text.
// Each turn's output is persisted to the SQLite compliance audit log via
// LogAuditAsync. All three turns pass FunctionChoiceBehavior.Auto() (see
// _toolExecutionSettings below), so any agent can call FraudInvestigationPlugin's
// Redis velocity/geo tools - each such call is itself audited by
// AuditFunctionFilter, separately from the per-turn LogAuditAsync rows here.
// Tool use isn't guaranteed on every run; it depends on the model deciding a
// tool call is useful for that turn's prompt.
public class MultiAgentRiskCoordinator
{
    private readonly Kernel _kernel;
    private readonly AuditDbContext _dbContext;
    private readonly ILogger<MultiAgentRiskCoordinator> _logger;

    public MultiAgentRiskCoordinator(
        Kernel kernel,
        AuditDbContext dbContext,
        ILogger<MultiAgentRiskCoordinator> logger)
    {
        _kernel = kernel;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> ExecuteMultiAgentInvestigationAsync(
        string transactionId,
        string accountId,
        double amount,
        double riskScore,
        string location)
    {
        _logger.LogInformation("[MultiAgent] Starting multi-agent investigation for Tx: {TxId}", transactionId);

        var chatHistory = new ChatHistory();
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // Enables automatic function calling so any agent turn below can invoke
        // FraudInvestigationPlugin's tools (registered on the Kernel in
        // Program.cs) instead of only reasoning over the metrics passed in text.
        var toolExecutionSettings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        string fraudInspectorPrompt = "You are the Fraud Inspector Agent. Analyze transaction metrics, volume, velocity, and machine learning risk scores. State key technical risk flags cleanly in 2-3 sentences.";
        string marketRiskPrompt = "You are the Market Risk & Anti-Money Laundering (AML) Agent. Focus on geographic anomaly, structuring, wire transfers, and offshore jurisdiction risks. State regulatory risk flags in 2-3 sentences.";
        string complianceDirectorPrompt = "You are the Compliance Director Agent. Review insights from the Fraud Inspector and Market Risk Agents. Issue a final risk determination (APPROVED FOR SAR or REJECTED), and draft the final Suspicious Activity Report summary.";

        // 1. Fraud Inspector Turn
        chatHistory.AddSystemMessage(fraudInspectorPrompt);
        chatHistory.AddUserMessage($"Investigate Transaction ID: {transactionId}, Account: {accountId}, Amount: ${amount:N2}, ONNX Risk Score: {riskScore}, Location: {location}");
        var fraudResponse = await chatService.GetChatMessageContentAsync(chatHistory, toolExecutionSettings, kernel: _kernel);

        await LogAuditAsync(transactionId, accountId, "FraudInspectorAgent", "AgentTurn", fraudResponse.Content ?? "");

        // 2. Market Risk Turn
        chatHistory.AddSystemMessage(marketRiskPrompt);
        chatHistory.AddUserMessage("Based on the transaction and initial findings, evaluate AML and regulatory risks.");
        var marketResponse = await chatService.GetChatMessageContentAsync(chatHistory, toolExecutionSettings, kernel: _kernel);

        await LogAuditAsync(transactionId, accountId, "MarketRiskAgent", "AgentTurn", marketResponse.Content ?? "");

        // 3. Compliance Director Decision Turn
        chatHistory.AddSystemMessage(complianceDirectorPrompt);
        chatHistory.AddUserMessage("Synthesize the findings from Fraud Inspector and Market Risk. Generate the final consensus SAR report.");
        var directorResponse = await chatService.GetChatMessageContentAsync(chatHistory, toolExecutionSettings, kernel: _kernel);

        await LogAuditAsync(transactionId, accountId, "ComplianceDirectorAgent", "SarDecision", directorResponse.Content ?? "");

        _logger.LogInformation("[MultiAgent] ComplianceDirectorAgent issued final determination.");

        return directorResponse.Content ?? "Multi-agent investigation completed without summary.";
    }

    // Persists one agent turn to the compliance audit trail. Failures here are
    // logged but swallowed (sad path) - a SQLite write failure must not abort
    // the investigation or drop the SAR result the compliance team is waiting on.
    private async Task LogAuditAsync(string txId, string accountId, string agentName, string eventType, string result)
    {
        try
        {
            var auditRecord = new ComplianceAuditLog
            {
                TransactionId = txId,
                AccountId = accountId,
                AgentName = agentName,
                EventType = eventType,
                PluginName = "MultiAgentCoordinator",
                FunctionName = eventType,
                OutputResult = result,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(auditRecord);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("[AuditLog] Persisted audit log for agent: {Agent} ({Event})", agentName, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditLog] Failed to persist audit log for agent: {Agent}", agentName);
        }
    }
}
