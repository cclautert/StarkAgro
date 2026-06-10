export const OFFLINE_STORAGE_KEYS = {
  pivots: 'agripeweb:offline:pivots',
  dashboard: (pivotId: number, days: number) => `agripeweb:offline:dashboard:${pivotId}:${days}`,
  syncQueue: 'agripeweb:offline:sync-queue',
  conflictLog: 'agripeweb:offline:conflict-log',
  syncedItemIds: 'agripeweb:offline:synced-item-ids',
} as const;
