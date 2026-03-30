import { renderHook, waitFor, act } from '@testing-library/react-native';

const mockGetAll = jest.fn();

jest.mock('../../services/pivotService', () => ({
  pivotService: { getAll: mockGetAll },
}));

beforeEach(() => {
  jest.useFakeTimers();
  mockGetAll.mockReset();
});

afterEach(() => {
  jest.useRealTimers();
});

describe('usePivots', () => {
  it('starts with loading=true and empty pivots', () => {
    mockGetAll.mockResolvedValue([]);
    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());
    expect(result.current.loading).toBe(true);
    expect(result.current.pivots).toEqual([]);
    expect(result.current.error).toBeNull();
  });

  it('populates pivots on success', async () => {
    const pivots = [{ id: 1, name: 'P1', userId: 1 }];
    mockGetAll.mockResolvedValue(pivots);
    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pivots).toEqual(pivots);
    expect(result.current.error).toBeNull();
  });

  it('sets error on failure', async () => {
    mockGetAll.mockRejectedValue(new Error('network'));
    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe('Erro ao carregar pivôs');
    expect(result.current.pivots).toEqual([]);
  });

  it('refresh re-calls getAll', async () => {
    mockGetAll.mockResolvedValue([]);
    const { usePivots } = require('../../hooks/usePivots');
    const { result } = renderHook(() => usePivots());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(mockGetAll).toHaveBeenCalledTimes(1);
    await act(async () => {
      result.current.refresh();
    });
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(mockGetAll).toHaveBeenCalledTimes(2);
  });
});
