import React, { useState, useCallback } from 'react';
import {
  View, Text, FlatList, TouchableOpacity, Alert, ActivityIndicator,
} from 'react-native';
import { useRouter, useFocusEffect } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { pivotService } from '../../../services/pivotService';
import { Pivot } from '../../../types/api';
import { Card } from '../../../components/ui/Card';
import { Colors } from '../../../constants/colors';

export default function PivotsScreen() {
  const router = useRouter();
  const [pivots, setPivots] = useState<Pivot[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setPivots(await pivotService.getAll());
    } catch {
      Alert.alert('Erro', 'Não foi possível carregar os pivôs');
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  const handleDelete = (pivot: Pivot) => {
    Alert.alert('Excluir', `Deseja excluir "${pivot.name}"?`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Excluir',
        style: 'destructive',
        onPress: async () => {
          try {
            await pivotService.delete(pivot.id);
            load();
          } catch {
            Alert.alert('Erro', 'Não foi possível excluir o pivô');
          }
        },
      },
    ]);
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 16, paddingBottom: 8 }}>
        <Text style={{ color: Colors.textPrimary, fontSize: 22, fontWeight: 'bold' }}>Pivôs</Text>
        <TouchableOpacity
          onPress={() => router.push('/(app)/pivots/new')}
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
          data={pivots}
          keyExtractor={(item) => String(item.id)}
          contentContainerStyle={{ padding: 16, paddingTop: 8 }}
          ListEmptyComponent={
            <Text style={{ color: Colors.textSecondary, textAlign: 'center', marginTop: 40 }}>
              Nenhum pivô cadastrado
            </Text>
          }
          renderItem={({ item }) => (
            <Card style={{ marginBottom: 10, flexDirection: 'row', alignItems: 'center' }}>
              <Ionicons name="git-branch-outline" size={20} color={Colors.primary} style={{ marginRight: 12 }} />
              <Text style={{ color: Colors.textPrimary, fontSize: 16, flex: 1 }}>{item.name}</Text>
              <TouchableOpacity onPress={() => router.push(`/(app)/pivots/${item.id}/edit`)} style={{ marginRight: 12 }}>
                <Ionicons name="pencil-outline" size={20} color={Colors.textSecondary} />
              </TouchableOpacity>
              <TouchableOpacity onPress={() => handleDelete(item)}>
                <Ionicons name="trash-outline" size={20} color={Colors.danger} />
              </TouchableOpacity>
            </Card>
          )}
        />
      )}
    </SafeAreaView>
  );
}
