import { ReadEntry } from '../types/api';

export interface TrendPoint {
  date: string;
  value: number;
  movingAvg?: number;
  trend?: number;
}

export interface ProjectionPoint {
  date: string;
  projMin: number;
  projMax: number;
  projMid: number;
}

export interface TrendStats {
  slope: number;
  intercept: number;
  avg: number;
  min: number;
  max: number;
  last: number;
  proj5: number;
  alertCount: number;
  compliancePct: number;
  variability: number;
}

export function toDayLabel(date: Date): string {
  const day = date.getUTCDate().toString().padStart(2, '0');
  const month = (date.getUTCMonth() + 1).toString().padStart(2, '0');
  return `${day}/${month}`;
}

export function linearRegression(data: { value: number }[]): { slope: number; intercept: number } {
  const n = data.length;
  if (n < 2) {
    return { slope: 0, intercept: data[0]?.value ?? 0 };
  }
  const sumX = data.reduce((s, _, i) => s + i, 0);
  const sumY = data.reduce((s, d) => s + d.value, 0);
  const sumXY = data.reduce((s, d, i) => s + i * d.value, 0);
  const sumXX = data.reduce((s, _, i) => s + i * i, 0);
  const denom = n * sumXX - sumX * sumX;
  if (denom === 0) {
    return { slope: 0, intercept: sumY / n };
  }
  const slope = (n * sumXY - sumX * sumY) / denom;
  const interceptVal = (sumY - slope * sumX) / n;
  return {
    slope: parseFloat(slope.toFixed(4)),
    intercept: parseFloat(interceptVal.toFixed(4)),
  };
}

export function movingAverage(data: TrendPoint[], window: number): TrendPoint[] {
  return data.map((d, i) => {
    const slice = data.slice(Math.max(0, i - window + 1), i + 1);
    const avg = slice.reduce((s, x) => s + x.value, 0) / slice.length;
    return { ...d, movingAvg: parseFloat(avg.toFixed(1)) };
  });
}

export function buildProjection(
  slope: number,
  intercept: number,
  n: number,
  projDays: number
): ProjectionPoint[] {
  const result: ProjectionPoint[] = [];
  for (let p = 1; p <= projDays; p++) {
    const rawValue = intercept + slope * (n - 1 + p);
    const clamped = Math.max(0, Math.min(100, rawValue));
    const margin = Math.min(p * 2.5, 15);
    result.push({
      date: `+${p}d`,
      projMin: parseFloat((clamped - margin).toFixed(1)),
      projMax: parseFloat((clamped + margin).toFixed(1)),
      projMid: parseFloat(clamped.toFixed(1)),
    });
  }
  return result;
}

export function computeDailyData(
  reads: ReadEntry[],
  limiteInferior: number,
  limiteSuperior: number
): { points: TrendPoint[]; projection: ProjectionPoint[]; stats: TrendStats } {
  const empty: TrendStats = {
    slope: 0, intercept: 0, avg: 0, min: 0, max: 0,
    last: 0, proj5: 0, alertCount: 0, compliancePct: 100, variability: 0,
  };

  if (!reads || reads.length === 0) {
    return { points: [], projection: [], stats: empty };
  }

  // Sort ascending
  const sorted = [...reads].sort(
    (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
  );

  // Group by UTC calendar day — keep last reading per day
  const byDay = new Map<string, ReadEntry>();
  for (const r of sorted) {
    const label = toDayLabel(new Date(r.date));
    const existing = byDay.get(label);
    if (!existing || new Date(r.date) >= new Date(existing.date)) {
      byDay.set(label, r);
    }
  }

  const sortedKeys = Array.from(byDay.keys());
  const dailyValues = sortedKeys.map((k) => byDay.get(k)!.value);

  let points: TrendPoint[] = sortedKeys.map((date, i) => ({ date, value: dailyValues[i] }));

  points = movingAverage(points, 3);

  const { slope, intercept } = linearRegression(points);

  points = points.map((p, i) => ({
    ...p,
    trend: parseFloat((intercept + slope * i).toFixed(1)),
  }));

  const projection = buildProjection(slope, intercept, points.length, 5);

  // Stats over all raw reads
  const allValues = reads.map((r) => r.value);
  const sum = allValues.reduce((a, b) => a + b, 0);
  const avg = parseFloat((sum / allValues.length).toFixed(1));
  const min = parseFloat(Math.min(...allValues).toFixed(1));
  const max = parseFloat(Math.max(...allValues).toFixed(1));
  const last = dailyValues[dailyValues.length - 1] ?? 0;
  const alertCount = allValues.filter((v) => v < limiteInferior || v > limiteSuperior).length;
  const compliancePct = parseFloat(
    (allValues.length > 0
      ? ((allValues.length - alertCount) / allValues.length) * 100
      : 100
    ).toFixed(0)
  );
  const variability = parseFloat((max - min).toFixed(1));
  const proj5 =
    projection[4]?.projMid ??
    parseFloat((intercept + slope * (points.length + 4)).toFixed(1));

  const stats: TrendStats = {
    slope, intercept, avg, min, max, last, proj5, alertCount, compliancePct, variability,
  };

  return { points, projection, stats };
}
