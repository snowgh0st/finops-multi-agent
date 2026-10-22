using Amazon.DynamoDBv2.DataModel;

public interface IFeatureStore
{
    Task RecordTransactionAsync(string accountId, decimal amount, double riskScore);
    Task<AccountFeatureAggregate?> GetWindowMetricsAsync(string accountId, string windowBucket);
}

// Long-term, persistent companion to the Redis TransactionFeatureStore. Where
// Redis only keeps a rolling 1-hour window for live scoring, this aggregates
// per-account stats into 1-minute DynamoDB buckets (AccountFeatureAggregate)
// that survive restarts/eviction - useful for historical lookback, but not
// read by the live scoring path today (FraudModelEvaluator only reads Redis).
// HighRiskTransactionListener writes to this on a background Task.Run with a
// 5s timeout and swallows failures (sad path), so DynamoDB being unavailable
// degrades long-term analytics only, never the live fraud-scoring loop.
public class DynamoDbFeatureStore : IFeatureStore
{
    private readonly IDynamoDBContext _context;

    public DynamoDbFeatureStore(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task RecordTransactionAsync(string accountId, decimal amount, double riskScore)
    {
        // Define 1-minute window bucket for sliding window aggregation
        var windowBucket = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");

        // Retrieve existing record for this window or create a new one
        var record = await _context.LoadAsync<AccountFeatureAggregate>(accountId, windowBucket) 
                     ?? new AccountFeatureAggregate { AccountId = accountId, WindowBucket = windowBucket };

        record.TransactionCount += 1;
        record.TotalAmount += amount;
        record.MaxRiskScore = Math.Max(record.MaxRiskScore, riskScore);
        record.LastUpdated = DateTime.UtcNow;

        await _context.SaveAsync(record);
    }

    public async Task<AccountFeatureAggregate?> GetWindowMetricsAsync(string accountId, string windowBucket)
    {
        return await _context.LoadAsync<AccountFeatureAggregate>(accountId, windowBucket);
    }
}