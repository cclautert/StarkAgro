import AsyncStorage from '@react-native-async-storage/async-storage';
import { offlineCache } from '../../../services/offline/offlineCache';
import { OFFLINE_STORAGE_KEYS } from '../../../services/offline/storageKeys';
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

  it('getPivots devolve null quando não há nada em cache', async () => {
    await expect(offlineCache.getPivots()).resolves.toBeNull();
  });

  it('getDashboard devolve null quando não há nada em cache', async () => {
    await expect(offlineCache.getDashboard(9, 7)).resolves.toBeNull();
  });

  it('getPivots devolve null (em vez de estourar) se o cache estiver corrompido', async () => {
    await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.pivots, '{ isto não é json válido');
    await expect(offlineCache.getPivots()).resolves.toBeNull();
  });

  it('getDashboard devolve null (em vez de estourar) se o cache estiver corrompido', async () => {
    await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.dashboard(1, 7), 'lixo}{');
    await expect(offlineCache.getDashboard(1, 7)).resolves.toBeNull();
  });
});
