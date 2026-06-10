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
import { computeDailyData } from '../../../../services/trendAnalysis';
import { TrendChart } from '../../../../components/dashboard/TrendChart';
import { TrendStatsPanel } from '../../../../components/dashboard/TrendStatsPanel';
import { ManualReadForm } from '../../../../components/dashboard/ManualReadForm';
import { PendingSyncBadge } from '../../../../components/ui/PendingSyncBadge';
import { useOfflineSync } from '../../../../hooks/useOfflineSync';
import { useNetworkStatus } from '../../../../hooks/useNetworkStatus';
import { mergePendingReads } from '../../../../services/offline/mergePendingReads';

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
  const isOnline = useNetworkStatus();

  const [sensors, setSensors] = useState<Sensor[]>([]);
  const [selectedSensorId, setSelectedSensorId] = useState<number | null>(null);
  const [serverReadings, setServerReadings] = useState<ReadEntry[]>([]);
  const [localReadings, setLocalReadings] = useState<ReadEntry[]>([]);
  const [days, setDays] = useState(7);
  const [loading, setLoading] = useState(true);

  const [showTrend, setShowTrend] = useState(true);
  const [showMovingAvg, setShowMovingAvg] = useState(true);
  const [showProjection, setShowProjection] = useState(true);

  const refreshReadings = useCallback(async () => {
    if (!selectedSensorId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const data = await readsService.getAllBySensorId(selectedSensorId, quadranteNumber, days);
      setServerReadings(data);
      setLocalReadings((prev) => prev.filter((entry) => entry.pendingSync));
    } catch {
      /* keep cached/local readings when offline */
    } finally {
      setLoading(false);
    }
  }, [selectedSensorId, quadranteNumber, days]);

  const { queueItems, pendingCount } = useOfflineSync(refreshReadings);

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

  useEffect(() => { loadSensors(); }, [loadSensors]);
  useEffect(() => { refreshReadings(); }, [refreshReadings]);

  useEffect(() => {
    if (!isOnline) return undefined;
    const interval = setInterval(refreshReadings, 60000);
    return () => clearInterval(interval);
  }, [refreshReadings, isOnline]);

  const readings = useMemo(() => {
    if (!selectedSensorId) return localReadings;
    const merged = mergePendingReads(serverReadings, queueItems, selectedSensorId, quadranteNumber);
    const localOnly = localReadings.filter(
      (entry) => !merged.some((item) => item.localQueueId && item.localQueueId === entry.localQueueId)
    );
    return [...localOnly, ...merged].sort(
      (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime()
    );
  }, [serverReadings, queueItems, selectedSensorId, quadranteNumber, localReadings]);

  const { points, projection, stats } = useMemo(
    () => computeDailyData(readings, humidityLower, humidityUpper),
    [readings, humidityLower, humidityUpper]
  );

  const selectedSensor = sensors.find((s) => s.id === selectedSensorId) ?? null;
  const qLabel = QUADRANT_LABELS[quadranteNumber] ?? quadrant;

  const handleManualSaved = (entry: ReadEntry) => {
    setLocalReadings((prev) => [entry, ...prev.filter((item) => item.localQueueId !== entry.localQueueId)]);
  };

  return (
    <View style={{ flex: 1, backgroundColor: Colors.background }}>
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
        {pendingCount > 0 && <PendingSyncBadge />}
      </View>

      <ScrollView contentContainerStyle={{ padding: 16 }}>
        {!isOnline && (
          <Card style={{ marginBottom: 16, borderColor: Colors.warning }}>
            <Text style={{ color: Colors.warning, fontWeight: '600' }}>
              Sem conexão — leituras serão sincronizadas ao reconectar
            </Text>
          </Card>
        )}

        {selectedSensor && pivotId && (
          <ManualReadForm
            sensor={selectedSensor}
            pivotId={parseInt(pivotId)}
            quadrante={quadranteNumber}
            onSaved={handleManualSaved}
          />
        )}

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

        <View style={{ flexDirection: 'row', gap: 6, marginBottom: 16, flexWrap: 'wrap' }}>
          <OverlayToggle label="Tendência" color={Colors.trendLine} active={showTrend} onPress={() => setShowTrend((v) => !v)} />
          <OverlayToggle label="Méd. Móvel" color={Colors.movingAvg} active={showMovingAvg} onPress={() => setShowMovingAvg((v) => !v)} />
          <OverlayToggle label="Projeção" color={Colors.projection} active={showProjection} onPress={() => setShowProjection((v) => !v)} />
        </View>

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

        {readings.length > 0 && (
          <Card style={{ marginBottom: 16 }}>
            <Text style={{ color: Colors.textPrimary, fontSize: 15, fontWeight: '700', marginBottom: 12 }}>
              Histórico recente
            </Text>
            {readings.slice(0, 5).map((entry) => (
              <View
                key={`${entry.id}-${entry.date}`}
                style={{
                  flexDirection: 'row',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  paddingVertical: 8,
                  borderBottomWidth: 1,
                  borderBottomColor: Colors.cardBorder,
                }}
              >
                <View>
                  <Text style={{ color: Colors.textPrimary, fontWeight: '600' }}>
                    {entry.value.toFixed(1)}%
                  </Text>
                  <Text style={{ color: Colors.textSecondary, fontSize: 12 }}>
                    {new Date(entry.date).toLocaleString('pt-BR')}
                  </Text>
                </View>
                {entry.pendingSync && <PendingSyncBadge compact />}
              </View>
            ))}
          </Card>
        )}

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
