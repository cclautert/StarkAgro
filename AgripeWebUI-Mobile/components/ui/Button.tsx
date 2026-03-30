import React from 'react';
import { TouchableOpacity, Text, ActivityIndicator, ViewStyle } from 'react-native';
import { Colors } from '../../constants/colors';

interface ButtonProps {
  title: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
  loading?: boolean;
  disabled?: boolean;
  style?: ViewStyle;
}

export function Button({ title, onPress, variant = 'primary', loading, disabled, style }: ButtonProps) {
  const bg =
    variant === 'primary' ? Colors.primary :
    variant === 'danger' ? Colors.danger :
    Colors.cardBorder;

  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={loading || disabled}
      style={[
        {
          backgroundColor: bg,
          borderRadius: 8,
          paddingVertical: 12,
          paddingHorizontal: 20,
          alignItems: 'center',
          opacity: disabled ? 0.5 : 1,
        },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color="#fff" size="small" />
      ) : (
        <Text style={{ color: '#fff', fontWeight: '700', fontSize: 15 }}>{title}</Text>
      )}
    </TouchableOpacity>
  );
}
