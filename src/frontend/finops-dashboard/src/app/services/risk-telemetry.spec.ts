import { TestBed } from '@angular/core/testing';
import { RiskTelemetry } from './risk-telemetry';

describe('RiskTelemetry', () => {
  let service: RiskTelemetry;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RiskTelemetry);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
