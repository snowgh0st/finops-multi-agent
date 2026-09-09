using FinOps.Api.Hubs;
using FinOps.Domain.Models;
using FinOps.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;

namespace FinOps.Api.Workers;

public class HighRiskTransactionListener : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly ILogger<HighRiskTransactionListener> _logger;
    
    // Risk threshold to trigger an AI investigation
    private const double HighRiskThreshold = 0.85;

    public HighRiskTransactionListener(
        IServiceProvider serviceProvider,
        IHubContext<RiskHub> hubContext,
        ILogger<HighRiskTransactionListener> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("High-Risk Transaction Agent Listener initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Simulate reading processed evaluated transactions from channel / queue
                var transaction = await GetNextEvaluatedTransactionAsync(stoppingToken);

                // 2. Check if transaction breaches anomaly threshold
                if (transaction != null && transaction.AnomalyScore >= HighRiskThreshold)
                {
                    _logger.LogWarning(
                        "High risk detected for Tx {TransactionId} (Score: {Score}). Triggering AI Agent...",
                        transaction.TransactionId,
                        transaction.AnomalyScore);

                    // Create a scoped service lifetime to resolve scoped dependencies
                    using var scope = _serviceProvider.CreateScope();
                    var sarService = scope.ServiceProvider.GetRequiredService<SarGeneratorService>();

                    // 3. Trigger Semantic Kernel Multi-Agent Investigation
                    string sarReportText = await sarService.InvestigateAndGenerateSarAsync(
                        transaction.TransactionId,
                        transaction.AccountId,
                        transaction.Amount,
                        transaction.AnomalyScore
                    );

                    var sarPayload = new
                    {
                        SarId = Guid.NewGuid().ToString(),
                        TransactionId = transaction.TransactionId,
                        AccountId = transaction.AccountId,
                        Amount = transaction.Amount,
                        RiskScore = transaction.AnomalyScore,
                        ReportBody = sarReportText,
                        GeneratedAt = DateTime.UtcNow
                    };

                    // 4. Broadcast live SAR Alert to connected Angular dashboard via SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveSarReport", sarPayload, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error processing high-risk transaction event.");
            }

            // Polling/delay interval between queue reads
            await Task.Delay(2000, stoppingToken);
        }
    }

    private static async Task<TransactionEvent?> GetNextEvaluatedTransactionAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        
        // Mocking high-risk event feed for demonstration
        var random = new Random();
        double score = Math.Round(random.NextDouble(), 2);

        // Inject high risk score periodically
        if (score > 0.70) score = 0.92; 

        return new TransactionEvent
        {
            TransactionId = $"TX-{random.Next(100000, 999999)}",
            AccountId = $"ACC-{random.Next(1000, 9999)}",
            Amount = random.Next(5000, 50000),
            AnomalyScore = score,
            Timestamp = DateTime.UtcNow
        };
    }
}