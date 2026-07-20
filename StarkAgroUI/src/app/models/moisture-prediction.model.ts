export interface PredictedMoisturePoint {
  date: string;
  predictedMoisture: number;
  confidenceMin: number;
  confidenceMax: number;
}

export interface MoisturePrediction {
  pivotId: number;
  predictedValues: PredictedMoisturePoint[];
  estimatedCriticalAt: string | null;
  confidence: number;
  dataPointsUsed: number;
}
