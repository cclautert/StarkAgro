// O networkMonitor tem estado de módulo (currentOnline, unsubscribeNetInfo), então cada teste
// recarrega o módulo do zero. O jest.resetModules também recria o mock do NetInfo, logo é
// preciso pegar a instância NOVA do NetInfo — a do topo ficaria obsoleta.
type NetInfoMock = {
  __setMockState: (s: { isConnected: boolean; isInternetReachable: boolean | null }) => void;
};

function fresh() {
  jest.resetModules();
  const netinfo = require('@react-native-community/netinfo').default as NetInfoMock;
  const { networkMonitor } = require('../../../services/offline/networkMonitor');
  return { monitor: networkMonitor, netinfo };
}

describe('networkMonitor', () => {
  it('começa online por padrão', () => {
    const { monitor } = fresh();
    expect(monitor.getIsOnline()).toBe(true);
  });

  it('subscribe entrega o estado atual na hora e devolve unsubscribe', () => {
    const { monitor } = fresh();
    const seen: boolean[] = [];
    const unsub = monitor.subscribe((v: boolean) => seen.push(v));

    expect(seen).toEqual([true]); // chamado imediatamente
    expect(typeof unsub).toBe('function');
  });

  it('init lê o estado inicial do NetInfo', async () => {
    const { monitor, netinfo } = fresh();
    netinfo.__setMockState({ isConnected: false, isInternetReachable: false });
    await monitor.init();
    expect(monitor.getIsOnline()).toBe(false);
  });

  it('só notifica quando o estado REALMENTE muda (online→offline)', async () => {
    const { monitor, netinfo } = fresh();
    await monitor.init();

    const changes: boolean[] = [];
    monitor.subscribe((v: boolean) => changes.push(v)); // recebe true na inscrição
    changes.length = 0;

    // Mesmo estado (online→online): não deve notificar.
    netinfo.__setMockState({ isConnected: true, isInternetReachable: true });
    expect(changes).toEqual([]);

    // Mudança real: perde a conexão.
    netinfo.__setMockState({ isConnected: false, isInternetReachable: true });
    expect(changes).toEqual([false]);
  });

  it('trata isInternetReachable null como online (rede ainda sem confirmação)', async () => {
    const { monitor, netinfo } = fresh();
    await monitor.init();
    netinfo.__setMockState({ isConnected: true, isInternetReachable: null });
    expect(monitor.getIsOnline()).toBe(true);
  });

  it('init é idempotente — a segunda chamada retorna cedo', async () => {
    const { monitor } = fresh();
    await monitor.init();
    await expect(monitor.init()).resolves.toBeUndefined();
  });

  it('unsubscribe para de receber notificações', async () => {
    const { monitor, netinfo } = fresh();
    await monitor.init();
    const changes: boolean[] = [];
    const unsub = monitor.subscribe((v: boolean) => changes.push(v));
    changes.length = 0;
    unsub();

    netinfo.__setMockState({ isConnected: false, isInternetReachable: false });
    expect(changes).toEqual([]);
  });

  it('_setOnlineForTests força o estado e notifica', () => {
    const { monitor } = fresh();
    const changes: boolean[] = [];
    monitor.subscribe((v: boolean) => changes.push(v));
    changes.length = 0;
    monitor._setOnlineForTests(false);
    expect(changes).toEqual([false]);
    expect(monitor.getIsOnline()).toBe(false);
  });
});
