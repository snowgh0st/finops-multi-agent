import { TestBed } from '@angular/core/testing';
import { RiskTelemetryService } from './risk-telemetry.service';

describe('RiskTelemetryService', () => {
  let service: RiskTelemetryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RiskTelemetryService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
