import { Pivot } from "./pivot.model";

export enum MetricType {
  Humidity = 0,
  Temperature = 1,
  Battery = 2
}

export interface Sensor {
  id: number;
  name: string;
  pivot: Pivot;
  quadrante: number;
  code: string;
  metricType?: MetricType;
}
