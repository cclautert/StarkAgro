import React, { useState, useEffect } from 'react';
import { View, Text, Alert, ScrollView, TouchableOpacity, ActivityIndicator } from 'react-native';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { pivotService } from '../../../../services/pivotService';
import { LabeledInput } from '../../../../components/ui/LabeledInput';
import { Button } from '../../../../components/ui/Button';
import { Colors } from '../../../../constants/colors';

export default function EditPivotScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);

  useEffect(() => {
    pivotService.getById(parseInt(id)).then((p) => {
      setName(p.name ?? '');
      setFetching(false);
    }).catch(() => {
      Alert.alert('Erro', 'Pivô não encontrado');
      router.back();
    });
  }, [id]);

  const handleSave = async () => {
    if (!name.trim()) { Alert.alert('Erro', 'Informe o nome'); return; }
    setLoading(true);
    try {
      await pivotService.update({ id: parseInt(id), name: name.trim() });
      router.back();
    } catch {
      Alert.alert('Erro', 'Não foi possível atualizar o pivô');
    } finally {
      setLoading(false);
    }
  };

  if (fetching) return <ActivityIndicator color={Colors.primary} style={{ flex: 1, backgroundColor: Colors.background }} />;

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', padding: 16, borderBottomWidth: 1, borderBottomColor: Colors.cardBorder }}>
        <TouchableOpacity onPress={() => router.back()} style={{ marginRight: 12 }}>
          <Ionicons name="arrow-back" size={24} color={Colors.textPrimary} />
        </TouchableOpacity>
        <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: '700' }}>Editar Pivô</Text>
      </View>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <LabeledInput label="Nome do Pivô" value={name} onChangeText={setName} />
        <Button title="Salvar" onPress={handleSave} loading={loading} />
      </ScrollView>
    </SafeAreaView>
  );
}
