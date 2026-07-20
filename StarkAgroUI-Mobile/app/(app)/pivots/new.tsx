import React, { useState } from 'react';
import { View, Text, Alert, ScrollView, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { pivotService } from '../../../services/pivotService';
import { LabeledInput } from '../../../components/ui/LabeledInput';
import { Button } from '../../../components/ui/Button';
import { Colors } from '../../../constants/colors';

export default function NewPivotScreen() {
  const router = useRouter();
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSave = async () => {
    if (!name.trim()) { Alert.alert('Erro', 'Informe o nome do pivô'); return; }
    setLoading(true);
    try {
      await pivotService.add({ name: name.trim() });
      router.back();
    } catch {
      Alert.alert('Erro', 'Não foi possível criar o pivô');
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', padding: 16, borderBottomWidth: 1, borderBottomColor: Colors.cardBorder }}>
        <TouchableOpacity onPress={() => router.back()} style={{ marginRight: 12 }}>
          <Ionicons name="arrow-back" size={24} color={Colors.textPrimary} />
        </TouchableOpacity>
        <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: '700' }}>Novo Pivô</Text>
      </View>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <LabeledInput label="Nome do Pivô" value={name} onChangeText={setName} placeholder="Ex: Pivô Central" />
        <Button title="Salvar" onPress={handleSave} loading={loading} />
      </ScrollView>
    </SafeAreaView>
  );
}
