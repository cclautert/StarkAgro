import AsyncStorage from '@react-native-async-storage/async-storage';

const mockCreateManualRead = jest.fn();
const mockGetLatestBySensorId = jest.fn();

jest.mock('../../../services/readsService', () => ({
  readsService: {
    createManualRead: mockCreateManualRead,
    getLatestBySensorId: mockGetLatestBySensorId,
  },
}));

beforeEach(async () => {
  await AsyncStorage.clear();
  mockCreateManualRead.mockReset();
  mockGetLatestBySensorId.mockReset();
  jest.resetModules();
});

describe('syncQueue', () => {
  it('enqueues manual reads and syncs when online', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue(null);
    mockCreateManualRead.mockResolvedValue({ id: 99, sensorId: 10, userId: 1, value: 42 });

    const result = await syncQueue.processQueue();
    expect(result.synced).toBe(1);
    await expect(syncQueue.getAll()).resolves.toEqual([]);
  });

  it('applies last-write-wins and logs conflict when server is newer', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');
    const { conflictLog } = require('../../../services/offline/conflictLog');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue({
      id: 1,
      sensorId: 10,
      value: 50,
      date: '2026-06-05T11:00:00.000Z',
    });

    const result = await syncQueue.processQueue();
    expect(result.synced).toBe(1);
    expect(mockCreateManualRead).not.toHaveBeenCalled();

    const conflicts = await conflictLog.getAll();
    expect(conflicts).toHaveLength(1);
    expect(conflicts[0].resolution).toBe('server_wins');
  });

  it('does not duplicate when server already has matching read', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue({
      id: 1,
      sensorId: 10,
      value: 42,
      date: '2026-06-05T10:00:00.000Z',
    });

    const result = await syncQueue.processQueue();
    expect(result.skipped).toBe(1);
    expect(mockCreateManualRead).not.toHaveBeenCalled();
  });
});
