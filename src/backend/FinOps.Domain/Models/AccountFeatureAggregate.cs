using Amazon.DynamoDBv2.DataModel;

// One row per (AccountId, WindowBucket) - WindowBucket is a 1-minute-resolution
// timestamp string ("yyyy-MM-dd-HH-mm"), so each row is a running total for that
// single minute. Written/read by DynamoDbFeatureStore; see that file for how
// this fits into the overall feature-store architecture.
[DynamoDBTable("AccountFeatureAggregates")]
public class AccountFeatureAggregate
{
    [DynamoDBHashKey("AccountId")]
    public string AccountId { get; set; } = string.Empty;

    [DynamoDBRangeKey("WindowBucket")]
    public string WindowBucket { get; set; } = string.Empty; // e.g., "2026-10-21-18-45"

    [DynamoDBProperty("TransactionCount")]
    public int TransactionCount { get; set; }

    [DynamoDBProperty("TotalAmount")]
    public decimal TotalAmount { get; set; }

    [DynamoDBProperty("MaxRiskScore")]
    public double MaxRiskScore { get; set; }

    [DynamoDBProperty("LastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}