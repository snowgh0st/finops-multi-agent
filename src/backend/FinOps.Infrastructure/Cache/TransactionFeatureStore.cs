namespace FinOps.Infrastructure.Cache;

using FinOps.Core.Interfaces;
using StackExchange.Redis;

// Hot-path sliding-window velocity store backed by a single Redis sorted set
// per account: key "account:{id}:velocity", member "{timestamp}:{amount}",
// score = timestamp. Score-range queries give O(log N) window lookups without
// a separate index. Window data lives only as long as Redis keeps it (no TTL
// is set on the key itself) - entries older than 1 hour are pruned on every
// write, which also caps unbounded growth for long-lived accounts.
public class TransactionFeatureStore : ITransactionFeatureStore
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

    public async Task<(int Count, double Total)> GetWindowMetricsAsync(string accountId, int windowSeconds)
    {
        var key = $"account:{accountId}:velocity";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startTime = now - windowSeconds;

        var entries = await _redis.SortedSetRangeByScoreAsync(key, startTime, now);

        int count = entries.Length;
        double total = 0;

        foreach (var entry in entries)
        {
            // Member format is "{timestamp}:{amount}" (see TrackTransactionAsync).
            // Malformed/unparsable entries are silently skipped rather than
            // thrown - a bad entry degrades the total instead of breaking the
            // whole window query.
            var parts = entry.ToString().Split(':');
            if (parts.Length == 2 && double.TryParse(parts[1], out var amt))
            {
                total += amt;
            }
        }

        return (count, total);
    }
}