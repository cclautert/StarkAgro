import { useState, useEffect, useCallback } from 'react';
import { Pivot } from '../types/api';
import { pivotService } from '../services/pivotService';
import { offlineCache } from '../services/offline/offlineCache';
import { networkMonitor } from '../services/offline/networkMonitor';

export function usePivots() {
  const [pivots, setPivots] = useState<Pivot[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fromCache, setFromCache] = useState(false);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setFromCache(false);

      if (networkMonitor.getIsOnline()) {
        try {
          const data = await pivotService.getAll();
          setPivots(data);
          await offlineCache.setPivots(data);
          return;
        } catch {
          // Fall back to cache below.
        }
      }

      const cached = await offlineCache.getPivots();
      if (cached?.length) {
        setPivots(cached);
        setFromCache(true);
        setError(networkMonitor.getIsOnline() ? 'Usando cache — falha ao atualizar pivôs' : 'Sem conexão — dados em cache');
      } else {
        setError('Erro ao carregar pivôs');
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void networkMonitor.init();
    load();
  }, [load]);

  return { pivots, loading, error, fromCache, refresh: load };
}
