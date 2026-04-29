export interface User {
  id: number;
  name: string;
  email: string;
  password: string;
  active: boolean;
  limiteInferior?: number;
  limiteSuperior?: number;
  rainThresholdMm?: number | null;
}
