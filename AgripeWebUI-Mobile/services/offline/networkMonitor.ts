import NetInfo, { NetInfoState } from '@react-native-community/netinfo';

export type NetworkListener = (isOnline: boolean) => void;

const listeners = new Set<NetworkListener>();
let currentOnline = true;
let unsubscribeNetInfo: (() => void) | null = null;

function deriveOnline(state: NetInfoState): boolean {
  return state.isConnected === true && state.isInternetReachable !== false;
}

export const networkMonitor = {
  getIsOnline(): boolean {
    return currentOnline;
  },

  subscribe(listener: NetworkListener): () => void {
    listeners.add(listener);
    listener(currentOnline);
    return () => listeners.delete(listener);
  },

  async init(): Promise<void> {
    if (unsubscribeNetInfo) return;

    const state = await NetInfo.fetch();
    currentOnline = deriveOnline(state);

    unsubscribeNetInfo = NetInfo.addEventListener((next) => {
      const wasOnline = currentOnline;
      currentOnline = deriveOnline(next);
      if (wasOnline !== currentOnline) {
        listeners.forEach((listener) => listener(currentOnline));
      }
    });
  },

  /** Test helper */
  _setOnlineForTests(value: boolean): void {
    currentOnline = value;
    listeners.forEach((listener) => listener(currentOnline));
  },
};
