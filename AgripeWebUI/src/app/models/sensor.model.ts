import { Pivot } from "./pivot.model";

export interface Sensor {
  id: number;
  name: string;
  pivot: Pivot;
  quadrante: number;
  code: string;
}
