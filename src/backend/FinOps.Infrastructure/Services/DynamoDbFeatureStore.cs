using Amazon.DynamoDBv2.DataModel;

public interface IFeatureStore
{
    Task RecordTransactionAsync(string accountId, decimal amount, double riskScore);
    Task<AccountFeatureAggregate?> GetWindowMetricsAsync(string accountId, string windowBucket);
}

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