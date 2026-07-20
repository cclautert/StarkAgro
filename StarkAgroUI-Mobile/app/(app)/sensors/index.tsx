import React, { useState, useCallback } from 'react';
import { View, Text, FlatList, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';
import { useRouter, useFocusEffect } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { sensorService } from '../../../services/sensorService';
import { Sensor } from '../../../types/api';
import { Card } from '../../../components/ui/Card';
import { Colors } from '../../../constants/colors';
import { QUADRANT_LABELS } from '../../../constants/api';

export default function SensorsScreen() {
  const router = useRouter();
  const [sensors, setSensors] = useState<Sensor[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSensors(await sensorService.getAll());
    } catch {
      Alert.alert('Erro', 'Não foi possível carregar os sensores');
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  const handleDelete = (sensor: Sensor) => {
    Alert.alert('Excluir', `Deseja excluir "${sensor.name ?? sensor.code}"?`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Excluir', style: 'destructive',
        onPress: async () => {
          try { await sensorService.delete(sensor.id); load(); }
          catch { Alert.alert('Erro', 'Não foi possível excluir'); }
        },
      },
    ]);
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 16, paddingBottom: 8 }}>
        <Text style={{ color: Colors.textPrimary, fontSize: 22, fontWeight: 'bold' }}>Sensores</Text>
        <TouchableOpacity
          onPress={() => router.push('/(app)/sensors/new')}
          style={{ backgroundColor: Colors.primary, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8, flexDirection: 'row', alignItems: 'center' }}
        >
          <Ionicons name="add" size={18} color="#fff" />
          <Text style={{ color: '#fff', fontWeight: '700', marginLeft: 4 }}>Novo</Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <ActivityIndicator color={Colors.primary} style={{ marginTop: 40 }} />
      ) : (
        <FlatList
          data={sensors}
          keyExtractor={(item) => String(item.id)}
          contentContainerStyle={{ padding: 16, paddingTop: 8 }}
          ListEmptyComponent={
            <Text style={{ color: Colors.textSecondary, textAlign: 'center', marginTop: 40 }}>
              Nenhum sensor cadastrado
            </Text>
          }
          renderItem={({ item }) => (
            <Card style={{ marginBottom: 10 }}>
              <View style={{ flexDirection: 'row', alignItems: 'center' }}>
                <Ionicons name="radio-outline" size={20} color={Colors.primary} style={{ marginRight: 12 }} />
                <View style={{ flex: 1 }}>
                  <Text style={{ color: Colors.textPrimary, fontSize: 15, fontWeight: '600' }}>
                    {item.name ?? item.code}
                  </Text>
                  <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 2 }}>
                    {item.pivot?.name ?? `Pivô ${item.pivot?.id}`} • {QUADRANT_LABELS[item.quadrante] ?? `Q${item.quadrante}`} • {item.code}
                  </Text>
                </View>
                <TouchableOpacity onPress={() => router.push(`/(app)/sensors/${item.id}/edit`)} style={{ marginRight: 12 }}>
                  <Ionicons name="pencil-outline" size={20} color={Colors.textSecondary} />
                </TouchableOpacity>
                <TouchableOpacity onPress={() => handleDelete(item)}>
                  <Ionicons name="trash-outline" size={20} color={Colors.danger} />
                </TouchableOpacity>
              </View>
            </Card>
          )}
        />
      )}
    </SafeAreaView>
  );
}
