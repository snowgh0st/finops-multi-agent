import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RiskTelemetryService, TransactionEvent } from './services/risk-telemetry';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-6 font-sans">
      <!-- Header Bar -->
      <header class="flex justify-between items-center pb-6 border-b border-slate-800 mb-6">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-ping"></span>
            FinOps Multi-Agent Risk Monitor
          </h1>
          <p class="text-sm text-slate-400 mt-1">Real-Time Fraud Telemetry & Semantic Kernel Agent Stream</p>
        </div>
        <div class="flex gap-4">
          <div class="bg-slate-900 border border-slate-800 rounded-lg px-4 py-2 text-right">
            <span class="text-xs text-slate-500 block uppercase font-mono">Engine Status</span>
            <span class="text-sm font-semibold text-emerald-400">ACTIVE / SIGNALR</span>
          </div>
        </div>
      </header>

      <!-- Grid Layout -->
      <main class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Alert Feed (2 Columns) -->
        <section class="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-2xl">
          <h2 class="text-lg font-semibold text-white mb-4 flex items-center justify-between">
            Streaming Transactions
            <span class="text-xs font-normal text-slate-400">Auto-updating WebSocket Feed</span>
          </h2>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm text-slate-300">
              <thead class="text-xs text-slate-400 uppercase bg-slate-950/50 border-b border-slate-800">
                <tr>
                  <th class="p-3">Tx ID</th>
                  <th class="p-3">Account</th>
                  <th class="p-3">Amount</th>
                  <th class="p-3">Merchant</th>
                  <th class="p-3">Risk Score</th>
                  <th class="p-3 text-right">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/50 font-mono">
                <tr *ngFor="let tx of riskAlerts$ | async" class="hover:bg-slate-800/30 transition-colors">
                  <td class="p-3 text-slate-400">{{ tx.transactionId.substring(0, 8) }}...</td>
                  <td class="p-3 font-medium text-white">{{ tx.accountId }}</td>
                  <td class="p-3">{{ tx.amount | currency:tx.currency }}</td>
                  <td class="p-3 text-slate-400">{{ tx.merchantCategory }}</td>
                  <td class="p-3">
                    <span [class]="tx.riskScore > 0.7 ? 'text-red-400 font-bold' : 'text-emerald-400'">
                      {{ tx.riskScore | number:'1.2-2' }}
                    </span>
                  </td>
                  <td class="p-3 text-right">
                    <span *ngIf="tx.isFlagged" class="bg-red-500/10 text-red-400 border border-red-500/30 px-2 py-0.5 rounded text-xs font-sans">
                      FLAGGED
                    </span>
                    <span *ngIf="!tx.isFlagged" class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded text-xs font-sans">
                      CLEAR
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Agent Decisions Side Panel (1 Column) -->
        <section class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-2xl">
          <h2 class="text-lg font-semibold text-white mb-4">Autonomous Agent Logs</h2>
          <div class="space-y-4">
            <div class="bg-slate-950 border border-slate-800 rounded-lg p-4 font-mono text-xs text-slate-300">
              <div class="text-slate-500 mb-1">[Semantic Kernel] Tool Call Triggered</div>
              <p class="text-amber-400 font-semibold mb-2">&gt; FraudInvestigationPlugin.FreezeAccountAsync</p>
              <p class="text-slate-400">Account ACC-9821 hold applied. High frequency anomaly detected in 60s window.</p>
            </div>
          </div>
        </section>
      </main>
    </div>
  `
})
export class AppComponent implements OnInit {
  private riskTelemetryService = inject(RiskTelemetryService);
  riskAlerts$!: Observable<TransactionEvent[]>;

  ngOnInit(): void {
    this.riskAlerts$ = this.riskTelemetryService.getRiskAlerts();
  }
}