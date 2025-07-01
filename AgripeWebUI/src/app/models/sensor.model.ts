import { Pivot } from "./pivot.model";

export interface Sensor {
  id: number;
  pivot: Pivot;
  quadrante: number;
  code: string;
}
