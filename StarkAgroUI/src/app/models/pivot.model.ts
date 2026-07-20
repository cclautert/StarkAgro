import { Quadrante } from "./quadrante.model";

export interface Pivot {
  id: number;
  name: string;
  quadrante?: Quadrante;
  limiteInferior?: number;
  limiteSuperior?: number;
  rainThresholdMm?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  altitude?: number | null;
  locationAddress?: string | null;
  locationUpdatedAt?: string | null;
}

export interface PivotLocation {
  latitude: number;
  longitude: number;
  altitude: number | null;
  locationAddress: string | null;
}
