import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';

const KEY = 'agripeweb_token';

export const tokenStorage = {
  async getToken(): Promise<string | null> {
    if (Platform.OS === 'web') {
      return typeof window !== 'undefined' ? localStorage.getItem(KEY) : null;
    }
    return SecureStore.getItemAsync(KEY);
  },
  async setToken(token: string): Promise<void> {
    if (Platform.OS === 'web') {
      if (typeof window !== 'undefined') localStorage.setItem(KEY, token);
      return;
    }
    return SecureStore.setItemAsync(KEY, token);
  },
  async removeToken(): Promise<void> {
    if (Platform.OS === 'web') {
      if (typeof window !== 'undefined') localStorage.removeItem(KEY);
      return;
    }
    return SecureStore.deleteItemAsync(KEY);
  },
};
