import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  ScrollView,
  ActivityIndicator,
  TouchableOpacity,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import { usePivots } from '../../hooks/usePivots';
import { useDashboardData } from '../../hooks/useDashboardData';
import { useSettingsStore } from '../../stores/settingsStore';
import { QuadrantCard } from './QuadrantCard';
import { HumidityChart } from './HumidityChart';
import { AlertBanner } from '../ui/AlertBanner';
import { Card } from '../ui/Card';
import { LoadingSpinner } from '../ui/LoadingSpinner';
import { Colors } from '../../constants/colors';
import { QUADRANT_NUMBER_TO_NAME } from '../../constants/api';

const DAY_OPTIONS = [
  { label: '7 dias', value: 7 },
  { label: '14 dias', value: 14 },
  { label: '30 dias', value: 30 },
];

// Maps QuadranteData keys to quadrant numbers
const QUADRANT_AVG_KEYS: Record<number, string> = {
  1: 'topRightAvg',
  2: 'bottomRightAvg',
  3: 'bottomLeftAvg',
  4: 'topLeftAvg',
};

function getAvg(pivot: { quadrante?: Record<string, number | undefined> } | null, q: number): number | null {
  if (!pivot?.quadrante) return null;
  const key = QUADRANT_AVG_KEYS[q];
  const val = pivot.quadrante[key];
  return val !== undefined ? val : null;
}

export function NewDashboard() {
  const router = useRouter();
  const { pivots, loading: pivotsLoading } = usePivots();
  const { humidityUpper, humidityLower } = useSettingsStore();
  const [selectedPivotId, setSelectedPivotId] = useState<number | null>(null);
  const [days, setDays] = useState(7);

  const selectedPivot = pivots.find((p) => p.id === selectedPivotId) ?? null;

  useEffect(() => {
    if (pivots.length > 0 && !selectedPivotId) {
      setSelectedPivotId(pivots[0].id);
    }
  }, [pivots]);

  const { pivot, chartData, loading } = useDashboardData(selectedPivotId, days);

  const quadrantAvgs = [1, 2, 3, 4].map((q) => ({
    q,
    avg: getAvg(pivot as { quadrante?: Record<string, number | undefined> } | null, q),
  }));

  const alerts = quadrantAvgs.filter(
    ({ avg }) => avg !== null && (avg < humidityLower || avg > humidityUpper)
  );

  if (pivotsLoading) return <LoadingSpinner message="Carregando pivôs..." />;

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: Colors.background }}
      contentContainerStyle={{ padding: 16 }}
    >
      {/* Header */}
      <Text style={{ color: Colors.textPrimary, fontSize: 20, fontWeight: 'bold', marginBottom: 4 }}>
        Controle de Irrigação
      </Text>
      <Text style={{ color: Colors.textSecondary, fontSize: 14, marginBottom: 16 }}>
        {selectedPivot?.name ?? 'Selecione um pivô'}
      </Text>

      {/* Pivot selector */}
      <Card style={{ marginBottom: 16 }}>
        <Text style={{ color: Colors.textSecondary, fontSize: 12, marginBottom: 4 }}>Filtro: Pivô</Text>
        <View style={{ backgroundColor: Colors.background, borderRadius: 8, overflow: 'hidden' }}>
          <Picker
            selectedValue={selectedPivotId}
            onValueChange={(val) => setSelectedPivotId(val as number)}
            style={{ color: Colors.textPrimary }}
            dropdownIconColor={Colors.textSecondary}
          >
            {pivots.map((p) => (
              <Picker.Item key={p.id} label={p.name ?? `Pivô ${p.id}`} value={p.id} color={Colors.textPrimary} />
            ))}
          </Picker>
        </View>
      </Card>

      {/* Quadrant cards */}
      <View style={{ flexDirection: 'row', gap: 8, marginBottom: 16 }}>
        {[1, 2].map((q) => (
          <QuadrantCard
            key={q}
            quadranteNumber={q}
            avg={quadrantAvgs[q - 1].avg}
            humidityUpper={humidityUpper}
            humidityLower={humidityLower}
            onPress={() =>
              selectedPivotId &&
              router.push(`/(app)/dashboard/${selectedPivotId}/${QUADRANT_NUMBER_TO_NAME[q]}`)
            }
          />
        ))}
      </View>
      <View style={{ flexDirection: 'row', gap: 8, marginBottom: 16 }}>
        {[3, 4].map((q) => (
          <QuadrantCard
            key={q}
            quadranteNumber={q}
            avg={quadrantAvgs[q - 1].avg}
            humidityUpper={humidityUpper}
            humidityLower={humidityLower}
            onPress={() =>
              selectedPivotId &&
              router.push(`/(app)/dashboard/${selectedPivotId}/${QUADRANT_NUMBER_TO_NAME[q]}`)
            }
          />
        ))}
      </View>

      {/* Chart */}
      <Card style={{ marginBottom: 16 }}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <Text style={{ color: Colors.textPrimary, fontSize: 15, fontWeight: '700' }}>
            Níveis de Umidade do Solo
          </Text>
          <View style={{ flexDirection: 'row', gap: 4 }}>
            {DAY_OPTIONS.map((opt) => (
              <TouchableOpacity
                key={opt.value}
                onPress={() => setDays(opt.value)}
                style={{
                  paddingHorizontal: 8,
                  paddingVertical: 4,
                  borderRadius: 6,
                  backgroundColor: days === opt.value ? Colors.primary : Colors.cardBorder,
                }}
              >
                <Text style={{ color: '#fff', fontSize: 11, fontWeight: '600' }}>{opt.label}</Text>
              </TouchableOpacity>
            ))}
          </View>
        </View>

        {loading ? (
          <ActivityIndicator color={Colors.primary} style={{ paddingVertical: 40 }} />
        ) : (
          <HumidityChart
            chartData={chartData}
            humidityUpper={humidityUpper}
            humidityLower={humidityLower}
          />
        )}
      </Card>

      {/* Alert banners */}
      {alerts.length > 0 && (
        <View style={{ marginBottom: 16 }}>
          {alerts.map(({ q, avg }) => {
            const isLow = avg !== null && avg < humidityLower;
            return (
              <AlertBanner
                key={q}
                type={isLow ? 'low' : 'high'}
                message={
                  isLow
                    ? `Alerta! Umidade Baixa no Quadrante ${q}! Enviando notificação...`
                    : `Alerta! Umidade Alta no Quadrante ${q}! Enviando notificação...`
                }
              />
            );
          })}
        </View>
      )}
    </ScrollView>
  );
}
