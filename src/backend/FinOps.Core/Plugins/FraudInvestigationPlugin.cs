namespace FinOps.Core.Plugins;

using System.ComponentModel;
using FinOps.Core.Interfaces;
using Microsoft.SemanticKernel;

public class FraudInvestigationPlugin
{
    private readonly ITransactionFeatureStore _featureStore;

    // Inject the Redis Feature Store interface into the plugin
    public FraudInvestigationPlugin(ITransactionFeatureStore featureStore)
    {
        _featureStore = featureStore;
    }

    [KernelFunction, Description("Gets real-time transaction velocity metrics for an account from the Redis Feature Store over specified time windows.")]
    public async Task<string> GetAccountVelocityAsync(
        [Description("The account ID to inspect")] string accountId)
    {
        // 1. Query Redis for short-term 60-second window
        var (shortWindowCount, shortWindowTotal) = await _featureStore.GetWindowMetricsAsync(accountId, windowSeconds: 60);

        // 2. Query Redis for medium-term 1-hour window (3600 seconds)
        var (hourlyCount, hourlyTotal) = await _featureStore.GetWindowMetricsAsync(accountId, windowSeconds: 3600);

        return $"""
            Real-Time Redis Velocity Profile for Account '{accountId}':
            - Last 60 Seconds: {shortWindowCount} transactions totaling ${shortWindowTotal:F2}
            - Last 1 Hour: {hourlyCount} transactions totaling ${hourlyTotal:F2}
            """;
    }

    [KernelFunction, Description("Checks if the transaction location deviates significantly from historical patterns or established home region.")]
    public async Task<string> CheckGeoAnomalyAsync(
        [Description("The account ID")] string accountId,
        [Description("Current IP address or geo-location")] string currentLocation)
    {
        // Simulated geo-profile lookup
        return $"Account '{accountId}' primary registered location is US-East (New York). Current transaction originating from unusual location/IP near: {currentLocation}.";
    }
}