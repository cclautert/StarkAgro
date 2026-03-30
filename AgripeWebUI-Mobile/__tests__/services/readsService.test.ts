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
});
