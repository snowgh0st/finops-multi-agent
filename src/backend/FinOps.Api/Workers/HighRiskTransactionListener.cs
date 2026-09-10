namespace FinOps.Api.Workers;

using FinOps.Api.Hubs;
using FinOps.Domain.Models;
using FinOps.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                // 1. Generate randomized sample transaction event
                var txId = $"TX-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                var accountId = $"ACC-{Random.Shared.Next(1000, 9999)}";
                var amount = Math.Round((decimal)(Random.Shared.NextDouble() * 145000 + 5000), 2);
                var anomalyScore = Math.Round(Random.Shared.NextDouble() * 0.5 + 0.5, 2); // 0.50 to 1.00

                var sampleEvent = CreateSampleEvent(txId, accountId, amount, anomalyScore);

                // 2. Emit alert payload over SignalR to Angular dashboard
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveHighRiskAlert",
                    sampleEvent,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation("Sent transaction alert over SignalR: {Id} | Risk: {Score}", 
                    sampleEvent.TransactionId, 
                    sampleEvent.AnomalyScore);

                // 3. Trigger autonomous SAR investigation pipeline only for extreme risk anomalies (>= 0.92)
                if (sampleEvent.AnomalyScore >= 0.92)
                {
                    _logger.LogWarning("Extreme risk detected ({Score}). Initiating Semantic Kernel SAR generation...", sampleEvent.AnomalyScore);

                    using var scope = _serviceProvider.CreateScope();
                    var sarService = scope.ServiceProvider.GetRequiredService<SarGeneratorService>();

                    string sarReport = await sarService.InvestigateAndGenerateSarAsync(
                        sampleEvent.TransactionId,
                        sampleEvent.AccountId,
                        (double)sampleEvent.Amount,
                        sampleEvent.AnomalyScore
                    );

                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveSarReport",
                        sarReport,
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