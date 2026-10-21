namespace FinOps.Api.Workers;

using FinOps.Api.Hubs;
using FinOps.Domain.Models;
using FinOps.Infrastructure.Cache;
using FinOps.Infrastructure.Evaluators;
using FinOps.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinOps.Core.Interfaces;

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
                var featureStore = scope.ServiceProvider.GetRequiredService<ITransactionFeatureStore>();
                var evaluator = scope.ServiceProvider.GetRequiredService<FraudModelEvaluator>();
                var sarService = scope.ServiceProvider.GetRequiredService<SarGeneratorService>();

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

                // 6. Trigger autonomous SAR investigation pipeline for extreme risk anomalies (>= 0.85)
                if (sampleEvent.AnomalyScore >= 0.85)
                {
                    _logger.LogWarning("Extreme risk detected ({Score}). Initiating Semantic Kernel SAR generation...", sampleEvent.AnomalyScore);

                    string sarReport = await sarService.InvestigateAndGenerateSarAsync(
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