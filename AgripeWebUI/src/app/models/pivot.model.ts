import { Quadrante } from "./quadrante.model";

export interface Pivot {
  id: number;
  name: string;
  quadrante?: Quadrante;
  limiteInferior?: number;
  limiteSuperior?: number;
}
