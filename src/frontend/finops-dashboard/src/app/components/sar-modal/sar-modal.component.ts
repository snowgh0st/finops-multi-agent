import { Component, input, output, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SarReport {
  transactionId: string;
  accountId: string;
  amount: number;
  riskScore: number;
  timestamp: string;
  generatedSarText: string;
}

@Component({
  selector: 'app-sar-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sar-modal.component.html',
})
export class SarModalComponent {
  // Signal inputs & outputs (Angular 18 modern syntax)
  report = input<SarReport | null>(null);
  isOpen = input<boolean>(false);
  closeModal = output<void>();

  copied = signal<boolean>(false);

  // Formatted risk badge helper
  riskSeverityClass = computed(() => {
    const score = this.report()?.riskScore ?? 0;
    if (score >= 0.85) return 'bg-red-500/10 text-red-400 border-red-500/30';
    if (score >= 0.6) return 'bg-amber-500/10 text-amber-400 border-amber-500/30';
    return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
  });

  onClose() {
    this.copied.set(false);
    this.closeModal.emit();
  }

  async copyToClipboard() {
    const content = this.report()?.generatedSarText;
    if (content) {
      await navigator.clipboard.writeText(content);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    }
  }
}