import AsyncStorage from '@react-native-async-storage/async-storage';

const mockCreateManualRead = jest.fn();
const mockGetLatestBySensorId = jest.fn();
const mockUploadDiagnosis = jest.fn();

jest.mock('../../../services/readsService', () => ({
  readsService: {
    createManualRead: mockCreateManualRead,
    getLatestBySensorId: mockGetLatestBySensorId,
  },
}));

// Sem este mock, o diagnosisService real arrasta o axios para dentro do Jest e a suíte
// inteira quebra no adapter de fetch do Expo (mesmo motivo das suítes de services).
jest.mock('../../../services/diagnosisService', () => ({
  diagnosisService: {
    upload: mockUploadDiagnosis,
  },
}));

beforeEach(async () => {
  await AsyncStorage.clear();
  mockCreateManualRead.mockReset();
  mockGetLatestBySensorId.mockReset();
  mockUploadDiagnosis.mockReset();
  jest.resetModules();
});

describe('syncQueue', () => {
  it('enqueues manual reads and syncs when online', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue(null);
    mockCreateManualRead.mockResolvedValue({ id: 99, sensorId: 10, userId: 1, value: 42 });

    const result = await syncQueue.processQueue();
    expect(result.synced).toBe(1);
    await expect(syncQueue.getAll()).resolves.toEqual([]);
  });

  it('applies last-write-wins and logs conflict when server is newer', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');
    const { conflictLog } = require('../../../services/offline/conflictLog');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue({
      id: 1,
      sensorId: 10,
      value: 50,
      date: '2026-06-05T11:00:00.000Z',
    });

    const result = await syncQueue.processQueue();
    expect(result.synced).toBe(1);
    expect(mockCreateManualRead).not.toHaveBeenCalled();

    const conflicts = await conflictLog.getAll();
    expect(conflicts).toHaveLength(1);
    expect(conflicts[0].resolution).toBe('server_wins');
  });

  it('does not duplicate when server already has matching read', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueManualRead({
      sensorCode: 'SENS01',
      sensorId: 10,
      pivotId: 1,
      quadrante: 1,
      value: 42,
      recordedAt: '2026-06-05T10:00:00.000Z',
    });

    mockGetLatestBySensorId.mockResolvedValue({
      id: 1,
      sensorId: 10,
      value: 42,
      date: '2026-06-05T10:00:00.000Z',
    });

    const result = await syncQueue.processQueue();
    expect(result.skipped).toBe(1);
    expect(mockCreateManualRead).not.toHaveBeenCalled();
  });
});

describe('syncQueue — foto do laudo', () => {
  const photo = {
    localUri: 'file:///tmp/folha.jpg',
    pivotId: 1,
    cropName: 'tomate',
    notes: 'manchas escuras',
    capturedAt: '2026-07-14T10:00:00.000Z',
  };

  it('sobe a foto quando a conexão volta e limpa a fila', async () => {
    // É o caso de uso central no campo: o produtor fotografa sem sinal e a foto sobe sozinha.
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueDiagnosisPhoto(photo);
    await expect(syncQueue.getPendingCount()).resolves.toBe(1);

    mockUploadDiagnosis.mockResolvedValue({ id: 7, status: 'Uploaded' });

    const result = await syncQueue.processQueue();

    expect(result.synced).toBe(1);
    expect(mockUploadDiagnosis).toHaveBeenCalledWith({
      localUri: photo.localUri,
      pivotId: photo.pivotId,
      cropName: photo.cropName,
      notes: photo.notes,
    });
    await expect(syncQueue.getAll()).resolves.toEqual([]);
  });

  it('mantém a foto na fila quando a rede falha', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueDiagnosisPhoto(photo);

    mockUploadDiagnosis.mockRejectedValue(new Error('Network Error'));   // sem response

    const result = await syncQueue.processQueue();

    expect(result.failed).toBe(1);

    const items = await syncQueue.getAll();
    expect(items).toHaveLength(1);
    expect(items[0].status).toBe('error');
    expect(items[0].retries).toBe(1);
  });

  it('descarta a foto quando o servidor a recusa (4xx), em vez de retentar para sempre', async () => {
    // Cota estourada ou foto inválida é veredito: retentar daria o mesmo erro eternamente
    // e a fila do produtor nunca esvaziaria.
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueDiagnosisPhoto(photo);

    mockUploadDiagnosis.mockRejectedValue({ response: { status: 400 } });

    const result = await syncQueue.processQueue();

    expect(result.failed).toBe(1);
    await expect(syncQueue.getAll()).resolves.toEqual([]);
    await expect(syncQueue.getPendingCount()).resolves.toBe(0);
  });

  it('retenta quando o servidor devolve 5xx', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueDiagnosisPhoto(photo);

    mockUploadDiagnosis.mockRejectedValue({ response: { status: 503 } });

    await syncQueue.processQueue();

    const items = await syncQueue.getAll();
    expect(items).toHaveLength(1);
    expect(items[0].status).toBe('error');
  });

  it('não reenvia uma foto que já subiu', async () => {
    const { syncQueue } = require('../../../services/offline/syncQueue');

    await syncQueue.enqueueDiagnosisPhoto(photo);
    mockUploadDiagnosis.mockResolvedValue({ id: 7, status: 'Uploaded' });
    await syncQueue.processQueue();

    // Uma segunda passada não pode cobrar outra análise pela mesma foto.
    const second = await syncQueue.processQueue();

    expect(second.synced).toBe(0);
    expect(mockUploadDiagnosis).toHaveBeenCalledTimes(1);
  });
});
