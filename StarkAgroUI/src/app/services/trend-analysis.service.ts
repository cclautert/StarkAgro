import { Injectable } from '@angular/core';
import { Read } from '../models/read.model';

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

@Injectable({ providedIn: 'root' })
export class TrendAnalysisService {

  /**
   * Main entry point. Groups raw reads by UTC calendar day (last reading per day),
   * applies moving average and linear regression, and builds a 5-day forward projection.
   */
  computeDailyData(
    reads: Read[],
    limiteInferior: number,
    limiteSuperior: number
  ): { points: TrendPoint[]; projection: ProjectionPoint[]; stats: TrendStats } {

    if (!reads || reads.length === 0) {
      const empty: TrendStats = {
        slope: 0, intercept: 0, avg: 0, min: 0, max: 0,
        last: 0, proj5: 0, alertCount: 0, compliancePct: 100, variability: 0
      };
      return { points: [], projection: [], stats: empty };
    }

    // Sort ascending by date so the map preserves chronological insertion order
    const sorted = [...reads].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

    // Group by UTC calendar day — keep only the last reading per day
    const byDay = new Map<string, Read>();
    for (const r of sorted) {
      const d = new Date(r.date);
      const label = this.toDayLabel(d);
      const existing = byDay.get(label);
      if (!existing || new Date(r.date) >= new Date(existing.date)) {
        byDay.set(label, r);
      }
    }

    const sortedKeys = Array.from(byDay.keys());
    const dailyValues = sortedKeys.map(k => byDay.get(k)!.value);

    // Build base points
    let points: TrendPoint[] = sortedKeys.map((date, i) => ({
      date,
      value: dailyValues[i]
    }));

    // Apply moving average (window = 3)
    points = this.movingAverage(points, 3);

    // Linear regression
    const { slope, intercept } = this.linearRegression(points);

    // Annotate trend values
    points = points.map((p, i) => ({
      ...p,
      trend: parseFloat((intercept + slope * i).toFixed(1))
    }));

    // Projection
    const projection = this.buildProjection(slope, intercept, points.length, 5);

    // Stats — computed over all daily values (not just grouped; use all raw reads for alert count)
    const allValues = reads.map(r => r.value);
    const sum = allValues.reduce((a, b) => a + b, 0);
    const avg = parseFloat((sum / allValues.length).toFixed(1));
    const min = parseFloat(Math.min(...allValues).toFixed(1));
    const max = parseFloat(Math.max(...allValues).toFixed(1));
    const last = dailyValues[dailyValues.length - 1] ?? 0;
    const alertCount = allValues.filter(v => v < limiteInferior || v > limiteSuperior).length;
    const compliancePct = parseFloat(
      (allValues.length > 0 ? ((allValues.length - alertCount) / allValues.length) * 100 : 100).toFixed(0)
    );
    const variability = parseFloat((max - min).toFixed(1));
    const proj5 = projection[4]?.projMid ?? parseFloat((intercept + slope * (points.length + 4)).toFixed(1));

    const stats: TrendStats = {
      slope,
      intercept,
      avg,
      min,
      max,
      last,
      proj5,
      alertCount,
      compliancePct,
      variability
    };

    return { points, projection, stats };
  }

  /**
   * Ordinary least squares linear regression over an array of {value} objects.
   * Returns slope and intercept. Falls back to slope=0 when fewer than 2 points.
   */
  linearRegression(data: { value: number }[]): { slope: number; intercept: number } {
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
      intercept: parseFloat(interceptVal.toFixed(4))
    };
  }

  /**
   * Trailing moving average. The first (window-1) points use a partial window
   * (average of available preceding points), matching the mockup behaviour.
   */
  movingAverage(data: TrendPoint[], window: number): TrendPoint[] {
    return data.map((d, i) => {
      const slice = data.slice(Math.max(0, i - window + 1), i + 1);
      const avg = slice.reduce((s, x) => s + x.value, 0) / slice.length;
      return { ...d, movingAvg: parseFloat(avg.toFixed(1)) };
    });
  }

  /**
   * Builds projDays projection points starting one day after the last historical index (n-1).
   * Confidence margin formula: Math.min(p * 2.5, 15) where p is the number of days ahead.
   * The projected value is clamped to [0, 100].
   */
  buildProjection(
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
        projMid: parseFloat(clamped.toFixed(1))
      });
    }
    return result;
  }

  /**
   * Formats a Date as 'DD/MM' using UTC calendar fields.
   */
  toDayLabel(date: Date): string {
    const d = new Date(date);
    const day = d.getUTCDate().toString().padStart(2, '0');
    const month = (d.getUTCMonth() + 1).toString().padStart(2, '0');
    return `${day}/${month}`;
  }
}
