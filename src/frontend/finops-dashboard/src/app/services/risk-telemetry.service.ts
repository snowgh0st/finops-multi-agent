import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface TransactionAlert {
  transactionId: string;
  accountId: string;
  amount: number;
  anomalyScore?: number;
  riskScore?: number;
  timestamp: string;
  isHighRisk?: boolean;
}

export interface SarReport {
  transactionId: string;
  accountId: string;
  amount: number;
  riskScore: number;
  timestamp: string;
  generatedSarText: string;
}

@Injectable({
  providedIn: 'root'
})
export class RiskTelemetryService {
  private hubConnection!: signalR.HubConnection;

  // RxJS stream for component subscriptions
  public alerts$ = new Subject<TransactionAlert>();

  // Public Signals for direct template binding
  public transactions = signal<TransactionAlert[]>([]);
  public connectionState = signal<'Connected' | 'Connecting' | 'Disconnected'>('Disconnected');
  public activeSarReport = signal<SarReport | null>(null);
  public isSarModalOpen = signal<boolean>(false);

  public startConnection(): void {
  if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
    return;
  }

  this.connectionState.set('Connecting');

  this.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5114/hubs/risk', {
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets
    })
    .withAutomaticReconnect()
    .build();

  // 1. Register handlers FIRST so no incoming messages are dropped
  this.registerEventHandlers();

  // 2. Start connection SECOND
  this.hubConnection
    .start()
    .then(() => {
      console.log('✅ [SignalR] Connected to Telemetry Hub');
      this.connectionState.set('Connected');
    })
    .catch((err) => {
      console.error('❌ [SignalR] Connection Failed:', err);
      this.connectionState.set('Disconnected');
    });
  }

  private registerEventHandlers(): void {
    this.hubConnection.on('ReceiveHighRiskAlert', (data: any) => {
      console.log('⚡ [SignalR] Raw Payload Received:', data);

      const alert: TransactionAlert = {
        transactionId: data.transactionId ?? data.TransactionId ?? `TX-${Math.random().toString(36).substring(2, 8).toUpperCase()}`,
        accountId: data.accountId ?? data.AccountId ?? 'ACC-UNKNOWN',
        amount: data.amount ?? data.Amount ?? 0,
        anomalyScore: data.anomalyScore ?? data.AnomalyScore ?? data.riskScore ?? data.RiskScore ?? 0,
        riskScore: data.riskScore ?? data.RiskScore ?? data.anomalyScore ?? data.AnomalyScore ?? 0,
        timestamp: data.timestamp ?? data.Timestamp ?? new Date().toISOString()
      };

      // 1. Emit through RxJS subject
      this.alerts$.next(alert);

      // 2. Prepend to top of signal array for UI binding
      this.transactions.update((prev) => [alert, ...prev]);
    });

    this.hubConnection.on('ReceiveSarReport', (reportText: string) => {
      console.log('📄 [SignalR] SAR Report Received:', reportText);
      this.activeSarReport.set({
        transactionId: 'TX-HIGH-RISK',
        accountId: 'ACC-SUSPECT',
        amount: 125000.00,
        riskScore: 0.89,
        timestamp: new Date().toISOString(),
        generatedSarText: reportText
      });
      this.isSarModalOpen.set(true);
    });
  }

  public closeSarModal(): void {
    this.isSarModalOpen.set(false);
  }
}