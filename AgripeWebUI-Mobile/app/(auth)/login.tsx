import { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { jwtDecode } from 'jwt-decode';
import { Ionicons } from '@expo/vector-icons';
import * as WebBrowser from 'expo-web-browser';
import { authService } from '../../services/authService';
import { useAuthStore } from '../../stores/authStore';
import { Colors } from '../../constants/colors';
import { GOOGLE_CLIENT_ID } from '../../constants/api';

interface JwtPayload {
  id: string;
  name?: string;
  email?: string;
}

function buildGoogleOAuthUrl(redirectUri: string): string {
  const params = new URLSearchParams({
    client_id: GOOGLE_CLIENT_ID,
    redirect_uri: redirectUri,
    response_type: 'code',
    scope: 'email profile openid',
    access_type: 'offline',
    prompt: 'select_account',
  });
  return `https://accounts.google.com/o/oauth2/v2/auth?${params.toString()}`;
}

export default function LoginScreen() {
  const router = useRouter();
  const { setAuth } = useAuthStore();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);

  const googleEnabled = Boolean(GOOGLE_CLIENT_ID);

  const handleLogin = async () => {
    if (!email.trim() || !password.trim()) {
      Alert.alert('Erro', 'Preencha o e-mail e a senha');
      return;
    }
    try {
      setLoading(true);
      const { token } = await authService.login({ email: email.trim(), password });
      const decoded = jwtDecode<JwtPayload>(token);
      await setAuth(token, parseInt(decoded.id), decoded.name ?? decoded.email ?? 'Usuário');
      router.replace('/(app)/home');
    } catch (error: any) {
      const status = error?.response?.status;
      if (status === 403) {
        Alert.alert('Acesso Bloqueado', 'Contate o suporte técnico.');
      } else if (status === 429) {
        Alert.alert('Muitas tentativas', 'Tente novamente em alguns minutos.');
      } else {
        Alert.alert('Erro de autenticação', 'E-mail ou senha inválidos. Verifique seus dados.');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleLogin = async () => {
    if (!googleEnabled) return;
    setGoogleLoading(true);
    try {
      const redirectUri =
        Platform.OS === 'web' && typeof window !== 'undefined'
          ? `${window.location.origin}/login/callback`
          : 'agripeweb://callback';

      const oauthUrl = buildGoogleOAuthUrl(redirectUri);

      if (Platform.OS === 'web') {
        // On web: navigate directly (Google redirects back with ?code=...)
        window.location.href = oauthUrl;
      } else {
        // On native: open in-app browser and intercept deep link
        await WebBrowser.openAuthSessionAsync(oauthUrl, 'agripeweb://callback');
      }
    } catch {
      Alert.alert('Erro', 'Falha ao iniciar login com Google');
      setGoogleLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: Colors.background }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={{
          flexGrow: 1,
          justifyContent: 'center',
          alignItems: 'center',
          padding: 24,
        }}
        keyboardShouldPersistTaps="handled"
      >
        {/* Header / Logo */}
        <View style={{ alignItems: 'center', marginBottom: 40 }}>
          <View
            style={{
              width: 80,
              height: 80,
              borderRadius: 40,
              backgroundColor: Colors.card,
              justifyContent: 'center',
              alignItems: 'center',
              borderWidth: 2,
              borderColor: Colors.success,
              marginBottom: 16,
            }}
          >
            <Ionicons name="leaf" size={40} color={Colors.success} />
          </View>
          <Text style={{ color: Colors.textPrimary, fontSize: 28, fontWeight: 'bold' }}>
            AgripeWeb
          </Text>
          <Text style={{ color: Colors.textSecondary, fontSize: 14, marginTop: 4 }}>
            Monitoramento Agrícola Inteligente
          </Text>
        </View>

        {/* Card */}
        <View
          style={{
            width: '100%',
            maxWidth: 420,
            backgroundColor: Colors.card,
            borderRadius: 16,
            padding: 28,
            borderWidth: 1,
            borderColor: Colors.cardBorder,
            shadowColor: '#000',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: 0.3,
            shadowRadius: 12,
            elevation: 8,
          }}
        >
          <Text
            style={{
              color: Colors.textPrimary,
              fontSize: 22,
              fontWeight: '700',
              marginBottom: 24,
              textAlign: 'center',
            }}
          >
            Entrar
          </Text>

          {/* Email field */}
          <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 6 }}>
            E-mail
          </Text>
          <View
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              backgroundColor: Colors.background,
              borderRadius: 10,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              marginBottom: 16,
              paddingHorizontal: 14,
            }}
          >
            <Ionicons name="mail-outline" size={18} color={Colors.textSecondary} />
            <TextInput
              style={{
                flex: 1,
                color: Colors.textPrimary,
                paddingVertical: 13,
                paddingLeft: 10,
                fontSize: 15,
              }}
              placeholder="seu@email.com"
              placeholderTextColor={Colors.textSecondary}
              value={email}
              onChangeText={setEmail}
              keyboardType="email-address"
              autoCapitalize="none"
              autoComplete="email"
              returnKeyType="next"
            />
          </View>

          {/* Password field */}
          <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 6 }}>
            Senha
          </Text>
          <View
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              backgroundColor: Colors.background,
              borderRadius: 10,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              marginBottom: 24,
              paddingHorizontal: 14,
            }}
          >
            <Ionicons name="lock-closed-outline" size={18} color={Colors.textSecondary} />
            <TextInput
              style={{
                flex: 1,
                color: Colors.textPrimary,
                paddingVertical: 13,
                paddingLeft: 10,
                fontSize: 15,
              }}
              placeholder="••••••••"
              placeholderTextColor={Colors.textSecondary}
              value={password}
              onChangeText={setPassword}
              secureTextEntry={!showPassword}
              autoComplete="password"
              returnKeyType="done"
              onSubmitEditing={handleLogin}
            />
            <TouchableOpacity onPress={() => setShowPassword(!showPassword)} style={{ padding: 4 }}>
              <Ionicons
                name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                size={20}
                color={Colors.textSecondary}
              />
            </TouchableOpacity>
          </View>

          {/* Login button */}
          <TouchableOpacity
            onPress={handleLogin}
            disabled={loading || googleLoading}
            style={{
              backgroundColor: Colors.primary,
              borderRadius: 10,
              paddingVertical: 14,
              alignItems: 'center',
              opacity: loading || googleLoading ? 0.7 : 1,
            }}
          >
            {loading ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={{ color: '#fff', fontWeight: '700', fontSize: 16 }}>Entrar</Text>
            )}
          </TouchableOpacity>

          {/* Google OAuth divider + button */}
          {googleEnabled && (
            <>
              <View
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  marginVertical: 20,
                }}
              >
                <View style={{ flex: 1, height: 1, backgroundColor: Colors.cardBorder }} />
                <Text
                  style={{
                    color: Colors.textSecondary,
                    fontSize: 12,
                    marginHorizontal: 12,
                  }}
                >
                  ou continue com
                </Text>
                <View style={{ flex: 1, height: 1, backgroundColor: Colors.cardBorder }} />
              </View>

              <TouchableOpacity
                onPress={handleGoogleLogin}
                disabled={loading || googleLoading}
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: '#fff',
                  borderRadius: 10,
                  paddingVertical: 13,
                  borderWidth: 1,
                  borderColor: '#E2E8F0',
                  opacity: loading || googleLoading ? 0.7 : 1,
                }}
              >
                {googleLoading ? (
                  <ActivityIndicator color={Colors.primary} />
                ) : (
                  <>
                    {/* Google "G" icon using text */}
                    <View
                      style={{
                        width: 22,
                        height: 22,
                        borderRadius: 11,
                        backgroundColor: '#4285F4',
                        justifyContent: 'center',
                        alignItems: 'center',
                        marginRight: 10,
                      }}
                    >
                      <Text style={{ color: '#fff', fontWeight: 'bold', fontSize: 13 }}>G</Text>
                    </View>
                    <Text
                      style={{ color: '#374151', fontWeight: '600', fontSize: 15 }}
                    >
                      Entrar com Google
                    </Text>
                  </>
                )}
              </TouchableOpacity>
            </>
          )}
        </View>

        {/* Footer */}
        <Text
          style={{
            color: Colors.textSecondary,
            fontSize: 12,
            marginTop: 24,
            textAlign: 'center',
          }}
        >
          © 2025 AgripeWeb — Sistema de Monitoramento Agrícola
        </Text>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
