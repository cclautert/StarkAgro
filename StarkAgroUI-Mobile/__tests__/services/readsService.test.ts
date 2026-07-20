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

const pivotResponse = {
  pivotId: 1,
  pivotName: 'Pivô 1',
  quadranteData: {
    TopRight: { quadranteName: 'TopRight', avgHumidity: 60 },
  },
};

const readings = [
  { data: '2025-01-01', hora: '10:00', umidade: 65, quadrante: 1 },
];

describe('readsService', () => {
  it('getByPivotId passes PivotId and NumberOfReads params', async () => {
    mock.onGet(/reads\/GetByPivotId/).reply(200, pivotResponse);
    const { readsService } = require('../../services/readsService');
    const result = await readsService.getByPivotId(1, 7);
    expect(result).toEqual(pivotResponse);
    expect(mock.history.get[0].params).toMatchObject({ PivotId: 1, NumberOfReads: 7 });
  });

  it('getByPivotId defaults to 7 days', async () => {
    mock.onGet(/reads\/GetByPivotId/).reply(200, pivotResponse);
    const { readsService } = require('../../services/readsService');
    await readsService.getByPivotId(1);
    expect(mock.history.get[0].params.NumberOfReads).toBe(7);
  });

  it('getAllBySensorId passes sensorId, quadrante and numberOfReads', async () => {
    mock.onGet(/reads\/GetAllBySensorId/).reply(200, readings);
    const { readsService } = require('../../services/readsService');
    const result = await readsService.getAllBySensorId(10, 1, 14);
    expect(result).toEqual(readings);
    expect(mock.history.get[0].params).toMatchObject({ sensorId: 10, quadrante: 1, numberOfReads: 14 });
  });

  it('getAllBySensorId defaults to 7 days', async () => {
    mock.onGet(/reads\/GetAllBySensorId/).reply(200, []);
    const { readsService } = require('../../services/readsService');
    await readsService.getAllBySensorId(10, 1);
    expect(mock.history.get[0].params.numberOfReads).toBe(7);
  });

  it('createManualRead posts code and value', async () => {
    mock.onPost(/reads\/Add/).reply(200, { id: 5, sensorId: 10, userId: 1 });
    const { readsService } = require('../../services/readsService');
    const result = await readsService.createManualRead({ code: 'SENS01', value: 42 });
    expect(result.id).toBe(5);
    expect(result.value).toBe(42);
    expect(JSON.parse(mock.history.post[0].data)).toEqual({ code: 'SENS01', value: 42 });
  });

  it('getLatestBySensorId returns newest reading', async () => {
    mock.onGet(/reads\/GetAllBySensorId/).reply(200, [
      { id: 1, sensorId: 10, value: 40, date: '2026-06-05T10:00:00.000Z' },
      { id: 2, sensorId: 10, value: 42, date: '2026-06-05T11:00:00.000Z' },
    ]);
    const { readsService } = require('../../services/readsService');
    const latest = await readsService.getLatestBySensorId(10, 1);
    expect(latest?.value).toBe(42);
  });

  it('getLatestBySensorId devolve null quando não há leitura (sensor novo)', async () => {
    mock.onGet(/reads\/GetAllBySensorId/).reply(200, []);
    const { readsService } = require('../../services/readsService');
    await expect(readsService.getLatestBySensorId(10, 1)).resolves.toBeNull();
  });
});
