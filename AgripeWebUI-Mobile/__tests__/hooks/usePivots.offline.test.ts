import { renderHook, waitFor } from '@testing-library/react-native';

// Cobre os caminhos de cache/offline do usePivots — a razão de o hook existir num app de campo,
// onde o 4G cai. O teste principal (usePivots.test.ts) cobre só o caminho online feliz.
const mockGetAll = jest.fn();
const mockGetIsOnline = jest.fn(() => true);
const mockGetPivots = jest.fn();
const mockSetPivots = jest.fn().mockResolvedValue(undefined);

jest.mock('../../services/pivotService', () => ({
  pivotService: { getAll: mockGetAll },
}));

jest.mock('../../services/offline/networkMonitor', () => ({
  networkMonitor: {
    init: jest.fn().mockResolvedValue(undefined),
    getIsOnline: mockGetIsOnline,
    subscribe: jest.fn(() => () => {}),
  },
}));

jest.mock('../../services/offline/offlineCache', () => ({
  offlineCache: { getPivots: mockGetPivots, setPivots: mockSetPivots },
}));

const cachedPivots = [{ id: 1, name: 'Pivô cacheado', userId: 1 }];

beforeEach(() => {
  mockGetAll.mockReset();
  mockGetPivots.mockReset();
  mockGetIsOnline.mockReturnValue(true);
});

describe('usePivots — cache e offline', () => {
  it('online mas a API falha: cai no cache e avisa que está desatualizado', async () => {
    mockGetIsOnline.mockReturnValue(true);
    mockGetAll.mockRejectedValue(new Error('timeout'));
    mockGetPivots.mockResolvedValue(cachedPivots);

    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pivots).toEqual(cachedPivots);
    expect(result.current.fromCache).toBe(true);
    expect(result.current.error).toMatch(/falha ao atualizar/i);
  });

  it('offline: usa o cache e avisa que está sem conexão', async () => {
    mockGetIsOnline.mockReturnValue(false);
    mockGetPivots.mockResolvedValue(cachedPivots);

    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pivots).toEqual(cachedPivots);
    expect(result.current.fromCache).toBe(true);
    expect(result.current.error).toMatch(/sem conex/i);
    expect(mockGetAll).not.toHaveBeenCalled(); // offline: nem tenta a rede
  });

  it('offline e sem cache: erro seco de carregamento', async () => {
    mockGetIsOnline.mockReturnValue(false);
    mockGetPivots.mockResolvedValue(null);

    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pivots).toEqual([]);
    expect(result.current.fromCache).toBe(false);
    expect(result.current.error).toBe('Erro ao carregar pivôs');
  });
});
