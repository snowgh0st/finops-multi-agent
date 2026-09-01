import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';

export interface TransactionEvent {
  transactionId: string;
  accountId: string;
  amount: number;
  currency: string;
  merchantCategory: string;
  location: string;
  timestamp: string;
  riskScore: number;
  isFlagged: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class RiskTelemetryService {
  private hubConnection!: signalR.HubConnection;
  private riskAlerts$ = new BehaviorSubject<TransactionEvent[]>([]);

  constructor() {
    this.startConnection();
    this.addRiskAlertListener();
  }

  private startConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/risk') // Points to .NET 8 RiskHub
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('SignalR WebSocket Connection Established'))
      .catch(err => console.error('Error while starting SignalR connection: ' + err));
  }

  private addRiskAlertListener(): void {
    this.hubConnection.on('ReceiveRiskAlert', (data: TransactionEvent) => {
      const currentAlerts = this.riskAlerts$.getValue();
      this.riskAlerts$.next([data, ...currentAlerts.slice(0, 49)]); // Keep last 50 events
    });
  }

  public getRiskAlerts(): Observable<TransactionEvent[]> {
    return this.riskAlerts$.asObservable();
  }
}