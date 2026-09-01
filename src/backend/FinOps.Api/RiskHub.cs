using Microsoft.AspNetCore.SignalR;
using FinOps.Domain;

namespace FinOps.Api;

public class RiskHub : Hub
{
    // Allows clients (Angular UI) to subscribe to real-time risk telemetry streams
    public async Task BroadcastRiskAlert(TransactionEvent txEvent)
    {
        await Clients.All.SendAsync("ReceiveRiskAlert", txEvent);
    }
}