using Microsoft.AspNetCore.SignalR;
using FinOps.Domain;

namespace FinOps.Api;

public class TransactionSimulator : BackgroundService
{
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly Random _random = new();

    public TransactionSimulator(IHubContext<RiskHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var riskScore = _random.NextDouble();
            var tx = new TransactionEvent(
                TransactionId: Guid.NewGuid().ToString(),
                AccountId: $"ACC-{_random.Next(1000, 9999)}",
                Amount: (decimal)Math.Round(_random.NextDouble() * 500, 2),
                Currency: "USD",
                MerchantCategory: "Online Retail",
                Location: "New York, US",
                Timestamp: DateTime.UtcNow,
                RiskScore: riskScore,
                IsFlagged: riskScore > 0.7
            );

            await _hubContext.Clients.All.SendAsync("ReceiveRiskAlert", tx, stoppingToken);
            await Task.Delay(2000, stoppingToken); // Broadcast every 2s
        }
    }
}