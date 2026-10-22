using FinOps.Core.Plugins;
using FinOps.Infrastructure.Cache;
using FinOps.Core.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FinOps.Infrastructure.Services;

// Alternate SAR-generation path: a single LLM agent, single turn, that relies
// entirely on FraudInvestigationPlugin tool calls for its findings (no
// metrics are handed to it directly in the prompt - contrast with
// MultiAgentRiskCoordinator's three-turn flow, which passes metrics in the
// prompt text AND allows optional tool calls on top).
//
// NOT CURRENTLY WIRED UP: this service is not registered in DI (Program.cs
// only registers MultiAgentRiskCoordinator) and nothing calls
// InvestigateAndGenerateSarAsync - the live pipeline uses
// MultiAgentRiskCoordinator instead (triggered from
// HighRiskTransactionListener). Keep this in mind before "fixing" this class;
// changes here won't affect the running SAR flow unless something starts
// invoking it.
public class SarGeneratorService
{
    private readonly Kernel _kernel;
    private readonly ITransactionFeatureStore _featureStore; // Update type to interface

    // Inject Kernel and TransactionFeatureStore via Dependency Injection
    public SarGeneratorService(Kernel kernel, ITransactionFeatureStore featureStore)
    {
        _kernel = kernel;
        _featureStore = featureStore;

        // Instantiate FraudInvestigationPlugin with the feature store and add it to the Kernel
        var plugin = new FraudInvestigationPlugin(featureStore);
        _kernel.Plugins.AddFromObject(plugin, "FraudTools");
    }

    public async Task<string> InvestigateAndGenerateSarAsync(
        string transactionId, 
        string accountId, 
        double amount, 
        double riskScore, 
        string currentLocation = "Unknown/External IP")
    {
        var systemPrompt = """
            You are an expert FINRA/SEC Anti-Money Laundering (AML) Compliance Agent.
            Your task is to investigate flagged high-risk financial transactions using your available tools.
            
            Instructions:
            1. Query real-time velocity metrics for the account using your available tool.
            2. Check geo-location anomalies for the transaction using your available tool.
            3. Synthesize the findings into an official, concise Suspicious Activity Report (SAR).
            4. Include specific quantitative metrics (60-second window, 1-hour volume, amount, risk score) and clear regulatory risk justifications.
            """;

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(
            $"Investigate Flagged Transaction '{transactionId}' for Account '{accountId}'. " +
            $"Transaction Amount: ${amount:F2}. Anomaly Score: {riskScore:F2}. Location: '{currentLocation}'."
        );

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Enable automatic function calling so Semantic Kernel automatically executes FraudInvestigationPlugin tools
        var executionSettings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: executionSettings,
            kernel: _kernel
        );

        return result.Content ?? "SAR Generation Failed: No response received from agent.";
    }
}