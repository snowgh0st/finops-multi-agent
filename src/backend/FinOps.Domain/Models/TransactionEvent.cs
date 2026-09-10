namespace FinOps.Domain.Models;

public record TransactionEvent(
    string TransactionId,
    string AccountId,
    decimal Amount,
    string Merchant,
    string Category,
    string Location,
    DateTime Timestamp,
    double AnomalyScore = 0.0,
    bool IsHighRisk = false
);