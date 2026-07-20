export interface ReadEntry {
  value: number;
  date: string;
}

export interface Quadrante {
  topLeft: string;
  topLeftAvg?: number;
  topLeftReads?: ReadEntry[];
  topRight: string;
  topRightAvg?: number;
  topRightReads?: ReadEntry[];
  bottomLeft: string;
  bottomLeftAvg?: number;
  bottomLeftReads?: ReadEntry[];
  bottomRight: string;
  bottomRightAvg?: number;
  bottomRightReads?: ReadEntry[];
}
