import { useEffect, useRef, useCallback } from 'react';
import { AppState, AppStateStatus } from 'react-native';
import { useRouter } from 'expo-router';
import { useAuthStore } from '../stores/authStore';

const TIMEOUT_MS = 10 * 60 * 1000; // 10 minutos

export function useIdleTimeout() {
  const { logout, token } = useAuthStore();
  const router = useRouter();
  const lastActivityRef = useRef(Date.now());
  const backgroundedAtRef = useRef<number | null>(null);

  const resetActivity = useCallback(() => {
    lastActivityRef.current = Date.now();
  }, []);

  const performLogout = useCallback(async () => {
    await logout();
    router.replace('/(auth)/login');
  }, [logout, router]);

  useEffect(() => {
    if (!token) return;

    // Verificação periódica de inatividade no foreground
    const interval = setInterval(() => {
      if (Date.now() - lastActivityRef.current > TIMEOUT_MS) {
        performLogout();
      }
    }, 60_000);

    // Detecção de background/foreground
    const handleAppStateChange = (nextState: AppStateStatus) => {
      if (nextState === 'background' || nextState === 'inactive') {
        backgroundedAtRef.current = Date.now();
      } else if (nextState === 'active') {
        if (backgroundedAtRef.current !== null) {
          if (Date.now() - backgroundedAtRef.current > TIMEOUT_MS) {
            performLogout();
          }
          backgroundedAtRef.current = null;
        }
        resetActivity();
      }
    };

    const subscription = AppState.addEventListener('change', handleAppStateChange);

    return () => {
      clearInterval(interval);
      subscription.remove();
    };
  }, [token, performLogout, resetActivity]);

  return { resetActivity };
}
