using FinOps.Core.Plugins;
using FinOps.Infrastructure.Cache;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FinOps.Infrastructure.Services;

public class SarGeneratorService
{
    private readonly Kernel _kernel;

    // Inject Kernel and TransactionFeatureStore via Dependency Injection
    public SarGeneratorService(Kernel kernel, TransactionFeatureStore featureStore)
    {
        _kernel = kernel;

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