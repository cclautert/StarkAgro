import React, { useState, useEffect, useCallback } from 'react';
import { View, Text, TouchableOpacity, ActivityIndicator, ScrollView } from 'react-native';
import { useRouter } from 'expo-router';
import Svg, { G, Path, Text as SvgText } from 'react-native-svg';
import { Picker } from '@react-native-picker/picker';
import { usePivots } from '../../hooks/usePivots';
import { readsService } from '../../services/readsService';
import { GetReadByPivotIdResponse } from '../../types/api';
import { Colors } from '../../constants/colors';
import { Card } from '../ui/Card';
import { LoadingSpinner } from '../ui/LoadingSpinner';

const QUADRANT_POSITIONS = [
  { key: 'TopLeft',    label: 'Q4', cx: 25, cy: 25, d: 'M 50,50 L 0,50 A 50,50 0 0,1 50,0 Z' },
  { key: 'TopRight',   label: 'Q1', cx: 75, cy: 25, d: 'M 50,50 L 50,0 A 50,50 0 0,1 100,50 Z' },
  { key: 'BottomRight',label: 'Q2', cx: 75, cy: 75, d: 'M 50,50 L 100,50 A 50,50 0 0,1 50,100 Z' },
  { key: 'BottomLeft', label: 'Q3', cx: 25, cy: 75, d: 'M 50,50 L 50,100 A 50,50 0 0,1 0,50 Z' },
];

function getAvg(pivot: GetReadByPivotIdResponse | null, key: string): string {
  if (!pivot?.quadrante) return '—';
  const q = pivot.quadrante as unknown as Record<string, number | string | undefined>;
  const avg = q[key + 'Avg'] as number | undefined;
  return avg != null ? avg.toFixed(1) : '—';
}

function getColor(pivot: GetReadByPivotIdResponse | null, key: string): string {
  if (!pivot?.quadrante) return '#607D8B';
  const q = pivot.quadrante as unknown as Record<string, string | undefined>;
  return q[key] ?? '#607D8B';
}

export function OldDashboard() {
  const router = useRouter();
  const { pivots, loading: pivotsLoading } = usePivots();
  const [selectedPivotId, setSelectedPivotId] = useState<number | null>(null);
  const [pivotData, setPivotData] = useState<GetReadByPivotIdResponse | null>(null);
  const [loading, setLoading] = useState(false);

  const loadData = useCallback(async (id: number) => {
    setLoading(true);
    try {
      const data = await readsService.getByPivotId(id, 7);
      setPivotData(data);
    } catch {
      /* ignore */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (pivots.length > 0 && !selectedPivotId) {
      setSelectedPivotId(pivots[0].id);
    }
  }, [pivots]);

  useEffect(() => {
    if (!selectedPivotId) return;
    loadData(selectedPivotId);
    const interval = setInterval(() => loadData(selectedPivotId), 60000);
    return () => clearInterval(interval);
  }, [selectedPivotId, loadData]);

  if (pivotsLoading) return <LoadingSpinner message="Carregando pivôs..." />;

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: Colors.background }}
      contentContainerStyle={{ padding: 16 }}
    >
      <Text style={{ color: Colors.textPrimary, fontSize: 22, fontWeight: 'bold', marginBottom: 16 }}>
        Monitoramento
      </Text>

      {/* Pivot selector */}
      <Card style={{ marginBottom: 20 }}>
        <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 4 }}>Selecione o Pivô</Text>
        <View style={{ borderRadius: 8, overflow: 'hidden', backgroundColor: Colors.background }}>
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

      {/* SVG Circle */}
      <Card style={{ alignItems: 'center', marginBottom: 16 }}>
        {loading ? (
          <ActivityIndicator color={Colors.primary} style={{ padding: 60 }} />
        ) : (
          <Svg viewBox="0 0 100 100" width={280} height={280}>
            {QUADRANT_POSITIONS.map((q) => (
              <G
                key={q.key}
                onPress={() =>
                  router.push(`/(app)/dashboard/${selectedPivotId}/${q.key}`)
                }
              >
                <Path
                  d={q.d}
                  fill={getColor(pivotData, q.key)}
                  stroke={Colors.card}
                  strokeWidth={1}
                />
                <SvgText
                  x={q.cx}
                  y={q.cy - 4}
                  textAnchor="middle"
                  fontSize={7}
                  fill="#fff"
                  fontWeight="bold"
                >
                  {q.label}
                </SvgText>
                <SvgText
                  x={q.cx}
                  y={q.cy + 7}
                  textAnchor="middle"
                  fontSize={6}
                  fill="#fff"
                >
                  {getAvg(pivotData, q.key)}
                </SvgText>
              </G>
            ))}
          </Svg>
        )}
      </Card>

      <Text style={{ color: Colors.textSecondary, fontSize: 12, textAlign: 'center' }}>
        Toque em um quadrante para ver o gráfico
      </Text>
    </ScrollView>
  );
}
