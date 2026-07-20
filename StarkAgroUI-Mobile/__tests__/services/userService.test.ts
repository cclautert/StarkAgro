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

let mock: MockAdapter;

beforeAll(() => {
  const api = require('../../services/api').default;
  mock = new MockAdapter(api);
});

afterEach(() => mock.reset());

const mockUser = { id: 1, name: 'João', email: 'joao@example.com' };

describe('userService', () => {
  it('getById sends GET with id param and returns user', async () => {
    mock.onGet(/user\/getById/).reply(200, mockUser);
    const { userService } = require('../../services/userService');
    const result = await userService.getById(1);
    expect(result).toEqual(mockUser);
    expect(mock.history.get[0].params).toMatchObject({ id: 1 });
  });

  it('update sends PUT with currentUserId and returns user', async () => {
    mock.onPut(/user\/update/).reply(200, mockUser);
    const { userService } = require('../../services/userService');
    const result = await userService.update({ id: 1, name: 'Novo Nome', currentUserId: 1 });
    expect(result).toEqual(mockUser);
    expect(JSON.parse(mock.history.put[0].data)).toMatchObject({ currentUserId: 1 });
  });
});
