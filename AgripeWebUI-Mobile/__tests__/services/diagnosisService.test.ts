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

describe('diagnosisService', () => {
  it('upload manda multipart para diagnosis e devolve id/status', async () => {
    let sentContentType: string | undefined;
    mock.onPost('diagnosis').reply((config) => {
      sentContentType = (config.headers?.['Content-Type'] as string) ?? undefined;
      // O corpo é um FormData — o que importa é o multipart, não os bytes.
      expect(config.data).toBeInstanceOf(FormData);
      return [200, { id: 7, status: 'Uploaded' }];
    });

    const { diagnosisService } = require('../../services/diagnosisService');
    const res = await diagnosisService.upload({
      localUri: 'file:///tmp/folha.jpg',
      pivotId: 3,
      cropName: 'Soja',
      notes: 'mancha na folha',
    });

    expect(res).toEqual({ id: 7, status: 'Uploaded' });
    expect(sentContentType).toBe('multipart/form-data');
  });

  it('upload funciona sem pivô/cultura/notas (campos opcionais)', async () => {
    mock.onPost('diagnosis').reply(200, { id: 8, status: 'Uploaded' });
    const { diagnosisService } = require('../../services/diagnosisService');
    const res = await diagnosisService.upload({ localUri: 'file:///tmp/x.jpg' });
    expect(res.id).toBe(8);
  });

  it('getAll devolve a lista de laudos', async () => {
    const summary = [{ id: 1, status: 'AiCompleted', createdAt: '2026-07-01' }];
    mock.onGet('diagnosis').reply(200, summary);
    const { diagnosisService } = require('../../services/diagnosisService');
    await expect(diagnosisService.getAll()).resolves.toEqual(summary);
  });

  it('getById busca pelo id', async () => {
    mock.onGet('diagnosis/5').reply(200, { id: 5, status: 'Signed', diseases: [] });
    const { diagnosisService } = require('../../services/diagnosisService');
    const res = await diagnosisService.getById(5);
    expect(res.id).toBe(5);
    expect(res.status).toBe('Signed');
  });

  it('getQuota lê a cota do produtor', async () => {
    const quota = { limit: 10, used: 3, remaining: 7, isUnlimited: false, isExhausted: false, resetsAt: '2026-08-01' };
    mock.onGet('diagnosis/quota').reply(200, quota);
    const { diagnosisService } = require('../../services/diagnosisService');
    await expect(diagnosisService.getQuota()).resolves.toEqual(quota);
  });

  it('propaga erro do backend (ex.: cota esgotada = 403)', async () => {
    mock.onPost('diagnosis').reply(403, { message: 'cota esgotada' });
    const { diagnosisService } = require('../../services/diagnosisService');
    await expect(diagnosisService.upload({ localUri: 'file:///x.jpg' })).rejects.toBeDefined();
  });
});
