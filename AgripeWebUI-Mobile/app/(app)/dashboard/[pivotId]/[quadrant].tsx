import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Dimensions,
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

const { width } = Dimensions.get('window');

const DAY_OPTIONS = [
  { label: '5 dias', value: 5 },
  { label: '10 dias', value: 10 },
  { label: '30 dias', value: 30 },
];

function formatDate(dateStr: string): string {
  const d = new Date(dateStr);
  const day = d.getDate().toString().padStart(2, '0');
  const month = (d.getMonth() + 1).toString().padStart(2, '0');
  const hours = d.getHours().toString().padStart(2, '0');
  const mins = d.getMinutes().toString().padStart(2, '0');
  return `${day}/${month} ${hours}:${mins}`;
}

function SimpleLineChart({ readings }: { readings: ReadEntry[] }) {
  if (!readings.length) {
    return (
      <View style={{ height: 160, justifyContent: 'center', alignItems: 'center' }}>
        <Text style={{ color: Colors.textSecondary }}>Sem dados disponíveis</Text>
      </View>
    );
  }

  const chartWidth = width - 64;
  const chartHeight = 140;
  const values = readings.map((r) => r.value);
  const minVal = Math.min(...values);
  const maxVal = Math.max(...values);
  const range = maxVal - minVal || 1;

  const points = readings.map((r, i) => ({
    x: (i / Math.max(readings.length - 1, 1)) * chartWidth,
    y: chartHeight - ((r.value - minVal) / range) * chartHeight,
  }));

  const pathD = points.reduce(
    (acc, p, i) => acc + (i === 0 ? `M${p.x},${p.y}` : ` L${p.x},${p.y}`),
    ''
  );

  return (
    <View style={{ height: chartHeight + 20, position: 'relative' }}>
      <View
        style={{
          position: 'absolute',
          left: 0,
          top: 0,
          width: chartWidth,
          height: chartHeight,
          overflow: 'hidden',
        }}
      >
        {/* SVG-less fallback: render as React Native View bars */}
        <View style={{ flex: 1, flexDirection: 'row', alignItems: 'flex-end' }}>
          {readings.map((r, i) => {
            const barH = ((r.value - minVal) / range) * chartHeight;
            return (
              <View
                key={i}
                style={{
                  flex: 1,
                  height: Math.max(barH, 2),
                  backgroundColor: Colors.primary,
                  marginHorizontal: 1,
                  borderTopLeftRadius: 2,
                  borderTopRightRadius: 2,
                  opacity: 0.8,
                }}
              />
            );
          })}
        </View>
      </View>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: chartHeight + 4 }}>
        <Text style={{ color: Colors.textSecondary, fontSize: 10 }}>
          {readings[0] ? formatDate(readings[0].date as string) : ''}
        </Text>
        <Text style={{ color: Colors.textSecondary, fontSize: 10 }}>
          {readings[readings.length - 1] ? formatDate(readings[readings.length - 1].date as string) : ''}
        </Text>
      </View>
    </View>
  );
}

export default function QuadrantDetailScreen() {
  const router = useRouter();
  const { pivotId, quadrant } = useLocalSearchParams<{ pivotId: string; quadrant: string }>();
  const quadranteNumber = QUADRANT_NAME_TO_NUMBER[quadrant] ?? 1;

  const [sensors, setSensors] = useState<Sensor[]>([]);
  const [selectedSensorId, setSelectedSensorId] = useState<number | null>(null);
  const [readings, setReadings] = useState<ReadEntry[]>([]);
  const [days, setDays] = useState(5);
  const [loading, setLoading] = useState(true);

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

        {/* Chart */}
        <Card>
          <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '700', marginBottom: 16 }}>
            Leituras do Sensor
          </Text>
          {loading ? (
            <ActivityIndicator color={Colors.primary} style={{ paddingVertical: 40 }} />
          ) : (
            <SimpleLineChart readings={readings} />
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
      </ScrollView>
    </View>
  );
}
