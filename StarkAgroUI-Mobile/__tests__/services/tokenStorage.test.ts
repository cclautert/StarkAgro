// jest-expo runs in native (android) mode by default.
// Test the native (SecureStore) path directly; web path via jest.doMock.

const mockLocalStorage = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: jest.fn((key: string) => store[key] ?? null),
    setItem: jest.fn((key: string, value: string) => { store[key] = value; }),
    removeItem: jest.fn((key: string) => { delete store[key]; }),
    clear: () => {
      store = {};
      (mockLocalStorage.getItem as jest.Mock).mockClear();
      (mockLocalStorage.setItem as jest.Mock).mockClear();
      (mockLocalStorage.removeItem as jest.Mock).mockClear();
    },
  };
})();

beforeEach(() => {
  const ss = require('expo-secure-store');
  ss.__reset?.();
  mockLocalStorage.clear();
});

// ── Native (default jest-expo environment is android) ────────────────────────
describe('tokenStorage — native branch', () => {
  it('getToken returns null when nothing stored', async () => {
    const { tokenStorage } = require('../../services/tokenStorage');
    expect(await tokenStorage.getToken()).toBeNull();
  });

  it('setToken and getToken round-trip', async () => {
    const { tokenStorage } = require('../../services/tokenStorage');
    await tokenStorage.setToken('native-token');
    expect(await tokenStorage.getToken()).toBe('native-token');
  });

  it('removeToken deletes the key', async () => {
    const { tokenStorage } = require('../../services/tokenStorage');
    await tokenStorage.setToken('native-token');
    await tokenStorage.removeToken();
    expect(await tokenStorage.getToken()).toBeNull();
  });
});

// ── Web branch via jest.doMock + isolateModules ───────────────────────────────
describe('tokenStorage — web branch', () => {
  let tokenStorage: {
    getToken: () => Promise<string | null>;
    setToken: (t: string) => Promise<void>;
    removeToken: () => Promise<void>;
  };

  beforeEach(() => {
    jest.resetModules();
    jest.doMock('react-native', () => ({
      Platform: { OS: 'web' },
    }));
    Object.defineProperty(global, 'window', {
      value: { localStorage: mockLocalStorage },
      writable: true,
      configurable: true,
    });
    Object.defineProperty(global, 'localStorage', {
      value: mockLocalStorage,
      writable: true,
      configurable: true,
    });
    tokenStorage = require('../../services/tokenStorage').tokenStorage;
  });

  afterEach(() => {
    jest.dontMock('react-native');
    jest.resetModules();
  });

  it('getToken calls localStorage.getItem', async () => {
    mockLocalStorage.getItem = jest.fn(() => 'web-token') as jest.Mock;
    const result = await tokenStorage.getToken();
    expect(result).toBe('web-token');
  });

  it('setToken calls localStorage.setItem', async () => {
    await tokenStorage.setToken('web-token');
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith('starkagro_token', 'web-token');
  });

  it('removeToken calls localStorage.removeItem', async () => {
    await tokenStorage.removeToken();
    expect(mockLocalStorage.removeItem).toHaveBeenCalledWith('starkagro_token');
  });
});
