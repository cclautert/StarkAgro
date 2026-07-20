import React from 'react';
import { View, Text } from 'react-native';
import { Colors } from '../../constants/colors';

interface PendingSyncBadgeProps {
  compact?: boolean;
}

export function PendingSyncBadge({ compact }: PendingSyncBadgeProps) {
  return (
    <View
      style={{
        backgroundColor: Colors.warning + '33',
        borderColor: Colors.warning,
        borderWidth: 1,
        borderRadius: compact ? 10 : 12,
        paddingHorizontal: compact ? 6 : 8,
        paddingVertical: compact ? 2 : 4,
      }}
    >
      <Text style={{ color: Colors.warning, fontSize: compact ? 10 : 11, fontWeight: '700' }}>
        pendente sync
      </Text>
    </View>
  );
}
