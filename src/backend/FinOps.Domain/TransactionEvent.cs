namespace FinOps.Domain;

public record TransactionEvent(
    string TransactionId,
    string AccountId,
    decimal Amount,
    string Currency,
    string MerchantCategory,
    string Location,
    DateTime Timestamp,
    double RiskScore = 0.0,
    bool IsFlagged = false
);