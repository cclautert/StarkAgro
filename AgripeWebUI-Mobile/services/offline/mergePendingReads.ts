import { ReadEntry } from '../types/api';
import { SyncQueueItem } from './syncQueue';

export function mergePendingReads(
  serverReadings: ReadEntry[],
  queueItems: SyncQueueItem[],
  sensorId: number,
  quadrante: number
): ReadEntry[] {
  const pending = queueItems
    .filter(
      (item): item is Extract<SyncQueueItem, { type: 'manual_read' }> =>
        item.type === 'manual_read' &&
        (item.status === 'pending' || item.status === 'error') &&
        item.payload.sensorId === sensorId &&
        item.payload.quadrante === quadrante
    )
    .map((item) => ({
      id: -Math.abs(item.id.length),
      sensorId: item.payload.sensorId,
      value: item.payload.value,
      date: item.payload.recordedAt,
      pendingSync: true,
      localQueueId: item.id,
    }));

  const merged = [...serverReadings];
  for (const pendingRead of pending) {
    const duplicate = merged.some(
      (entry) =>
        Math.abs(entry.value - pendingRead.value) < 0.01 &&
        Math.abs(new Date(entry.date).getTime() - new Date(pendingRead.date).getTime()) < 60000
    );
    if (!duplicate) {
      merged.push(pendingRead);
    }
  }

  return merged.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
}
