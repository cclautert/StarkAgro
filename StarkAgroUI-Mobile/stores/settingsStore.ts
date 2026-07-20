import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';

interface SettingsState {
  humidityUpper: number;
  humidityLower: number;
  useNewDashboard: boolean;
  setHumidityUpper: (v: number) => void;
  setHumidityLower: (v: number) => void;
  setUseNewDashboard: (v: boolean) => void;
}

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      humidityUpper: 75,
      humidityLower: 25,
      useNewDashboard: true,
      setHumidityUpper: (v) => set({ humidityUpper: v }),
      setHumidityLower: (v) => set({ humidityLower: v }),
      setUseNewDashboard: (v) => set({ useNewDashboard: v }),
    }),
    {
      name: 'starkagro-settings',
      storage: createJSONStorage(() => AsyncStorage),
    }
  )
);
