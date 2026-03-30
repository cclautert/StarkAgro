import React, { useState, useEffect } from 'react';
import { View, Text, Alert, ScrollView, TouchableOpacity } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { Picker } from '@react-native-picker/picker';
import { sensorService } from '../../../services/sensorService';
import { pivotService } from '../../../services/pivotService';
import { Pivot } from '../../../types/api';
import { LabeledInput } from '../../../components/ui/LabeledInput';
import { Button } from '../../../components/ui/Button';
import { Card } from '../../../components/ui/Card';
import { Colors } from '../../../constants/colors';

export default function NewSensorScreen() {
  const router = useRouter();
  const [pivots, setPivots] = useState<Pivot[]>([]);
  const [selectedPivotId, setSelectedPivotId] = useState<number | null>(null);
  const [quadrante, setQuadrante] = useState<number>(1);
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    pivotService.getAll().then((data) => {
      setPivots(data);
      if (data.length > 0) setSelectedPivotId(data[0].id);
    });
  }, []);

  const handleSave = async () => {
    if (!code.trim()) { Alert.alert('Erro', 'Informe o código do sensor'); return; }
    if (!selectedPivotId) { Alert.alert('Erro', 'Selecione um pivô'); return; }
    const pivot = pivots.find((p) => p.id === selectedPivotId)!;
    setLoading(true);
    try {
      await sensorService.add({ name: name.trim(), code: code.trim(), quadrante, pivot: { id: pivot.id, name: pivot.name ?? '' } });
      router.back();
    } catch {
      Alert.alert('Erro', 'Não foi possível criar o sensor');
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
        <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: '700' }}>Novo Sensor</Text>
      </View>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <LabeledInput label="Nome (opcional)" value={name} onChangeText={setName} placeholder="Ex: Sensor Norte" />
        <LabeledInput label="Código *" value={code} onChangeText={setCode} placeholder="Ex: SNS-001" autoCapitalize="characters" />

        <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 6 }}>Pivô</Text>
        <Card style={{ marginBottom: 16 }}>
          <Picker selectedValue={selectedPivotId} onValueChange={(v) => setSelectedPivotId(v as number)} style={{ color: Colors.textPrimary }} dropdownIconColor={Colors.textSecondary}>
            {pivots.map((p) => <Picker.Item key={p.id} label={p.name ?? `Pivô ${p.id}`} value={p.id} color={Colors.textPrimary} />)}
          </Picker>
        </Card>

        <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 6 }}>Quadrante</Text>
        <Card style={{ marginBottom: 20 }}>
          <Picker selectedValue={quadrante} onValueChange={(v) => setQuadrante(v as number)} style={{ color: Colors.textPrimary }} dropdownIconColor={Colors.textSecondary}>
            {[1, 2, 3, 4].map((q) => <Picker.Item key={q} label={`Quadrante ${q}`} value={q} color={Colors.textPrimary} />)}
          </Picker>
        </Card>

        <Button title="Salvar" onPress={handleSave} loading={loading} />
      </ScrollView>
    </SafeAreaView>
  );
}
