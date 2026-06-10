import { useEffect, useState, useCallback } from 'react';
import { networkMonitor } from '../services/offline/networkMonitor';
import { syncQueue, SyncQueueItem } from '../services/offline/syncQueue';

export function useOfflineSync(onSynced?: () => void) {
  const [pendingCount, setPendingCount] = useState(0);
  const [isSyncing, setIsSyncing] = useState(false);
  const [queueItems, setQueueItems] = useState<SyncQueueItem[]>([]);

  const refreshQueue = useCallback(async () => {
    const items = await syncQueue.getAll();
    setQueueItems(items);
    setPendingCount(items.filter((item) => item.status === 'pending' || item.status === 'error').length);
  }, []);

  const runSync = useCallback(async () => {
    if (!networkMonitor.getIsOnline()) return;
    setIsSyncing(true);
    try {
      const result = await syncQueue.processQueue();
      await refreshQueue();
      if (result.synced > 0) {
        onSynced?.();
      }
    } finally {
      setIsSyncing(false);
    }
  }, [onSynced, refreshQueue]);

  useEffect(() => {
    void networkMonitor.init();
    void refreshQueue();

    const unsubNetwork = networkMonitor.subscribe((online) => {
      if (online) {
        void runSync();
      }
    });

    const unsubQueue = syncQueue.subscribe((items) => {
      setQueueItems(items);
      setPendingCount(items.filter((item) => item.status === 'pending' || item.status === 'error').length);
    });

    if (networkMonitor.getIsOnline()) {
      void runSync();
    }

    return () => {
      unsubNetwork();
      unsubQueue();
    };
  }, [refreshQueue, runSync]);

  return { pendingCount, isSyncing, queueItems, runSync, refreshQueue };
}
