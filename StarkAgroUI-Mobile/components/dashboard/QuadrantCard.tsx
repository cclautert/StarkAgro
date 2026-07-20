import React from 'react';
import { View, Text, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Colors } from '../../constants/colors';

interface QuadrantCardProps {
  quadranteNumber: number;
  avg: number | null;
  humidityUpper: number;
  humidityLower: number;
  onPress: () => void;
}

interface Status {
  label: string;
  color: string;
}

function getStatus(avg: number | null, upper: number, lower: number): Status {
  if (avg === null) return { label: 'Sem dados', color: Colors.textSecondary };
  if (avg < lower) return { label: 'Umidade Baixa!', color: Colors.danger };
  if (avg > upper) return { label: 'Umidade Alta!', color: Colors.danger };
  if (avg < lower + 15) return { label: 'Normal', color: Colors.success };
  return { label: 'Ótimo', color: '#16A34A' };
}

export function QuadrantCard({ quadranteNumber, avg, humidityUpper, humidityLower, onPress }: QuadrantCardProps) {
  const status = getStatus(avg, humidityUpper, humidityLower);
  const isAlert = avg !== null && (avg < humidityLower || avg > humidityUpper);

  return (
    <TouchableOpacity
      onPress={onPress}
      style={{
        flex: 1,
        backgroundColor: Colors.card,
        borderRadius: 12,
        padding: 12,
        borderWidth: 1,
        borderColor: isAlert ? status.color : Colors.cardBorder,
        alignItems: 'center',
        minHeight: 110,
      }}
    >
      <Ionicons name="leaf" size={24} color={Colors.success} style={{ marginBottom: 4 }} />
      <Text style={{ color: Colors.textPrimary, fontSize: 12, fontWeight: '600', marginBottom: 2 }}>
        Q{quadranteNumber}
      </Text>
      <Text style={{ color: Colors.textPrimary, fontSize: 18, fontWeight: 'bold' }}>
        {avg !== null ? `${avg.toFixed(1)}` : '—'}
      </Text>
      <Text
        style={{ color: status.color, fontSize: 10, fontWeight: '600', textAlign: 'center', marginTop: 4 }}
        numberOfLines={2}
      >
        {isAlert ? `⚠ ${status.label}` : status.label}
      </Text>
    </TouchableOpacity>
  );
}
