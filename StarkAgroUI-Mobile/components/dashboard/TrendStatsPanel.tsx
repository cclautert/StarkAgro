import React from 'react';
import { View, Text } from 'react-native';
import { TrendStats } from '../../services/trendAnalysis';
import { Colors } from '../../constants/colors';

interface TrendStatsPanelProps {
  stats: TrendStats;
}

export function TrendStatsPanel({ stats }: TrendStatsPanelProps) {
  const { slope, avg, min, max, last, proj5, alertCount, compliancePct, variability } = stats;

  const trendDirection =
    slope > 0.3 ? 'Subindo' : slope < -0.3 ? 'Caindo' : 'Estável';
  const trendIcon = slope > 0.3 ? '↑' : slope < -0.3 ? '↓' : '→';
  const trendColor =
    slope > 0.3 ? Colors.danger : slope < -0.3 ? Colors.warning : Colors.success;

  return (
    <View>
      {/* Trend direction badge */}
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          backgroundColor: Colors.background,
          borderRadius: 8,
          padding: 10,
          marginBottom: 12,
          gap: 8,
        }}
      >
        <Text style={{ fontSize: 20, color: trendColor }}>{trendIcon}</Text>
        <View>
          <Text style={{ color: Colors.textSecondary, fontSize: 11 }}>Tendência</Text>
          <Text style={{ color: trendColor, fontWeight: '700', fontSize: 14 }}>
            {trendDirection}
          </Text>
        </View>
        <View style={{ marginLeft: 'auto' }}>
          <Text style={{ color: Colors.textSecondary, fontSize: 11 }}>Inclinação</Text>
          <Text style={{ color: Colors.textPrimary, fontWeight: '600', fontSize: 13 }}>
            {slope >= 0 ? '+' : ''}{slope.toFixed(2)} %/dia
          </Text>
        </View>
      </View>

      {/* Metrics grid */}
      <View style={{ flexDirection: 'row', gap: 8, marginBottom: 8 }}>
        <StatCard label="Última Leitura" value={`${last.toFixed(1)}%`} />
        <StatCard label="Média do Período" value={`${avg.toFixed(1)}%`} />
      </View>
      <View style={{ flexDirection: 'row', gap: 8, marginBottom: 8 }}>
        <StatCard label="Mínimo" value={`${min.toFixed(1)}%`} valueColor={Colors.limitLower} />
        <StatCard label="Máximo" value={`${max.toFixed(1)}%`} valueColor={Colors.limitUpper} />
      </View>
      <View style={{ flexDirection: 'row', gap: 8, marginBottom: 8 }}>
        <StatCard
          label="Projeção 5 dias"
          value={`${proj5.toFixed(1)}%`}
          valueColor={Colors.projection}
        />
        <StatCard label="Variabilidade" value={`${variability.toFixed(1)}%`} />
      </View>
      <View style={{ flexDirection: 'row', gap: 8 }}>
        <StatCard
          label="Conformidade"
          value={`${compliancePct}%`}
          valueColor={compliancePct >= 80 ? Colors.success : Colors.danger}
        />
        <StatCard
          label="Alertas"
          value={String(alertCount)}
          valueColor={alertCount > 0 ? Colors.danger : Colors.success}
        />
      </View>
    </View>
  );
}

function StatCard({
  label,
  value,
  valueColor,
}: {
  label: string;
  value: string;
  valueColor?: string;
}) {
  return (
    <View
      style={{
        flex: 1,
        backgroundColor: Colors.background,
        borderRadius: 8,
        padding: 10,
      }}
    >
      <Text style={{ color: Colors.textSecondary, fontSize: 11, marginBottom: 2 }}>{label}</Text>
      <Text
        style={{
          color: valueColor ?? Colors.textPrimary,
          fontWeight: '700',
          fontSize: 15,
        }}
      >
        {value}
      </Text>
    </View>
  );
}
