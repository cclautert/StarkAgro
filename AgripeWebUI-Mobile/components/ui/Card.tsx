import React from 'react';
import { View, ViewStyle } from 'react-native';
import { Colors } from '../../constants/colors';

interface CardProps {
  children: React.ReactNode;
  style?: ViewStyle;
}

export function Card({ children, style }: CardProps) {
  return (
    <View
      style={[
        {
          backgroundColor: Colors.card,
          borderRadius: 12,
          padding: 16,
          borderWidth: 1,
          borderColor: Colors.cardBorder,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}
