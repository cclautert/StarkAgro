import { renderHook } from '@testing-library/react-native';

// Prefixo `mock` é obrigatório para a fábrica do jest.mock poder referenciar (regra do Jest).
let mockOnline = true;
const mockListeners = new Set<(v: boolean) => void>();

const mockInit = jest.fn().mockResolvedValue(undefined);
const mockProcessQueue = jest.fn().mockResolvedValue(undefined);

jest.mock('../../services/offline/networkMonitor', () => ({
  networkMonitor: {
    init: () => mockInit(),
    getIsOnline: () => mockOnline,
    subscribe: (fn: (v: boolean) => void) => {
      mockListeners.add(fn);
      return () => mockListeners.delete(fn);
    },
  },
}));

jest.mock('../../services/offline/syncQueue', () => ({
  syncQueue: { processQueue: () => mockProcessQueue() },
}));

function emit(value: boolean) {
  mockOnline = value;
  mockListeners.forEach((l) => l(value));
}

beforeEach(() => {
  mockOnline = true;
  mockListeners.clear();
  mockInit.mockClear();
  mockProcessQueue.mockClear();
});

describe('useOfflineBootstrap', () => {
  it('inicializa o monitor e, estando online, já drena a fila uma vez', () => {
    const { useOfflineBootstrap } = require('../../hooks/useOfflineBootstrap');
    renderHook(() => useOfflineBootstrap());

    expect(mockInit).toHaveBeenCalledTimes(1);
    expect(mockProcessQueue).toHaveBeenCalledTimes(1); // pela verificação inicial
  });

  it('não drena a fila no boot se estiver offline', () => {
    mockOnline = false;
    const { useOfflineBootstrap } = require('../../hooks/useOfflineBootstrap');
    renderHook(() => useOfflineBootstrap());

    expect(mockInit).toHaveBeenCalledTimes(1);
    expect(mockProcessQueue).not.toHaveBeenCalled();
  });

  it('drena a fila quando a conexão volta (o motivo de existir do hook)', () => {
    mockOnline = false;
    const { useOfflineBootstrap } = require('../../hooks/useOfflineBootstrap');
    renderHook(() => useOfflineBootstrap());
    expect(mockProcessQueue).not.toHaveBeenCalled();

    emit(true);
    expect(mockProcessQueue).toHaveBeenCalledTimes(1);
  });

  it('perder a conexão não dispara sync (evita drenar a fila à toa)', () => {
    const { useOfflineBootstrap } = require('../../hooks/useOfflineBootstrap');
    renderHook(() => useOfflineBootstrap());
    mockProcessQueue.mockClear();

    emit(false);
    expect(mockProcessQueue).not.toHaveBeenCalled();
  });

  it('cancela a inscrição ao desmontar', () => {
    const { useOfflineBootstrap } = require('../../hooks/useOfflineBootstrap');
    const { unmount } = renderHook(() => useOfflineBootstrap());
    expect(mockListeners.size).toBe(1);
    unmount();
    expect(mockListeners.size).toBe(0);
  });
});
