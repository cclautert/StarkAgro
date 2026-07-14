import api from './api';

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

export interface DiagnosisContext {
  pivotName?: string | null;
  moistureAvg7d?: number | null;
  limiteSuperior?: number | null;
  daysAboveUpperLimit: number;
  openAnomalies: number;
  forecastSummary?: string | null;
}

export interface DiagnosisSignature {
  agronomistName: string;
  crea?: string | null;
  signedAt: string;
}

export interface PlantDiagnosis extends PlantDiagnosisSummary {
  producerNotes?: string | null;
  capturedAt: string;
  diseases: DiseaseSuggestion[];
  context?: DiagnosisContext | null;
  aiReportMarkdown?: string | null;
  agronomistReportMarkdown?: string | null;
  prescription?: string | null;
  rejectionReason?: string | null;
  signature?: DiagnosisSignature | null;
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

export interface UploadDiagnosisInput {
  /** URI local da foto tirada pela câmera (file://...). */
  localUri: string;
  pivotId?: number | null;
  cropName?: string | null;
  notes?: string | null;
}

export const diagnosisService = {
  /**
   * Envia a foto. O React Native monta o multipart a partir do URI local do arquivo —
   * não é preciso ler os bytes na memória, o que importa para foto de celular (2–8 MB).
   */
  async upload(input: UploadDiagnosisInput): Promise<{ id: number; status: PlantDiagnosisStatus }> {
    const form = new FormData();

    form.append('image', {
      uri: input.localUri,
      name: `laudo-${Date.now()}.jpg`,
      type: 'image/jpeg',
    } as unknown as Blob);

    if (input.pivotId) form.append('pivotId', String(input.pivotId));
    if (input.cropName) form.append('cropName', input.cropName);
    if (input.notes) form.append('notes', input.notes);

    const res = await api.post<{ id: number; status: PlantDiagnosisStatus }>('diagnosis', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      // A foto sobe pelo 4G do campo: o timeout padrão de 15s é curto demais.
      timeout: 60000,
    });

    return res.data;
  },

  async getAll(): Promise<PlantDiagnosisSummary[]> {
    const res = await api.get<PlantDiagnosisSummary[]>('diagnosis');
    return res.data;
  },

  async getById(id: number): Promise<PlantDiagnosis> {
    const res = await api.get<PlantDiagnosis>(`diagnosis/${id}`);
    return res.data;
  },

  async getQuota(): Promise<DiagnosisQuota> {
    const res = await api.get<DiagnosisQuota>('diagnosis/quota');
    return res.data;
  },
};
