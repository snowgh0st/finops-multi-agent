using StackExchange.Redis;

namespace FinOps.Infrastructure.Cache;

public class TransactionFeatureStore
{
    private readonly IDatabase _redis;

    public TransactionFeatureStore(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task TrackTransactionAsync(string accountId, double amount, long timestampUnix)
    {
        var key = $"account:{accountId}:velocity";

        // Add transaction to sorted set with timestamp as score
        await _redis.SortedSetAddAsync(key, $"{timestampUnix}:{amount}", timestampUnix);

        // Trim transactions older than 1 hour (3600 seconds)
        var cutoff = timestampUnix - 3600;
        await _redis.SortedSetRemoveRangeByScoreAsync(key, 0, cutoff);
    }

    public async Task<(int count, double totalAmount)> GetWindowMetricsAsync(string accountId, int windowSeconds)
    {
        var key = $"account:{accountId}:velocity";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startTime = now - windowSeconds;

        var entries = await _redis.SortedSetRangeByScoreAsync(key, startTime, now);

        int count = entries.Length;
        double total = 0;

        foreach (var entry in entries)
        {
            var parts = entry.ToString().Split(':');
            if (parts.Length == 2 && double.TryParse(parts[1], out var amt))
            {
                total += amt;
            }
        }

        return (count, total);
    }
}