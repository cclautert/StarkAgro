import { create } from 'zustand';
import { tokenStorage } from '../services/tokenStorage';

interface AuthState {
  token: string | null;
  userId: number | null;
  userName: string | null;
  isHydrated: boolean;
  setAuth: (token: string, userId: number, userName: string) => Promise<void>;
  logout: () => Promise<void>;
  hydrate: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set) => ({
  token: null,
  userId: null,
  userName: null,
  isHydrated: false,

  setAuth: async (token, userId, userName) => {
    await tokenStorage.setToken(token);
    set({ token, userId, userName });
  },

  logout: async () => {
    await tokenStorage.removeToken();
    set({ token: null, userId: null, userName: null });
  },

  hydrate: async () => {
    const token = await tokenStorage.getToken();
    set({ token, isHydrated: true });
  },
}));
