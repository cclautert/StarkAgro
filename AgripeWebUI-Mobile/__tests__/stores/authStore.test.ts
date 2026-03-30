import { act, renderHook } from '@testing-library/react-native';
import { useAuthStore } from '../../stores/authStore';

// Reset store state between tests using the store's own actions
beforeEach(async () => {
  const { result } = renderHook(() => useAuthStore());
  await act(async () => {
    await result.current.logout();
  });
  // Reset isHydrated back to false
  await act(async () => {
    useAuthStore.setState({ isHydrated: false });
  });

  const ss = require('expo-secure-store');
  ss.__reset?.();
});

describe('authStore', () => {
  it('starts with null token and isHydrated=false', () => {
    const { result } = renderHook(() => useAuthStore());
    expect(result.current.token).toBeNull();
    expect(result.current.userId).toBeNull();
    expect(result.current.userName).toBeNull();
    expect(result.current.isHydrated).toBe(false);
  });

  it('setAuth stores token, userId and userName', async () => {
    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.setAuth('my-token', 42, 'João');
    });
    expect(result.current.token).toBe('my-token');
    expect(result.current.userId).toBe(42);
    expect(result.current.userName).toBe('João');
  });

  it('logout clears token, userId and userName', async () => {
    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.setAuth('my-token', 42, 'João');
    });
    await act(async () => {
      await result.current.logout();
    });
    expect(result.current.token).toBeNull();
    expect(result.current.userId).toBeNull();
    expect(result.current.userName).toBeNull();
  });

  it('hydrate sets isHydrated=true and restores persisted token', async () => {
    const SecureStore = require('expo-secure-store');
    await SecureStore.setItemAsync('agripeweb_token', 'persisted-token');

    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.hydrate();
    });
    expect(result.current.isHydrated).toBe(true);
    expect(result.current.token).toBe('persisted-token');
  });

  it('hydrate sets token to null when nothing is stored', async () => {
    const { result } = renderHook(() => useAuthStore());
    await act(async () => {
      await result.current.hydrate();
    });
    expect(result.current.isHydrated).toBe(true);
    expect(result.current.token).toBeNull();
  });
});
