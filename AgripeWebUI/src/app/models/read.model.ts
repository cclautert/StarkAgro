export interface Read {
  //id: number;
  sensorId: number;
  value: number;
  date: Date;
  isEdgeAnomaly?: boolean;
  edgeDetectedAt?: string;
}
