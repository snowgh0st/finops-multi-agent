namespace FinOps.Api.Hubs;

using FinOps.Domain.Models;
using Microsoft.AspNetCore.SignalR;

public class RiskHub : Hub
{
    public async Task BroadcastTransaction(TransactionEvent transaction)
    {
        await Clients.All.SendAsync("ReceiveTransactionAlert", transaction);
    }
}