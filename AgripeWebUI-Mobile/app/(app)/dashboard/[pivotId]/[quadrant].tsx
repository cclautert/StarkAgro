import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { sensorService } from '../../../../services/sensorService';
import { readsService } from '../../../../services/readsService';
import { Sensor, ReadEntry } from '../../../../types/api';
import { Colors } from '../../../../constants/colors';
import { QUADRANT_NAME_TO_NUMBER, QUADRANT_LABELS } from '../../../../constants/api';
import { Card } from '../../../../components/ui/Card';
import { Picker } from '@react-native-picker/picker';
import { useSettingsStore } from '../../../../stores/settingsStore';
import { computeDailyData, TrendStats, TrendPoint, ProjectionPoint } from '../../../../services/trendAnalysis';
import { TrendChart } from '../../../../components/dashboard/TrendChart';
import { TrendStatsPanel } from '../../../../components/dashboard/TrendStatsPanel';

const DAY_OPTIONS = [
  { label: '7 dias', value: 7 },
  { label: '14 dias', value: 14 },
  { label: '30 dias', value: 30 },
];

export default function QuadrantDetailScreen() {
  const router = useRouter();
  const { pivotId, quadrant } = useLocalSearchParams<{ pivotId: string; quadrant: string }>();
  const quadranteNumber = QUADRANT_NAME_TO_NUMBER[quadrant] ?? 1;
  const { humidityUpper, humidityLower } = useSettingsStore();

  const [sensors, setSensors] = useState<Sensor[]>([]);
  const [selectedSensorId, setSelectedSensorId] = useState<number | null>(null);
  const [readings, setReadings] = useState<ReadEntry[]>([]);
  const [days, setDays] = useState(7);
  const [loading, setLoading] = useState(true);

  // Overlay toggles
  const [showTrend, setShowTrend] = useState(true);
  const [showMovingAvg, setShowMovingAvg] = useState(true);
  const [showProjection, setShowProjection] = useState(true);

  const loadSensors = useCallback(async () => {
    if (!pivotId) return;
    try {
      const data = await sensorService.getAllByPivotId(parseInt(pivotId), quadranteNumber);
      setSensors(data);
      if (data.length > 0) setSelectedSensorId(data[0].id);
    } catch {
      /* ignore */
    }
  }, [pivotId, quadranteNumber]);

  const loadReadings = useCallback(async () => {
    if (!selectedSensorId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const data = await readsService.getAllByPivotId(selectedSensorId, quadranteNumber, days);
      setReadings(data);
    } catch {
      /* ignore */
    } finally {
      setLoading(false);
    }
  }, [selectedSensorId, quadranteNumber, days]);

  useEffect(() => { loadSensors(); }, [loadSensors]);
  useEffect(() => { loadReadings(); }, [loadReadings]);

  useEffect(() => {
    const interval = setInterval(loadReadings, 60000);
    return () => clearInterval(interval);
  }, [loadReadings]);

  // Trend analysis computed from readings
  const { points, projection, stats } = useMemo(
    () => computeDailyData(readings, humidityLower, humidityUpper),
    [readings, humidityLower, humidityUpper]
  );

  const qLabel = QUADRANT_LABELS[quadranteNumber] ?? quadrant;

  return (
    <View style={{ flex: 1, backgroundColor: Colors.background }}>
      {/* Header */}
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          padding: 16,
          paddingTop: 52,
          backgroundColor: Colors.card,
          borderBottomWidth: 1,
          borderBottomColor: Colors.cardBorder,
        }}
      >
        <TouchableOpacity onPress={() => router.back()} style={{ marginRight: 12 }}>
          <Ionicons name="arrow-back" size={24} color={Colors.textPrimary} />
        </TouchableOpacity>
        <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: '700', flex: 1 }}>
          {qLabel}
        </Text>
      </View>

      <ScrollView contentContainerStyle={{ padding: 16 }}>
        {/* Sensor selector */}
        {sensors.length > 1 && (
          <Card style={{ marginBottom: 16 }}>
            <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 4 }}>Sensor</Text>
            <View style={{ backgroundColor: Colors.background, borderRadius: 8, overflow: 'hidden' }}>
              <Picker
                selectedValue={selectedSensorId}
                onValueChange={(val) => setSelectedSensorId(val as number)}
                style={{ color: Colors.textPrimary }}
                dropdownIconColor={Colors.textSecondary}
              >
                {sensors.map((s) => (
                  <Picker.Item key={s.id} label={s.name ?? s.code} value={s.id} color={Colors.textPrimary} />
                ))}
              </Picker>
            </View>
          </Card>
        )}

        {/* Day filter */}
        <View style={{ flexDirection: 'row', marginBottom: 16, gap: 8 }}>
          {DAY_OPTIONS.map((opt) => (
            <TouchableOpacity
              key={opt.value}
              onPress={() => setDays(opt.value)}
              style={{
                flex: 1,
                paddingVertical: 8,
                borderRadius: 8,
                backgroundColor: days === opt.value ? Colors.primary : Colors.card,
                borderWidth: 1,
                borderColor: Colors.cardBorder,
                alignItems: 'center',
              }}
            >
              <Text style={{ color: days === opt.value ? '#fff' : Colors.textSecondary, fontWeight: '600', fontSize: 13 }}>
                {opt.label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        {/* Overlay toggles */}
        <View style={{ flexDirection: 'row', gap: 6, marginBottom: 16, flexWrap: 'wrap' }}>
          <OverlayToggle
            label="Tendência"
            color={Colors.trendLine}
            active={showTrend}
            onPress={() => setShowTrend((v) => !v)}
          />
          <OverlayToggle
            label="Méd. Móvel"
            color={Colors.movingAvg}
            active={showMovingAvg}
            onPress={() => setShowMovingAvg((v) => !v)}
          />
          <OverlayToggle
            label="Projeção"
            color={Colors.projection}
            active={showProjection}
            onPress={() => setShowProjection((v) => !v)}
          />
        </View>

        {/* Chart */}
        <Card style={{ marginBottom: 16 }}>
          <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '700', marginBottom: 16 }}>
            Leituras do Sensor
          </Text>
          {loading ? (
            <ActivityIndicator color={Colors.primary} style={{ paddingVertical: 40 }} />
          ) : (
            <TrendChart
              points={points}
              projection={projection}
              humidityUpper={humidityUpper}
              humidityLower={humidityLower}
              showTrend={showTrend}
              showMovingAvg={showMovingAvg}
              showProjection={showProjection}
            />
          )}
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: 8 }}>
            <Text style={{ color: Colors.textSecondary, fontSize: 12 }}>
              {readings.length} leituras
            </Text>
            <Text style={{ color: Colors.textSecondary, fontSize: 12 }}>
              Últ. {days} dias
            </Text>
          </View>
        </Card>

        {/* Trend stats panel */}
        {!loading && points.length > 0 && (
          <Card style={{ marginBottom: 16 }}>
            <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '700', marginBottom: 12 }}>
              Análise de Tendência
            </Text>
            <TrendStatsPanel stats={stats} />
          </Card>
        )}
      </ScrollView>
    </View>
  );
}

function OverlayToggle({
  label,
  color,
  active,
  onPress,
}: {
  label: string;
  color: string;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        paddingHorizontal: 10,
        paddingVertical: 5,
        borderRadius: 20,
        borderWidth: 1,
        borderColor: active ? color : Colors.cardBorder,
        backgroundColor: active ? color + '22' : Colors.card,
        gap: 5,
      }}
    >
      <View
        style={{
          width: 8,
          height: 8,
          borderRadius: 4,
          backgroundColor: active ? color : Colors.textSecondary,
        }}
      />
      <Text style={{ color: active ? color : Colors.textSecondary, fontSize: 12, fontWeight: '600' }}>
        {label}
      </Text>
    </TouchableOpacity>
  );
}
