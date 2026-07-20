import { renderHook, act } from '@testing-library/react-native';

// Prefixo `mock` é obrigatório para a fábrica do jest.mock poder referenciar (regra do Jest).
let mockOnline = true;
const mockListeners = new Set<(v: boolean) => void>();

jest.mock('../../services/offline/networkMonitor', () => ({
  networkMonitor: {
    getIsOnline: () => mockOnline,
    subscribe: (fn: (v: boolean) => void) => {
      mockListeners.add(fn);
      fn(mockOnline);
      return () => mockListeners.delete(fn);
    },
  },
}));

function emit(value: boolean) {
  mockOnline = value;
  mockListeners.forEach((l) => l(value));
}

beforeEach(() => {
  mockOnline = true;
  mockListeners.clear();
});

describe('useNetworkStatus', () => {
  it('começa com o estado atual do monitor', () => {
    mockOnline = false;
    const { useNetworkStatus } = require('../../hooks/useNetworkStatus');
    const { result } = renderHook(() => useNetworkStatus());
    expect(result.current).toBe(false);
  });

  it('reage à mudança de conectividade', () => {
    const { useNetworkStatus } = require('../../hooks/useNetworkStatus');
    const { result } = renderHook(() => useNetworkStatus());
    expect(result.current).toBe(true);

    act(() => emit(false));
    expect(result.current).toBe(false);

    act(() => emit(true));
    expect(result.current).toBe(true);
  });

  it('cancela a inscrição ao desmontar', () => {
    const { useNetworkStatus } = require('../../hooks/useNetworkStatus');
    const { unmount } = renderHook(() => useNetworkStatus());
    expect(mockListeners.size).toBe(1);
    unmount();
    expect(mockListeners.size).toBe(0);
  });
});
