import axios from 'axios';
import MockAdapter from 'axios-mock-adapter';

jest.mock('../../services/tokenStorage', () => ({
  tokenStorage: {
    getToken: jest.fn().mockResolvedValue(null),
    setToken: jest.fn(),
    removeToken: jest.fn(),
  },
}));

jest.mock('expo-router', () => ({
  router: { replace: jest.fn() },
  useRouter: jest.fn(() => ({ replace: jest.fn() })),
}));

let mock: MockAdapter;

beforeAll(() => {
  const api = require('../../services/api').default;
  mock = new MockAdapter(api);
});

afterEach(() => {
  mock.reset();
});

describe('authService.login', () => {
  it('returns token on 200', async () => {
    mock.onPost(/Auth\/LogIn/).reply(200, { token: 'jwt-token' });
    const { authService } = require('../../services/authService');
    const result = await authService.login({ email: 'a@b.com', password: '123' });
    expect(result.token).toBe('jwt-token');
  });

  it('throws on 401', async () => {
    mock.onPost(/Auth\/LogIn/).reply(401);
    const { authService } = require('../../services/authService');
    await expect(authService.login({ email: 'a@b.com', password: 'wrong' })).rejects.toThrow();
  });
});

describe('authService.externalLogin', () => {
  it('returns token on 200', async () => {
    mock.onPost(/Auth\/external-login/).reply(200, { token: 'oauth-token' });
    const { authService } = require('../../services/authService');
    const result = await authService.externalLogin({
      provider: 'Google',
      code: 'auth-code',
      redirectUri: 'http://localhost',
    });
    expect(result?.token).toBe('oauth-token');
  });

  it('throws on server error', async () => {
    mock.onPost(/Auth\/external-login/).reply(500);
    const { authService } = require('../../services/authService');
    await expect(
      authService.externalLogin({ provider: 'Google', code: 'bad', redirectUri: 'http://localhost' })
    ).rejects.toThrow();
  });
});
