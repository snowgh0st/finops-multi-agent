namespace FinOps.Core.Interfaces;

// Redis-backed sliding-window velocity store (see TransactionFeatureStore for the
// implementation). This is the hot-path feature source the ONNX model and the
// FraudInvestigationPlugin tools both read from.
public interface ITransactionFeatureStore
{
    // Records a transaction so it counts toward future sliding-window lookups.
    Task TrackTransactionAsync(string accountId, double amount, long timestampSeconds);

    // Returns (transaction count, total amount) for an account within the last
    // `windowSeconds` seconds, e.g. 60 for a 60s window or 3600 for 1 hour.
    Task<(int Count, double Total)> GetWindowMetricsAsync(string accountId, int windowSeconds);
}