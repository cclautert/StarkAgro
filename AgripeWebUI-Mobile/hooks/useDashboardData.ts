import { useState, useEffect, useCallback } from 'react';
import { readsService } from '../services/readsService';
import { sensorService } from '../services/sensorService';
import { GetReadByPivotIdResponse, ReadEntry } from '../types/api';

export interface QuadrantChartData {
  quadrante: number;
  readings: ReadEntry[];
}

export interface DashboardData {
  pivot: GetReadByPivotIdResponse | null;
  chartData: QuadrantChartData[];
  loading: boolean;
  error: string | null;
}

export function useDashboardData(pivotId: number | null, numberOfDays: number = 7): DashboardData & { refresh: () => void } {
  const [pivot, setPivot] = useState<GetReadByPivotIdResponse | null>(null);
  const [chartData, setChartData] = useState<QuadrantChartData[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!pivotId) return;
    try {
      setLoading(true);
      setError(null);

      // Fetch pivot with quadrant averages
      const pivotData = await readsService.getByPivotId(pivotId, numberOfDays);
      setPivot(pivotData);

      // Fetch time-series for each quadrant (1–4)
      const quadrantReadings = await Promise.all(
        [1, 2, 3, 4].map(async (q) => {
          try {
            const sensors = await sensorService.getAllByPivotId(pivotId, q);
            if (!sensors.length) return { quadrante: q, readings: [] };
            const readings = await readsService.getAllByPivotId(sensors[0].id, q, numberOfDays);
            return { quadrante: q, readings };
          } catch {
            return { quadrante: q, readings: [] };
          }
        })
      );
      setChartData(quadrantReadings);
    } catch {
      setError('Erro ao carregar dados do dashboard');
    } finally {
      setLoading(false);
    }
  }, [pivotId, numberOfDays]);

  useEffect(() => {
    load();
    const interval = setInterval(load, 60000);
    return () => clearInterval(interval);
  }, [load]);

  return { pivot, chartData, loading, error, refresh: load };
}
