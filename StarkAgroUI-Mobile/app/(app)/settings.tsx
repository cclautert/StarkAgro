import React, { useState } from 'react';
import {
  View,
  Text,
  Switch,
  TextInput,
  ScrollView,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useSettingsStore } from '../../stores/settingsStore';
import { Card } from '../../components/ui/Card';
import { Colors } from '../../constants/colors';

export default function SettingsScreen() {
  const { humidityUpper, humidityLower, useNewDashboard, setHumidityUpper, setHumidityLower, setUseNewDashboard } =
    useSettingsStore();

  const [upperInput, setUpperInput] = useState(String(humidityUpper));
  const [lowerInput, setLowerInput] = useState(String(humidityLower));

  const applyLimits = () => {
    const upper = parseFloat(upperInput);
    const lower = parseFloat(lowerInput);
    if (isNaN(upper) || isNaN(lower)) {
      Alert.alert('Erro', 'Informe valores numéricos válidos');
      return;
    }
    if (lower >= upper) {
      Alert.alert('Erro', 'O limite inferior deve ser menor que o superior');
      return;
    }
    setHumidityUpper(upper);
    setHumidityLower(lower);
    Alert.alert('Salvo', 'Limites de umidade atualizados');
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <Text style={{ color: Colors.textPrimary, fontSize: 22, fontWeight: 'bold', marginBottom: 24 }}>
          Configurações
        </Text>

        {/* Dashboard selection */}
        <Card style={{ marginBottom: 16 }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
            <View style={{ flex: 1, marginRight: 12 }}>
              <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '600' }}>
                Usar Novo Dashboard
              </Text>
              <Text style={{ color: Colors.textSecondary, fontSize: 13, marginTop: 2 }}>
                {useNewDashboard
                  ? 'Exibindo o dashboard com gráfico e alertas'
                  : 'Exibindo o dashboard circular clássico'}
              </Text>
            </View>
            <Switch
              value={useNewDashboard}
              onValueChange={setUseNewDashboard}
              trackColor={{ false: Colors.cardBorder, true: Colors.primary }}
              thumbColor="#fff"
            />
          </View>
          <View
            style={{
              marginTop: 12,
              padding: 8,
              backgroundColor: Colors.background,
              borderRadius: 8,
              flexDirection: 'row',
              alignItems: 'center',
            }}
          >
            <Ionicons
              name={useNewDashboard ? 'bar-chart-outline' : 'radio-button-on-outline'}
              size={16}
              color={Colors.primary}
              style={{ marginRight: 6 }}
            />
            <Text style={{ color: Colors.primary, fontSize: 12 }}>
              {useNewDashboard ? 'Dashboard com gráfico de umidade ativo' : 'Dashboard circular ativo'}
            </Text>
          </View>
        </Card>

        {/* Humidity limits */}
        <Card style={{ marginBottom: 16 }}>
          <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '600', marginBottom: 4 }}>
            Limites de Umidade
          </Text>
          <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 16 }}>
            Linhas de referência exibidas no gráfico do dashboard
          </Text>

          {/* Upper limit */}
          <View style={{ marginBottom: 16 }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 6 }}>
              <View style={{ width: 12, height: 3, backgroundColor: Colors.limitUpper, borderRadius: 2, marginRight: 8 }} />
              <Text style={{ color: Colors.textSecondary, fontSize: 13 }}>
                Limite Superior (%) — atual: {humidityUpper}%
              </Text>
            </View>
            <TextInput
              style={{
                backgroundColor: Colors.background,
                color: Colors.textPrimary,
                borderWidth: 1,
                borderColor: Colors.limitUpper,
                borderRadius: 8,
                paddingHorizontal: 12,
                paddingVertical: 10,
                fontSize: 16,
              }}
              value={upperInput}
              onChangeText={setUpperInput}
              keyboardType="numeric"
              placeholder="Ex: 75"
              placeholderTextColor={Colors.textSecondary}
            />
          </View>

          {/* Lower limit */}
          <View style={{ marginBottom: 20 }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 6 }}>
              <View style={{ width: 12, height: 3, backgroundColor: Colors.limitLower, borderRadius: 2, marginRight: 8 }} />
              <Text style={{ color: Colors.textSecondary, fontSize: 13 }}>
                Limite Inferior (%) — atual: {humidityLower}%
              </Text>
            </View>
            <TextInput
              style={{
                backgroundColor: Colors.background,
                color: Colors.textPrimary,
                borderWidth: 1,
                borderColor: Colors.limitLower,
                borderRadius: 8,
                paddingHorizontal: 12,
                paddingVertical: 10,
                fontSize: 16,
              }}
              value={lowerInput}
              onChangeText={setLowerInput}
              keyboardType="numeric"
              placeholder="Ex: 25"
              placeholderTextColor={Colors.textSecondary}
            />
          </View>

          <TouchableOpacity
            onPress={applyLimits}
            style={{
              backgroundColor: Colors.primary,
              borderRadius: 8,
              paddingVertical: 12,
              alignItems: 'center',
            }}
          >
            <Text style={{ color: '#fff', fontWeight: '700', fontSize: 15 }}>Aplicar Limites</Text>
          </TouchableOpacity>
        </Card>

        {/* Info */}
        <Card>
          <Text style={{ color: Colors.textPrimary, fontSize: 15, fontWeight: '600', marginBottom: 8 }}>
            Sobre
          </Text>
          <Text style={{ color: Colors.textSecondary, fontSize: 13, lineHeight: 20 }}>
            StarkAgro Mobile — Monitoramento Agrícola IoT{'\n'}
            Versão 1.0.0
          </Text>
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}
