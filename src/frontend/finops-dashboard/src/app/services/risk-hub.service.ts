import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

export interface RiskAlert {
  transactionId: string;
  accountId: string;
  amount: number;
  riskScore: number;
  location: string;
  timestamp: string;
  agentSummary?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RiskHubService {
  private hubConnection?: signalR.HubConnection;

  // Angular Signals for real-time reactive state
  public isConnected = signal<boolean>(false);
  public alerts = signal<RiskAlert[]>([]);
  public activeInvestigation = signal<string | null>(null);

  constructor() {
    this.startConnection();
  }

  public startConnection() {
    // Matches the port 5001 mapping and hub path configured in backend Program.cs
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5001/hubs/risk', {
        withCredentials: true
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => {
        this.isConnected.set(true);
        console.log('[SignalR] Connected successfully to RiskHub');
        this.registerListeners();
      })
      .catch(err => console.error('[SignalR] Connection failed: ', err));
  }

  private registerListeners() {
    // Listen for incoming real-time risk alerts from the background worker
    this.hubConnection?.on('ReceiveRiskAlert', (alert: RiskAlert) => {
      this.alerts.update(current => [alert, ...current]);
    });

    // Listen for live multi-agent investigation updates
    this.hubConnection?.on('ReceiveAgentUpdate', (statusMessage: string) => {
      this.activeInvestigation.set(statusMessage);
    });
  }
}