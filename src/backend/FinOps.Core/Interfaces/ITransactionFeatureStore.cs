namespace FinOps.Core.Interfaces;

public interface ITransactionFeatureStore
{
    Task TrackTransactionAsync(string accountId, double amount, long timestampSeconds);
    Task<(int Count, double Total)> GetWindowMetricsAsync(string accountId, int windowSeconds);
}