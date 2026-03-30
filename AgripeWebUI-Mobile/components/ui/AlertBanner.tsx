import React from 'react';
import { View, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

interface AlertBannerProps {
  message: string;
  type?: 'low' | 'high';
}

export function AlertBanner({ message, type = 'low' }: AlertBannerProps) {
  const bg = type === 'low' ? '#7F1D1D' : '#7C2D12';
  const border = type === 'low' ? '#EF4444' : '#F97316';
  const icon = type === 'low' ? 'arrow-down-circle' : 'arrow-up-circle';

  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: bg,
        borderLeftWidth: 4,
        borderLeftColor: border,
        borderRadius: 8,
        padding: 12,
        marginVertical: 4,
      }}
    >
      <Ionicons name={icon} size={20} color={border} />
      <Text style={{ color: '#fff', fontSize: 13, fontWeight: '600', marginLeft: 8, flex: 1 }}>
        {message}
      </Text>
    </View>
  );
}
