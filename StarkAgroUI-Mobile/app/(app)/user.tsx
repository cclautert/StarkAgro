import React, { useState, useEffect } from 'react';
import {
  View, Text, Alert, ScrollView, ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { userService } from '../../services/userService';
import { useAuthStore } from '../../stores/authStore';
import { LabeledInput } from '../../components/ui/LabeledInput';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Colors } from '../../constants/colors';

export default function UserScreen() {
  const { userId, userName } = useAuthStore();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);

  useEffect(() => {
    if (!userId) { setFetching(false); return; }
    userService.getById(userId).then((u) => {
      setName(u.name ?? '');
      setEmail(u.email ?? '');
      setFetching(false);
    }).catch(() => setFetching(false));
  }, [userId]);

  const handleSave = async () => {
    if (!name.trim() || !email.trim()) {
      Alert.alert('Erro', 'Preencha o nome e o e-mail');
      return;
    }
    setLoading(true);
    try {
      await userService.update({
        name: name.trim(),
        email: email.trim(),
        ...(password.trim() ? { password: password.trim() } : {}),
        currentUserId: userId!,
      });
      Alert.alert('Sucesso', 'Perfil atualizado');
      setPassword('');
    } catch {
      Alert.alert('Erro', 'Não foi possível atualizar o perfil');
    } finally {
      setLoading(false);
    }
  };

  if (fetching) return <ActivityIndicator color={Colors.primary} style={{ flex: 1, backgroundColor: Colors.background }} />;

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <View style={{ alignItems: 'center', marginBottom: 24 }}>
          <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: Colors.card, justifyContent: 'center', alignItems: 'center', borderWidth: 2, borderColor: Colors.primary }}>
            <Ionicons name="person" size={36} color={Colors.primary} />
          </View>
          <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: '700', marginTop: 8 }}>
            {userName ?? name}
          </Text>
        </View>

        <Card>
          <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '600', marginBottom: 16 }}>
            Editar Perfil
          </Text>
          <LabeledInput label="Nome" value={name} onChangeText={setName} />
          <LabeledInput label="E-mail" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none" />
          <LabeledInput label="Nova Senha (opcional)" value={password} onChangeText={setPassword} secureTextEntry placeholder="Deixe em branco para não alterar" />
          <Button title="Salvar Alterações" onPress={handleSave} loading={loading} style={{ marginTop: 8 }} />
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}
