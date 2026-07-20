export interface Read {
  //id: number;
  sensorId: number;
  value: number;
  date: Date;
  isEdgeAnomaly?: boolean;
  edgeDetectedAt?: string;
  humidity?: number | null;
  temperature?: number | null;
  batteryVoltage?: number | null;
}
