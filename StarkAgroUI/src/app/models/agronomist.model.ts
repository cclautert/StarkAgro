import { PlantDiagnosisStatus } from './plant-diagnosis.model';

export interface AgronomistQueueItem {
  id: number;
  status: PlantDiagnosisStatus;
  clientUserId: number;
  clientName?: string | null;
  pivotName?: string | null;
  cropName?: string | null;
  topDisease?: string | null;
  topProbability: number;
  createdAt: string;
  reviewStartedAt?: string | null;
  imageUrl: string;
}

export type AgronomistClientStatus = 'Pending' | 'Active' | 'Declined' | 'Revoked' | 'Expired';

export interface AgronomistClient {
  id: number;
  clientUserId?: number | null;
  clientEmail: string;
  clientName?: string | null;
  status: AgronomistClientStatus;
  invitedAt: string;
  inviteExpiresAt: string;
  acceptedAt?: string | null;
  pendingDiagnoses: number;
}

export interface AgronomistInvite {
  id: number;
  agronomistId: number;
  agronomistName?: string | null;
  agronomistCrea?: string | null;
  invitedAt: string;
  inviteExpiresAt: string;
}

export interface SignPayload {
  reportMarkdown: string;
  confirmedDisease?: string | null;
  severity?: string | null;
  prescription?: string | null;
  crea?: string | null;
}
