export interface SensorTelemetry {
  quadrante: number;
  deviceEui: string;
  humidity: number | null;
  temperature: number | null;
  batteryVoltage: number | null;
  batteryPercent: number | null;
  readAt: string | null;
}
