import AsyncStorage from '@react-native-async-storage/async-storage';
import { GetReadByPivotIdResponse, Pivot } from '../../types/api';
import { QuadrantChartData } from '../../hooks/useDashboardData';
import { OFFLINE_STORAGE_KEYS } from './storageKeys';

interface CachedDashboard {
  pivot: GetReadByPivotIdResponse;
  chartData: QuadrantChartData[];
  cachedAt: string;
}

export const offlineCache = {
  async getPivots(): Promise<Pivot[] | null> {
    const raw = await AsyncStorage.getItem(OFFLINE_STORAGE_KEYS.pivots);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as Pivot[];
    } catch {
      return null;
    }
  },

  async setPivots(pivots: Pivot[]): Promise<void> {
    await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.pivots, JSON.stringify(pivots));
  },

  async getDashboard(pivotId: number, days: number): Promise<CachedDashboard | null> {
    const raw = await AsyncStorage.getItem(OFFLINE_STORAGE_KEYS.dashboard(pivotId, days));
    if (!raw) return null;
    try {
      return JSON.parse(raw) as CachedDashboard;
    } catch {
      return null;
    }
  },

  async setDashboard(
    pivotId: number,
    days: number,
    pivot: GetReadByPivotIdResponse,
    chartData: QuadrantChartData[]
  ): Promise<void> {
    const payload: CachedDashboard = {
      pivot,
      chartData,
      cachedAt: new Date().toISOString(),
    };
    await AsyncStorage.setItem(
      OFFLINE_STORAGE_KEYS.dashboard(pivotId, days),
      JSON.stringify(payload)
    );
  },
};
