import AsyncStorage from '@react-native-async-storage/async-storage';
import { readsService } from '../readsService';
import { conflictLog } from './conflictLog';
import { OFFLINE_STORAGE_KEYS } from './storageKeys';

export interface ManualReadPayload {
  sensorCode: string;
  sensorId: number;
  pivotId: number;
  quadrante: number;
  value: number;
  recordedAt: string;
}

export interface PhotoPayload {
  localUri: string;
  sensorId: number;
  pivotId: number;
  quadrante: number;
  capturedAt: string;
}

export type SyncQueueItem =
  | {
      id: string;
      type: 'manual_read';
      status: 'pending' | 'syncing' | 'error';
      retries: number;
      createdAt: string;
      payload: ManualReadPayload;
    }
  | {
      id: string;
      type: 'photo';
      status: 'pending' | 'syncing' | 'error';
      retries: number;
      createdAt: string;
      payload: PhotoPayload;
    };

export type SyncQueueListener = (items: SyncQueueItem[]) => void;

const listeners = new Set<SyncQueueListener>();
let processing = false;

function notify(items: SyncQueueItem[]) {
  listeners.forEach((listener) => listener(items));
}

async function readSyncedIds(): Promise<Set<string>> {
  const raw = await AsyncStorage.getItem(OFFLINE_STORAGE_KEYS.syncedItemIds);
  if (!raw) return new Set();
  try {
    return new Set(JSON.parse(raw) as string[]);
  } catch {
    return new Set();
  }
}

async function markSynced(id: string): Promise<void> {
  const ids = await readSyncedIds();
  ids.add(id);
  await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.syncedItemIds, JSON.stringify([...ids]));
}

export const syncQueue = {
  subscribe(listener: SyncQueueListener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  async getAll(): Promise<SyncQueueItem[]> {
    const raw = await AsyncStorage.getItem(OFFLINE_STORAGE_KEYS.syncQueue);
    if (!raw) return [];
    try {
      return JSON.parse(raw) as SyncQueueItem[];
    } catch {
      return [];
    }
  },

  async save(items: SyncQueueItem[]): Promise<void> {
    await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.syncQueue, JSON.stringify(items));
    notify(items);
  },

  async enqueueManualRead(payload: ManualReadPayload): Promise<SyncQueueItem> {
    const items = await this.getAll();
    const item: SyncQueueItem = {
      id: `read-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      type: 'manual_read',
      status: 'pending',
      retries: 0,
      createdAt: new Date().toISOString(),
      payload,
    };
    items.push(item);
    await this.save(items);
    return item;
  },

  async enqueuePhoto(payload: PhotoPayload): Promise<SyncQueueItem> {
    const items = await this.getAll();
    const item: SyncQueueItem = {
      id: `photo-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      type: 'photo',
      status: 'pending',
      retries: 0,
      createdAt: new Date().toISOString(),
      payload,
    };
    items.push(item);
    await this.save(items);
    return item;
  },

  async getPendingCount(): Promise<number> {
    const items = await this.getAll();
    return items.filter((item) => item.status === 'pending' || item.status === 'error').length;
  },

  async processQueue(): Promise<{ synced: number; failed: number; skipped: number }> {
    if (processing) return { synced: 0, failed: 0, skipped: 0 };
    processing = true;

    let synced = 0;
    let failed = 0;
    let skipped = 0;

    try {
      const syncedIds = await readSyncedIds();
      let items = await this.getAll();

      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        if (item.status === 'syncing') continue;
        if (syncedIds.has(item.id)) {
          items = items.filter((entry) => entry.id !== item.id);
          skipped += 1;
          continue;
        }

        if (item.type === 'photo') {
          // Photo upload API not yet available — keep queued for a future backend contract.
          skipped += 1;
          continue;
        }

        items[i] = { ...item, status: 'syncing' };
        await this.save(items);

        try {
          const latest = await readsService.getLatestBySensorId(
            item.payload.sensorId,
            item.payload.quadrante
          );

          if (latest) {
            const localTime = new Date(item.payload.recordedAt).getTime();
            const serverTime = new Date(latest.date).getTime();

            if (serverTime > localTime) {
              await conflictLog.append({
                type: 'manual_read',
                localId: item.id,
                sensorId: item.payload.sensorId,
                localValue: item.payload.value,
                serverValue: latest.value,
                localRecordedAt: item.payload.recordedAt,
                serverRecordedAt: latest.date,
                resolution: 'server_wins',
              });
              await markSynced(item.id);
              items = items.filter((entry) => entry.id !== item.id);
              synced += 1;
              await this.save(items);
              continue;
            }

            if (
              serverTime === localTime ||
              (Math.abs(latest.value - item.payload.value) < 0.01 && serverTime >= localTime - 60000)
            ) {
              await markSynced(item.id);
              items = items.filter((entry) => entry.id !== item.id);
              skipped += 1;
              await this.save(items);
              continue;
            }
          }

          await readsService.createManualRead({
            code: item.payload.sensorCode,
            value: item.payload.value,
          });

          if (latest && Math.abs(latest.value - item.payload.value) > 0.01) {
            await conflictLog.append({
              type: 'manual_read',
              localId: item.id,
              sensorId: item.payload.sensorId,
              localValue: item.payload.value,
              serverValue: latest.value,
              localRecordedAt: item.payload.recordedAt,
              serverRecordedAt: latest.date,
              resolution: 'local_wins',
            });
          }

          await markSynced(item.id);
          items = items.filter((entry) => entry.id !== item.id);
          synced += 1;
        } catch {
          items[i] = {
            ...item,
            status: 'error',
            retries: item.retries + 1,
          };
          failed += 1;
        }

        await this.save(items);
      }
    } finally {
      processing = false;
    }

    return { synced, failed, skipped };
  },
};
