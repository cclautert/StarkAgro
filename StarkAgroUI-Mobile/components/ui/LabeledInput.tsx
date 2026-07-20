import React from 'react';
import { View, Text, TextInput, TextInputProps } from 'react-native';
import { Colors } from '../../constants/colors';

interface LabeledInputProps extends TextInputProps {
  label: string;
}

export function LabeledInput({ label, ...props }: LabeledInputProps) {
  return (
    <View style={{ marginBottom: 16 }}>
      <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 6 }}>{label}</Text>
      <TextInput
        style={{
          backgroundColor: Colors.background,
          color: Colors.textPrimary,
          borderWidth: 1,
          borderColor: Colors.cardBorder,
          borderRadius: 8,
          paddingHorizontal: 12,
          paddingVertical: 12,
          fontSize: 15,
        }}
        placeholderTextColor={Colors.textSecondary}
        {...props}
      />
    </View>
  );
}
