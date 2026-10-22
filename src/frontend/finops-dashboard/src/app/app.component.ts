import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SarModalComponent } from './components/sar-modal/sar-modal.component';
import { RiskTelemetryService } from './services/risk-telemetry.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, SarModalComponent],
  templateUrl: './app.component.html'
})
// Root component - the entry point of the dashboard CUJ. Opens the SignalR
// connection on init and otherwise just exposes RiskTelemetryService's signals
// to the template; it holds no state of its own.
export class AppComponent implements OnInit {
  public telemetryService = inject(RiskTelemetryService);

  // Expose signal directly for Angular @for loop in app.component.html
  public transactions = this.telemetryService.transactions;

  ngOnInit(): void {
    this.telemetryService.startConnection();
  }
}