export interface Anomaly {
  id: number;
  sensorId: number;
  readSensorId: number;
  value: number;
  expectedMin: number;
  expectedMax: number;
  date: string;
  acknowledged: boolean;
}
