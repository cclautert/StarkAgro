import { renderHook, waitFor } from '@testing-library/react-native';

// Caminhos de cache/offline do dashboard. O teste principal cobre o online feliz.
const mockGetByPivotId = jest.fn();
const mockGetAllBySensorId = jest.fn();
const mockGetAllSensorsByPivotId = jest.fn();
const mockGetIsOnline = jest.fn(() => true);
const mockGetDashboard = jest.fn();
const mockSetDashboard = jest.fn().mockResolvedValue(undefined);

jest.mock('../../services/readsService', () => ({
  readsService: { getByPivotId: mockGetByPivotId, getAllBySensorId: mockGetAllBySensorId },
}));

jest.mock('../../services/sensorService', () => ({
  sensorService: { getAllByPivotId: mockGetAllSensorsByPivotId },
}));

jest.mock('../../services/offline/networkMonitor', () => ({
  networkMonitor: {
    init: jest.fn().mockResolvedValue(undefined),
    getIsOnline: mockGetIsOnline,
    subscribe: jest.fn(() => () => {}),
  },
}));

jest.mock('../../services/offline/offlineCache', () => ({
  offlineCache: { getDashboard: mockGetDashboard, setDashboard: mockSetDashboard },
}));

const cached = {
  pivot: { pivotId: 1, pivotName: 'Pivô 1', quadranteData: {} },
  chartData: [{ quadrante: 1, readings: [] }],
  cachedAt: '2026-07-01T00:00:00Z',
};

beforeEach(() => {
  jest.useFakeTimers();
  mockGetByPivotId.mockReset();
  mockGetAllBySensorId.mockReset();
  mockGetAllSensorsByPivotId.mockReset();
  mockGetDashboard.mockReset();
  mockGetIsOnline.mockReturnValue(true);
});

afterEach(() => jest.useRealTimers());

describe('useDashboardData — cache e offline', () => {
  it('online mas a API falha: usa o cache e marca fromCache', async () => {
    mockGetIsOnline.mockReturnValue(true);
    mockGetByPivotId.mockRejectedValue(new Error('timeout'));
    mockGetDashboard.mockResolvedValue(cached);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.fromCache).toBe(true);
    expect(result.current.pivot).toEqual(cached.pivot);
    expect(result.current.error).toMatch(/falha ao atualizar/i);
  });

  it('offline: usa o cache e avisa sem conexão, sem tentar a rede', async () => {
    mockGetIsOnline.mockReturnValue(false);
    mockGetDashboard.mockResolvedValue(cached);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.fromCache).toBe(true);
    expect(result.current.error).toMatch(/sem conex/i);
    expect(mockGetByPivotId).not.toHaveBeenCalled();
  });

  it('offline e sem cache: erro de carregamento', async () => {
    mockGetIsOnline.mockReturnValue(false);
    mockGetDashboard.mockResolvedValue(null);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe('Erro ao carregar dados do dashboard');
    expect(result.current.fromCache).toBe(false);
  });
});
