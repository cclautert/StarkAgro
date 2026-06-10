import AsyncStorage from '@react-native-async-storage/async-storage';
import { offlineCache } from '../../../services/offline/offlineCache';
import { Pivot } from '../../types/api';

const pivots: Pivot[] = [{ id: 1, name: 'Pivô A' }];

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('offlineCache', () => {
  it('stores and retrieves pivots', async () => {
    await offlineCache.setPivots(pivots);
    await expect(offlineCache.getPivots()).resolves.toEqual(pivots);
  });

  it('stores and retrieves dashboard data', async () => {
    const pivot = { id: 1, name: 'Pivô A', quadrante: {} };
    const chartData = [{ quadrante: 1, readings: [] }];
    await offlineCache.setDashboard(1, 7, pivot, chartData);
    const cached = await offlineCache.getDashboard(1, 7);
    expect(cached?.pivot).toEqual(pivot);
    expect(cached?.chartData).toEqual(chartData);
    expect(cached?.cachedAt).toBeTruthy();
  });
});
