export interface AdminUser {
  id: number;
  name: string;
  email: string;
  active: boolean;
  isAdmin: boolean;
  isAgronomist?: boolean;
  agronomistCrea?: string | null;
  limiteInferior?: number;
  limiteSuperior?: number;
  rainThresholdMm?: number | null;
  geminiApiKey?: string | null;
  uplinkIntervalSeconds?: number | null;
}
