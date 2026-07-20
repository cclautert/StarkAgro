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

const mockPivot = { id: 1, name: 'Pivô 1', userId: 10 };

describe('pivotService', () => {
  it('getAll sends GET to pivot/getAll and returns array', async () => {
    mock.onGet(/pivot\/getAll/).reply(200, [mockPivot]);
    const { pivotService } = require('../../services/pivotService');
    const result = await pivotService.getAll();
    expect(result).toEqual([mockPivot]);
  });

  it('getById sends GET with id param', async () => {
    mock.onGet(/pivot\/getById/).reply(200, mockPivot);
    const { pivotService } = require('../../services/pivotService');
    const result = await pivotService.getById(1);
    expect(result).toEqual(mockPivot);
  });

  it('add sends POST and returns id', async () => {
    mock.onPost(/pivot\/add/).reply(200, { id: 2 });
    const { pivotService } = require('../../services/pivotService');
    const result = await pivotService.add({ name: 'Novo Pivô', userId: 10 });
    expect(result.id).toBe(2);
  });

  it('update sends PUT and returns id', async () => {
    mock.onPut(/pivot\/update/).reply(200, { id: 1 });
    const { pivotService } = require('../../services/pivotService');
    const result = await pivotService.update({ id: 1, name: 'Editado', userId: 10 });
    expect(result.id).toBe(1);
  });

  it('delete sends DELETE with id param', async () => {
    mock.onDelete(/pivot\/delete/).reply(200);
    const { pivotService } = require('../../services/pivotService');
    await expect(pivotService.delete(1)).resolves.not.toThrow();
  });
});
