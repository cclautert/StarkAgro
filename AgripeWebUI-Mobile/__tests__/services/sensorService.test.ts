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

const mockSensor = { id: 1, name: 'Sensor 1', pivotId: 2, quadrante: 1, userId: 10 };

describe('sensorService', () => {
  it('getAll returns sensor array', async () => {
    mock.onGet(/sensor\/getAll/).reply(200, [mockSensor]);
    const { sensorService } = require('../../services/sensorService');
    expect(await sensorService.getAll()).toEqual([mockSensor]);
  });

  it('getById returns a single sensor', async () => {
    mock.onGet(/sensor\/getById/).reply(200, mockSensor);
    const { sensorService } = require('../../services/sensorService');
    expect(await sensorService.getById(1)).toEqual(mockSensor);
  });

  it('getAllByPivotId sends pivotId and quadrante params', async () => {
    mock.onGet(/sensor\/getAllByPivotId/).reply(200, [mockSensor]);
    const { sensorService } = require('../../services/sensorService');
    const result = await sensorService.getAllByPivotId(2, 1);
    expect(result).toEqual([mockSensor]);
    expect(mock.history.get[0].params).toMatchObject({ pivotId: 2, quadrante: 1 });
  });

  it('add sends POST and returns id', async () => {
    mock.onPost(/sensor\/add/).reply(200, { id: 5 });
    const { sensorService } = require('../../services/sensorService');
    expect((await sensorService.add({ name: 'S', pivotId: 2, quadrante: 1, userId: 10 })).id).toBe(5);
  });

  it('update sends PUT and returns id', async () => {
    mock.onPut(/sensor\/update/).reply(200, { id: 1 });
    const { sensorService } = require('../../services/sensorService');
    expect((await sensorService.update({ id: 1, name: 'S2', pivotId: 2, quadrante: 1, userId: 10 })).id).toBe(1);
  });

  it('delete sends DELETE', async () => {
    mock.onDelete(/sensor\/delete/).reply(200);
    const { sensorService } = require('../../services/sensorService');
    await expect(sensorService.delete(1)).resolves.not.toThrow();
  });
});
