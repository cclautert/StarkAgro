import { useEffect } from 'react';
import { networkMonitor } from '../services/offline/networkMonitor';
import { syncQueue } from '../services/offline/syncQueue';

/** Initializes offline sync at app root — auto-sync on reconnect. */
export function useOfflineBootstrap() {
  useEffect(() => {
    void networkMonitor.init();

    const unsub = networkMonitor.subscribe((online) => {
      if (online) {
        void syncQueue.processQueue();
      }
    });

    if (networkMonitor.getIsOnline()) {
      void syncQueue.processQueue();
    }

    return unsub;
  }, []);
}
