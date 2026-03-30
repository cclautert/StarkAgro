import { act, renderHook } from '@testing-library/react-native';
import { useSettingsStore } from '../../stores/settingsStore';

// Reset to defaults between tests
beforeEach(async () => {
  const { result } = renderHook(() => useSettingsStore());
  await act(async () => {
    result.current.setHumidityUpper(75);
    result.current.setHumidityLower(25);
    result.current.setUseNewDashboard(true);
  });
});

describe('settingsStore', () => {
  it('has correct default values', () => {
    const { result } = renderHook(() => useSettingsStore());
    expect(result.current.humidityUpper).toBe(75);
    expect(result.current.humidityLower).toBe(25);
    expect(result.current.useNewDashboard).toBe(true);
  });

  it('setHumidityUpper updates upper limit', async () => {
    const { result } = renderHook(() => useSettingsStore());
    await act(async () => {
      result.current.setHumidityUpper(80);
    });
    expect(result.current.humidityUpper).toBe(80);
  });

  it('setHumidityLower updates lower limit', async () => {
    const { result } = renderHook(() => useSettingsStore());
    await act(async () => {
      result.current.setHumidityLower(30);
    });
    expect(result.current.humidityLower).toBe(30);
  });

  it('setUseNewDashboard toggles the flag', async () => {
    const { result } = renderHook(() => useSettingsStore());
    await act(async () => {
      result.current.setUseNewDashboard(false);
    });
    expect(result.current.useNewDashboard).toBe(false);
  });

  it('persists settings to AsyncStorage', async () => {
    const { result } = renderHook(() => useSettingsStore());
    await act(async () => {
      result.current.setHumidityUpper(90);
    });
    const AsyncStorage = require('@react-native-async-storage/async-storage');
    const raw = await AsyncStorage.getItem('agripeweb-settings');
    expect(raw).not.toBeNull();
    const parsed = JSON.parse(raw!);
    expect(parsed.state.humidityUpper).toBe(90);
  });
});
