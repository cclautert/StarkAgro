import { useEffect } from 'react';
import { Alert, View, Text, ActivityIndicator, Platform } from 'react-native';
import { useRouter } from 'expo-router';
import * as Linking from 'expo-linking';
import { jwtDecode } from 'jwt-decode';
import { authService } from '../../services/authService';
import { useAuthStore } from '../../stores/authStore';
import { Colors } from '../../constants/colors';

interface JwtPayload {
  id: string;
  name?: string;
  email?: string;
}

async function exchangeCode(code: string, redirectUri: string, setAuth: (token: string, userId: number, userName: string) => Promise<void>, router: ReturnType<typeof useRouter>) {
  try {
    const result = await authService.externalLogin({
      provider: 'Google',
      code,
      redirectUri,
    });
    if (result?.token) {
      const decoded = jwtDecode<JwtPayload>(result.token);
      await setAuth(result.token, parseInt(decoded.id), decoded.name ?? decoded.email ?? 'Usuário');
      router.replace('/(app)/home');
    } else {
      router.replace('/(auth)/login');
    }
  } catch (error: any) {
    const status = error?.response?.status;
    if (status === 403) {
      Alert.alert('Acesso Bloqueado', 'Contate o suporte técnico.', [
        { text: 'OK', onPress: () => router.replace('/(auth)/login') }
      ]);
    } else {
      router.replace('/(auth)/login');
    }
  }
}

export default function CallbackScreen() {
  const router = useRouter();
  const { setAuth } = useAuthStore();

  // Web: parse code from window.location.search
  useEffect(() => {
    if (Platform.OS !== 'web' || typeof window === 'undefined') return;
    const params = new URLSearchParams(window.location.search);
    const code = params.get('code');
    if (!code) {
      router.replace('/(auth)/login');
      return;
    }
    const redirectUri = `${window.location.origin}/login/callback`;
    exchangeCode(code, redirectUri, setAuth, router);
  }, []);

  // Native: intercept deep-link agripeweb://callback?code=...
  const url = Linking.useURL();
  useEffect(() => {
    if (Platform.OS === 'web' || !url) return;
    const { queryParams } = Linking.parse(url);
    const code = queryParams?.code as string | undefined;
    if (!code) {
      router.replace('/(auth)/login');
      return;
    }
    exchangeCode(code, 'agripeweb://callback', setAuth, router);
  }, [url]);

  return (
    <View style={{ flex: 1, backgroundColor: Colors.background, justifyContent: 'center', alignItems: 'center' }}>
      <ActivityIndicator size="large" color={Colors.primary} />
      <Text style={{ color: Colors.textSecondary, marginTop: 16 }}>Autenticando...</Text>
    </View>
  );
}
