import { useEffect, useState } from 'react';
import { networkMonitor } from '../services/offline/networkMonitor';

export function useNetworkStatus(): boolean {
  const [isOnline, setIsOnline] = useState(networkMonitor.getIsOnline());

  useEffect(() => {
    return networkMonitor.subscribe(setIsOnline);
  }, []);

  return isOnline;
}
