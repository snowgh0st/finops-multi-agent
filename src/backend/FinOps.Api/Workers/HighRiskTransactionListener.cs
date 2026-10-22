namespace FinOps.Api.Workers;

using FinOps.Api.Hubs;
using FinOps.Domain.Models;
using FinOps.Infrastructure.Agents;
using FinOps.Infrastructure.Cache;
using FinOps.Infrastructure.Evaluators;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// CORE HOT PATH: this background service is the heart of the demo/CUJ.
// Every ~4 seconds it synthesizes one sample transaction, scores it, persists
// features, and pushes it to every connected dashboard client. There is no
// real transaction ingestion (no queue, no API endpoint) - ExecuteAsync below
// is both the producer and the processor. If the dashboard looks "frozen",
// check this loop and the SignalR connection first.
public class HighRiskTransactionListener : BackgroundService
{
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HighRiskTransactionListener> _logger;

    public HighRiskTransactionListener(
        IHubContext<RiskHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<HighRiskTransactionListener> logger)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(".NET HighRiskTransactionListener background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var featureStore = scope.ServiceProvider.GetRequiredService<TransactionFeatureStore>(); // Redis
                var dynamoFeatureStore = scope.ServiceProvider.GetRequiredService<IFeatureStore>(); // DynamoDB
                var evaluator = scope.ServiceProvider.GetRequiredService<FraudModelEvaluator>();
                var multiAgentCoordinator = scope.ServiceProvider.GetRequiredService<MultiAgentRiskCoordinator>();

                // 1. Generate randomized sample transaction event
                var txId = $"TX-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                var accountId = $"ACC-{Random.Shared.Next(1000, 9999)}";
                var amount = Math.Round((decimal)(Random.Shared.NextDouble() * 145000 + 5000), 2);
                bool isGeoAnomaly = Random.Shared.NextDouble() > 0.7; // 30% chance of geo-anomaly

                // 2. Track transaction in Redis Feature Store
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await featureStore.TrackTransactionAsync(accountId, (double)amount, timestamp);

                // 3. Fetch real-time sliding-window metrics from Redis
                var (count60s, total60s) = await featureStore.GetWindowMetricsAsync(accountId, windowSeconds: 60);
                var (count1h, total1h) = await featureStore.GetWindowMetricsAsync(accountId, windowSeconds: 3600);

                // 4. Score transaction using local ONNX model evaluator (< 1 ms latency)
                double anomalyScore = evaluator.PredictRiskScore(
                    amount: (double)amount,
                    count60s: count60s,
                    total60s: total60s,
                    count1h: count1h,
                    total1h: total1h,
                    isGeoAnomaly: isGeoAnomaly
                );

                anomalyScore = Math.Round(anomalyScore, 2);

                // 4b. Track transaction in DynamoDB Persistent Feature Store.
                // Runs on a background thread because the AWS SDK's first DynamoDB Local
                // call can block synchronously (not just slowly), which would otherwise
                // defeat an inline WaitAsync and stall this entire loop indefinitely.
                try
                {
                    await Task.Run(() => dynamoFeatureStore.RecordTransactionAsync(accountId, amount, anomalyScore))
                        .WaitAsync(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("DynamoDB feature store write skipped due to connectivity: {Message}", ex.Message);
                }

                var sampleEvent = CreateSampleEvent(txId, accountId, amount, anomalyScore);

                // 5. Emit alert payload over SignalR to Angular dashboard
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveHighRiskAlert",
                    sampleEvent,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation("Sent transaction alert over SignalR: {Id} | Score: {Score} | Redis 60s Count: {Count}", 
                    sampleEvent.TransactionId, 
                    sampleEvent.AnomalyScore,
                    count60s);

                // 6. Trigger Multi-Agent Risk Coordinator pipeline for extreme risk anomalies (>= 0.85).
                // This 0.85 threshold is the CUJ branch point between "just show an
                // alert" and "run the full LLM investigation + SAR report" - the
                // expensive/slow path. It is independent of the 0.75 IsHighRisk flag
                // set in CreateSampleEvent, which only affects dashboard styling.
                if (sampleEvent.AnomalyScore >= 0.85)
                {
                    _logger.LogWarning("Extreme risk detected ({Score}). Initiating Multi-Agent Risk Coordinator investigation...", sampleEvent.AnomalyScore);

                    string sarReport = await multiAgentCoordinator.ExecuteMultiAgentInvestigationAsync(
                        sampleEvent.TransactionId,
                        sampleEvent.AccountId,
                        (double)sampleEvent.Amount,
                        sampleEvent.AnomalyScore,
                        sampleEvent.Location
                    );

                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveSarReport",
                        new
                        {
                            transactionId = sampleEvent.TransactionId,
                            accountId = sampleEvent.AccountId,
                            amount = sampleEvent.Amount,
                            riskScore = sampleEvent.AnomalyScore,
                            generatedSarText = sarReport,
                            timestamp = DateTime.UtcNow.ToString("o")
                        },
                        cancellationToken: stoppingToken
                    );
                }

                await Task.Delay(4000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // SAD PATH: any unexpected failure (Redis down, DynamoDB unreachable,
                // OpenAI call throwing, etc.) is swallowed here so one bad iteration
                // doesn't kill the whole background service. The loop logs and
                // retries after a longer back-off instead of crashing the worker.
                _logger.LogError(ex, "Error in HighRiskTransactionListener loop.");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation(".NET HighRiskTransactionListener background worker stopped.");
    }

    private static TransactionEvent CreateSampleEvent(string id, string account, decimal amount, double anomalyScore)
    {
        return new TransactionEvent(
            TransactionId: id,
            AccountId: account,
            Amount: amount,
            Merchant: "High-Risk External Transfer",
            Category: "Wire Transfer",
            Location: "Cayman Islands, KY",
            Timestamp: DateTime.UtcNow,
            AnomalyScore: anomalyScore,
            IsHighRisk: anomalyScore >= 0.75
        );
    }
}