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
  symptoms?: string | null;
  treatments?: string[];
}

export interface DiagnosisSignature {
  agronomistName: string;
  crea?: string | null;
  signedAt: string;
  contentSha256: string;
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

  /** Laudo redigido pela IA. Imutável — a edição do agrônomo vive em outro campo. */
  aiReportMarkdown?: string | null;
  aiProvider?: string | null;

  /** Nome do produtor — só vem na visão do agrônomo. */
  clientName?: string | null;

  agronomistReportMarkdown?: string | null;
  confirmedDisease?: string | null;
  agronomistSeverity?: string | null;
  prescription?: string | null;
  rejectionReason?: string | null;
  signature?: DiagnosisSignature | null;

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

export interface DiagnosisAuditEntry {
  at: string;
  actorUserId?: number | null;
  actorName?: string | null;
  fromStatus?: string | null;
  toStatus: string;
  action: string;
}

export interface DiagnosisHistoryItem {
  id: number;
  status: PlantDiagnosisStatus;
  capturedAt: string;
  topDisease?: string | null;
  topProbability: number;
  confirmedDisease?: string | null;
  severity?: string | null;
  moistureAvg7d?: number | null;
  daysAboveUpperLimit: number;
  isSigned: boolean;
  imageUrl: string;
}

export interface DiagnosisHistory {
  pivotId: number;
  pivotName?: string | null;
  items: DiagnosisHistoryItem[];

  /** "a mancha piorou desde 12/03" — a pergunta que um app de foto avulsa não responde. */
  trend?: string | null;
}

export interface DiagnosisQuota {
  /** 0 = ilimitado. */
  limit: number;
  used: number;
  remaining: number;
  isUnlimited: boolean;
  isExhausted: boolean;
  resetsAt: string;
}
