using FinOps.Domain.Entities;
using FinOps.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FinOps.Infrastructure.Agents;

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

        string fraudInspectorPrompt = "You are the Fraud Inspector Agent. Analyze transaction metrics, volume, velocity, and machine learning risk scores. State key technical risk flags cleanly in 2-3 sentences.";
        string marketRiskPrompt = "You are the Market Risk & Anti-Money Laundering (AML) Agent. Focus on geographic anomaly, structuring, wire transfers, and offshore jurisdiction risks. State regulatory risk flags in 2-3 sentences.";
        string complianceDirectorPrompt = "You are the Compliance Director Agent. Review insights from the Fraud Inspector and Market Risk Agents. Issue a final risk determination (APPROVED FOR SAR or REJECTED), and draft the final Suspicious Activity Report summary.";

        // 1. Fraud Inspector Turn
        chatHistory.AddSystemMessage(fraudInspectorPrompt);
        chatHistory.AddUserMessage($"Investigate Transaction ID: {transactionId}, Account: {accountId}, Amount: ${amount:N2}, ONNX Risk Score: {riskScore}, Location: {location}");
        var fraudResponse = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel);
        
        await LogAuditAsync(transactionId, accountId, "FraudInspectorAgent", "AgentTurn", fraudResponse.Content ?? "");

        // 2. Market Risk Turn
        chatHistory.AddSystemMessage(marketRiskPrompt);
        chatHistory.AddUserMessage("Based on the transaction and initial findings, evaluate AML and regulatory risks.");
        var marketResponse = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel);
        
        await LogAuditAsync(transactionId, accountId, "MarketRiskAgent", "AgentTurn", marketResponse.Content ?? "");

        // 3. Compliance Director Decision Turn
        chatHistory.AddSystemMessage(complianceDirectorPrompt);
        chatHistory.AddUserMessage("Synthesize the findings from Fraud Inspector and Market Risk. Generate the final consensus SAR report.");
        var directorResponse = await chatService.GetChatMessageContentAsync(chatHistory, kernel: _kernel);
        
        await LogAuditAsync(transactionId, accountId, "ComplianceDirectorAgent", "SarDecision", directorResponse.Content ?? "");

        _logger.LogInformation("[MultiAgent] ComplianceDirectorAgent issued final determination.");

        return directorResponse.Content ?? "Multi-agent investigation completed without summary.";
    }

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