export type PlantDiagnosisStatus =
  | 'Uploaded'
  | 'Processing'
  | 'PendingReview'
  | 'InReview'
  | 'AiCompleted'
  | 'Signed'
  | 'Rejected'
  | 'Failed';

export interface PlantDiagnosisSummary {
  id: number;
  status: PlantDiagnosisStatus;
  pivotId?: number | null;
  cropName?: string | null;
  createdAt: string;
  processedAt?: string | null;
  failureReason?: string | null;
}

export interface DiseaseSuggestion {
  name: string;
  scientificName?: string | null;
  probability: number;
  severity?: string | null;
}

/** Dados da lavoura congelados no laudo — é o diferencial contra um app de foto comum. */
export interface DiagnosisContext {
  pivotName?: string | null;
  moistureAvg7d?: number | null;
  limiteInferior?: number | null;
  limiteSuperior?: number | null;
  daysAboveUpperLimit: number;
  openAnomalies: number;
  irrigationAlerts7d: number;
  forecastSummary?: string | null;
}

export interface PlantDiagnosis extends PlantDiagnosisSummary {
  producerNotes?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  capturedAt: string;
  isPlant: boolean;
  topProbability: number;
  diseases: DiseaseSuggestion[];
  context?: DiagnosisContext | null;
  aiReportMarkdown?: string | null;
  aiProvider?: string | null;
  imageUrl: string;
}

export interface CreatePlantDiagnosisResult {
  id: number;
  status: PlantDiagnosisStatus;
  statusUrl: string;
}

export interface PlantDiagnosisStatusResult {
  id: number;
  status: PlantDiagnosisStatus;
  updatedAt: string;
  failureReason?: string | null;
}
