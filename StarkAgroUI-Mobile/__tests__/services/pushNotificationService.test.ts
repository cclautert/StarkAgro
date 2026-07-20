import MockAdapter from 'axios-mock-adapter';

jest.mock('../../services/tokenStorage', () => ({
  tokenStorage: {
    getToken: jest.fn().mockResolvedValue('mock-token'),
    setToken: jest.fn(),
    removeToken: jest.fn(),
  },
}));

jest.mock('expo-router', () => ({
  router: { replace: jest.fn() },
  useRouter: jest.fn(() => ({ replace: jest.fn() })),
}));

const mockGetPermissions = jest.fn();
const mockRequestPermissions = jest.fn();
const mockGetToken = jest.fn();

jest.mock('expo-notifications', () => ({
  getPermissionsAsync: (...a: unknown[]) => mockGetPermissions(...a),
  requestPermissionsAsync: (...a: unknown[]) => mockRequestPermissions(...a),
  getExpoPushTokenAsync: (...a: unknown[]) => mockGetToken(...a),
}));

// Platform.OS é controlado por teste; o default do RN mock é 'ios'.
jest.mock('react-native', () => ({ Platform: { OS: 'ios' } }));

let mock: MockAdapter;

beforeAll(() => {
  const api = require('../../services/api').default;
  mock = new MockAdapter(api);
});

beforeEach(() => {
  mockGetPermissions.mockReset();
  mockRequestPermissions.mockReset();
  mockGetToken.mockReset();
});

afterEach(() => mock.reset());

describe('registerForPushNotificationsAsync', () => {
  it('devolve o token quando a permissão já está concedida (sem pedir de novo)', async () => {
    mockGetPermissions.mockResolvedValue({ status: 'granted' });
    mockGetToken.mockResolvedValue({ data: 'ExponentPushToken[abc]' });

    const { registerForPushNotificationsAsync } = require('../../services/pushNotificationService');
    const token = await registerForPushNotificationsAsync();

    expect(token).toBe('ExponentPushToken[abc]');
    expect(mockRequestPermissions).not.toHaveBeenCalled();
  });

  it('pede permissão quando ainda não foi concedida e então devolve o token', async () => {
    mockGetPermissions.mockResolvedValue({ status: 'undetermined' });
    mockRequestPermissions.mockResolvedValue({ status: 'granted' });
    mockGetToken.mockResolvedValue({ data: 'ExponentPushToken[xyz]' });

    const { registerForPushNotificationsAsync } = require('../../services/pushNotificationService');
    const token = await registerForPushNotificationsAsync();

    expect(mockRequestPermissions).toHaveBeenCalledTimes(1);
    expect(token).toBe('ExponentPushToken[xyz]');
  });

  it('devolve null se o usuário negar a permissão', async () => {
    mockGetPermissions.mockResolvedValue({ status: 'undetermined' });
    mockRequestPermissions.mockResolvedValue({ status: 'denied' });

    const { registerForPushNotificationsAsync } = require('../../services/pushNotificationService');
    const token = await registerForPushNotificationsAsync();

    expect(token).toBeNull();
    expect(mockGetToken).not.toHaveBeenCalled();
  });

  it('devolve null na web (não há push nativo)', async () => {
    // Platform.OS é lido no momento da chamada — basta mutar a instância mockada, sem
    // resetModules (que desconectaria o MockAdapter do api).
    const RN = require('react-native');
    RN.Platform.OS = 'web';
    try {
      const { registerForPushNotificationsAsync } = require('../../services/pushNotificationService');
      const token = await registerForPushNotificationsAsync();

      expect(token).toBeNull();
      expect(mockGetPermissions).not.toHaveBeenCalled();
    } finally {
      RN.Platform.OS = 'ios';
    }
  });
});

describe('registerTokenWithBackend', () => {
  it('faz PUT em user/pushToken com o token', async () => {
    let body: unknown;
    mock.onPut('user/pushToken').reply((config) => {
      body = JSON.parse(config.data);
      return [200];
    });

    const { registerTokenWithBackend } = require('../../services/pushNotificationService');
    await registerTokenWithBackend('ExponentPushToken[abc]');

    expect(body).toEqual({ token: 'ExponentPushToken[abc]' });
  });
});
