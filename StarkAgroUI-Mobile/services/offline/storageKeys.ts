export const OFFLINE_STORAGE_KEYS = {
  pivots: 'starkagro:offline:pivots',
  dashboard: (pivotId: number, days: number) => `starkagro:offline:dashboard:${pivotId}:${days}`,
  syncQueue: 'starkagro:offline:sync-queue',
  conflictLog: 'starkagro:offline:conflict-log',
  syncedItemIds: 'starkagro:offline:synced-item-ids',
} as const;
