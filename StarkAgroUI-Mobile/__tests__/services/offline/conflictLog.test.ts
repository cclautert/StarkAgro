import AsyncStorage from '@react-native-async-storage/async-storage';
import { conflictLog } from '../../../services/offline/conflictLog';

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('conflictLog', () => {
  it('appends conflict entries', async () => {
    const entry = await conflictLog.append({
      type: 'manual_read',
      localId: 'local-1',
      sensorId: 10,
      localValue: 42,
      serverValue: 40,
      localRecordedAt: '2026-06-05T10:00:00.000Z',
      serverRecordedAt: '2026-06-05T11:00:00.000Z',
      resolution: 'server_wins',
    });

    const all = await conflictLog.getAll();
    expect(all).toHaveLength(1);
    expect(all[0].id).toBe(entry.id);
    expect(all[0].resolution).toBe('server_wins');
  });
});
