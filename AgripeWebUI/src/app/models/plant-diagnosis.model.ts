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

export interface PlantDiagnosis extends PlantDiagnosisSummary {
  producerNotes?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  capturedAt: string;
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
