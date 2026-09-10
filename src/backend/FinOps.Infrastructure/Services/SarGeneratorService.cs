using FinOps.Core.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FinOps.Infrastructure.Services;

public class SarGeneratorService
{
    private readonly Kernel _kernel;

    public SarGeneratorService(Kernel kernel)
    {
        _kernel = kernel;
        _kernel.Plugins.AddFromType<FraudInvestigationPlugin>("FraudTools");
    }

    public async Task<string> InvestigateAndGenerateSarAsync(string transactionId, string accountId, double amount, double riskScore)
    {
        var systemPrompt = """
            You are a FINRA/SEC Lead Anti-Money Laundering (AML) Compliance Agent.
            Your job is to investigate flagged transactions using your available tools and generate an official Suspicious Activity Report (SAR).
            Be precise, professional, and highlight specific risk indicators (geo-velocity, amount thresholds, anomaly scores).
            """;

        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage($"Investigate Flagged Transaction '{transactionId}' for Account '{accountId}'. Amount: ${amount}. Anomaly Score: {riskScore}.");

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        
        // Execute loop with automatic tool calling enabled
        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel: _kernel
        );

        return result.Content ?? "SAR Generation Failed.";
    }
}