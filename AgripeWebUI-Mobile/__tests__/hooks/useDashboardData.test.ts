import { renderHook, waitFor } from '@testing-library/react-native';

const mockGetByPivotId = jest.fn();
const mockGetAllByPivotId = jest.fn();
const mockGetAllSensorsByPivotId = jest.fn();

jest.mock('../../services/readsService', () => ({
  readsService: {
    getByPivotId: mockGetByPivotId,
    getAllByPivotId: mockGetAllByPivotId,
  },
}));

jest.mock('../../services/sensorService', () => ({
  sensorService: {
    getAllByPivotId: mockGetAllSensorsByPivotId,
  },
}));

const fakePivotData = {
  pivotId: 1,
  pivotName: 'Pivô 1',
  quadranteData: {},
};

const fakeReadings = [{ data: '2025-01-01', hora: '10:00', umidade: 60, quadrante: 1 }];

beforeEach(() => {
  jest.useFakeTimers();
  mockGetByPivotId.mockReset();
  mockGetAllByPivotId.mockReset();
  mockGetAllSensorsByPivotId.mockReset();
});

afterEach(() => {
  jest.useRealTimers();
});

describe('useDashboardData', () => {
  it('returns loading=false and chartData[4] on success', async () => {
    mockGetByPivotId.mockResolvedValue(fakePivotData);
    mockGetAllSensorsByPivotId.mockResolvedValue([{ id: 10, pivotId: 1, quadrante: 1 }]);
    mockGetAllByPivotId.mockResolvedValue(fakeReadings);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pivot).toEqual(fakePivotData);
    expect(result.current.chartData).toHaveLength(4);
    expect(result.current.error).toBeNull();
  });

  it('calls getByPivotId once', async () => {
    mockGetByPivotId.mockResolvedValue(fakePivotData);
    mockGetAllSensorsByPivotId.mockResolvedValue([]);
    mockGetAllByPivotId.mockResolvedValue([]);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(mockGetByPivotId).toHaveBeenCalledTimes(1);
    expect(mockGetByPivotId).toHaveBeenCalledWith(1, 7);
  });

  it('handles partial quadrant failure gracefully (returns empty readings)', async () => {
    mockGetByPivotId.mockResolvedValue(fakePivotData);
    // Only quadrant 1 has sensors; others return empty
    mockGetAllSensorsByPivotId.mockImplementation(async (_pivotId, q) => {
      if (q === 1) return [{ id: 10, pivotId: 1, quadrante: 1 }];
      throw new Error('no sensors');
    });
    mockGetAllByPivotId.mockResolvedValue(fakeReadings);

    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(1, 7));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.chartData).toHaveLength(4);
    // Quadrant 1 should have readings; others empty
    const q1 = result.current.chartData.find((c: { quadrante: number }) => c.quadrante === 1);
    const q2 = result.current.chartData.find((c: { quadrante: number }) => c.quadrante === 2);
    expect(q1?.readings).toEqual(fakeReadings);
    expect(q2?.readings).toEqual([]);
  });

  it('does nothing when pivotId is null', async () => {
    const { useDashboardData } = require('../../hooks/useDashboardData');
    const { result } = renderHook(() => useDashboardData(null, 7));
    expect(result.current.loading).toBe(false);
    expect(mockGetByPivotId).not.toHaveBeenCalled();
  });
});
