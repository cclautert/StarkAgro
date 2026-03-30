import React from 'react';
import { View, ActivityIndicator, Text } from 'react-native';
import { Colors } from '../../constants/colors';

interface LoadingSpinnerProps {
  message?: string;
}

export function LoadingSpinner({ message }: LoadingSpinnerProps) {
  return (
    <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: Colors.background }}>
      <ActivityIndicator size="large" color={Colors.primary} />
      {message && (
        <Text style={{ color: Colors.textSecondary, marginTop: 12, fontSize: 14 }}>{message}</Text>
      )}
    </View>
  );
}
