import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import api from './api';

export async function registerForPushNotificationsAsync(): Promise<string | null> {
  if (Platform.OS === 'web') return null;

  const { status: existingStatus } = await Notifications.getPermissionsAsync();
  let finalStatus = existingStatus;

  if (existingStatus !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }

  if (finalStatus !== 'granted') return null;

  const tokenData = await Notifications.getExpoPushTokenAsync();
  return tokenData.data;
}

export async function registerTokenWithBackend(token: string): Promise<void> {
  await api.put('user/pushToken', { token });
}
