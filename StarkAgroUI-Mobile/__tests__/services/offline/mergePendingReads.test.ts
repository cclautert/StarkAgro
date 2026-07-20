import { mergePendingReads } from '../../../services/offline/mergePendingReads';
import { SyncQueueItem } from '../../../services/offline/syncQueue';

describe('mergePendingReads', () => {
  it('adds pending queue reads with badge metadata', () => {
    const queue: SyncQueueItem[] = [
      {
        id: 'read-1',
        type: 'manual_read',
        status: 'pending',
        retries: 0,
        createdAt: '2026-06-05T12:00:00.000Z',
        payload: {
          sensorCode: 'S1',
          sensorId: 10,
          pivotId: 1,
          quadrante: 2,
          value: 55,
          recordedAt: '2026-06-05T12:00:00.000Z',
        },
      },
    ];

    const merged = mergePendingReads([], queue, 10, 2);
    expect(merged).toHaveLength(1);
    expect(merged[0].pendingSync).toBe(true);
    expect(merged[0].value).toBe(55);
  });

  it('does not duplicate near-identical server reads', () => {
    const queue: SyncQueueItem[] = [
      {
        id: 'read-1',
        type: 'manual_read',
        status: 'pending',
        retries: 0,
        createdAt: '2026-06-05T12:00:00.000Z',
        payload: {
          sensorCode: 'S1',
          sensorId: 10,
          pivotId: 1,
          quadrante: 2,
          value: 55,
          recordedAt: '2026-06-05T12:00:00.000Z',
        },
      },
    ];

    const merged = mergePendingReads(
      [{ id: 1, sensorId: 10, value: 55, date: '2026-06-05T12:00:05.000Z' }],
      queue,
      10,
      2
    );
    expect(merged).toHaveLength(1);
    expect(merged[0].pendingSync).toBeUndefined();
  });
});
