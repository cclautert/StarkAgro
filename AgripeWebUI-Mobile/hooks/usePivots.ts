import { useState, useEffect, useCallback } from 'react';
import { Pivot } from '../types/api';
import { pivotService } from '../services/pivotService';

export function usePivots() {
  const [pivots, setPivots] = useState<Pivot[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await pivotService.getAll();
      setPivots(data);
    } catch {
      setError('Erro ao carregar pivôs');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { pivots, loading, error, refresh: load };
}
