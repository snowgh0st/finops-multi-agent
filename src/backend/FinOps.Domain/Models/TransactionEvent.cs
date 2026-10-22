namespace FinOps.Domain.Models;

// The payload broadcast to the Angular dashboard over SignalR
// ("ReceiveHighRiskAlert") and consumed by RiskTelemetryService on the
// frontend. AnomalyScore is the ONNX/heuristic fraud score (0-1); IsHighRisk is
// a display-only flag set at >= 0.75 in HighRiskTransactionListener and is
// independent of the >= 0.85 threshold that triggers the full SAR
// investigation.
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