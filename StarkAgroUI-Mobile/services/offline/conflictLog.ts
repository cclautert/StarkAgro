import AsyncStorage from '@react-native-async-storage/async-storage';
import { OFFLINE_STORAGE_KEYS } from './storageKeys';

export interface ConflictLogEntry {
  id: string;
  type: 'manual_read';
  localId: string;
  sensorId: number;
  localValue: number;
  serverValue: number;
  localRecordedAt: string;
  serverRecordedAt: string;
  resolution: 'server_wins' | 'local_wins';
  loggedAt: string;
}

export const conflictLog = {
  async getAll(): Promise<ConflictLogEntry[]> {
    const raw = await AsyncStorage.getItem(OFFLINE_STORAGE_KEYS.conflictLog);
    if (!raw) return [];
    try {
      return JSON.parse(raw) as ConflictLogEntry[];
    } catch {
      return [];
    }
  },

  async append(entry: Omit<ConflictLogEntry, 'id' | 'loggedAt'>): Promise<ConflictLogEntry> {
    const items = await this.getAll();
    const full: ConflictLogEntry = {
      ...entry,
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      loggedAt: new Date().toISOString(),
    };
    items.push(full);
    await AsyncStorage.setItem(OFFLINE_STORAGE_KEYS.conflictLog, JSON.stringify(items));
    return full;
  },
};
