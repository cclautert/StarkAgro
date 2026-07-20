const listeners = new Set<(state: { isConnected: boolean; isInternetReachable: boolean }) => void>();

let mockState = { isConnected: true, isInternetReachable: true };

export default {
  fetch: jest.fn(async () => mockState),
  addEventListener: jest.fn((listener: (state: typeof mockState) => void) => {
    listeners.add(listener);
    return () => listeners.delete(listener);
  }),
  __setMockState: (state: typeof mockState) => {
    mockState = state;
    listeners.forEach((listener) => listener(mockState));
  },
};
