import React, { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, ActivityIndicator } from 'react-native';
import { Colors } from '../../constants/colors';
import { Card } from '../ui/Card';
import { networkMonitor } from '../../services/offline/networkMonitor';
import { syncQueue } from '../../services/offline/syncQueue';
import { readsService } from '../../services/readsService';
import { ReadEntry, Sensor } from '../../types/api';

interface ManualReadFormProps {
  sensor: Sensor;
  pivotId: number;
  quadrante: number;
  onSaved: (entry: ReadEntry) => void;
}

export function ManualReadForm({ sensor, pivotId, quadrante, onSaved }: ManualReadFormProps) {
  const [value, setValue] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async () => {
    const parsed = Number(value.replace(',', '.'));
    if (Number.isNaN(parsed) || parsed < 0 || parsed > 100) {
      setError('Informe umidade entre 0 e 100');
      return;
    }

    setSaving(true);
    setError(null);
    const recordedAt = new Date().toISOString();

    try {
      if (networkMonitor.getIsOnline()) {
        try {
          const response = await readsService.createManualRead({
            code: sensor.code,
            value: parsed,
          });
          onSaved({
            id: response.id,
            sensorId: sensor.id,
            value: parsed,
            date: recordedAt,
            pendingSync: false,
          });
          setValue('');
          return;
        } catch {
          // Fall through to offline queue on transient network failure.
        }
      }

      const queued = await syncQueue.enqueueManualRead({
        sensorCode: sensor.code,
        sensorId: sensor.id,
        pivotId,
        quadrante,
        value: parsed,
        recordedAt,
      });

      onSaved({
        id: -Date.now(),
        sensorId: sensor.id,
        value: parsed,
        date: recordedAt,
        pendingSync: true,
        localQueueId: queued.id,
      });
      setValue('');
    } catch {
      setError('Não foi possível registrar a leitura');
    } finally {
      setSaving(false);
    }
  };

  const handlePhotoQueued = async () => {
    await syncQueue.enqueuePhoto({
      localUri: `file://offline-photo-${Date.now()}.jpg`,
      sensorId: sensor.id,
      pivotId,
      quadrante,
      capturedAt: new Date().toISOString(),
    });
  };

  return (
    <Card style={{ marginBottom: 16 }}>
      <Text style={{ color: Colors.textPrimary, fontSize: 16, fontWeight: '700', marginBottom: 12 }}>
        Leitura manual
      </Text>
      <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 8 }}>
        Sensor: {sensor.name ?? sensor.code}
      </Text>
      <TextInput
        value={value}
        onChangeText={setValue}
        keyboardType="decimal-pad"
        placeholder="Umidade (%)"
        placeholderTextColor={Colors.textSecondary}
        style={{
          backgroundColor: Colors.background,
          borderRadius: 8,
          borderWidth: 1,
          borderColor: Colors.cardBorder,
          color: Colors.textPrimary,
          paddingHorizontal: 12,
          paddingVertical: 10,
          marginBottom: 12,
        }}
      />
      {error && (
        <Text style={{ color: Colors.danger, fontSize: 12, marginBottom: 8 }}>{error}</Text>
      )}
      <View style={{ flexDirection: 'row', gap: 8 }}>
        <TouchableOpacity
          onPress={handlePhotoQueued}
          style={{
            paddingVertical: 12,
            paddingHorizontal: 14,
            borderRadius: 8,
            borderWidth: 1,
            borderColor: Colors.cardBorder,
          }}
        >
          <Text style={{ color: Colors.textSecondary, fontWeight: '600' }}>Foto offline</Text>
        </TouchableOpacity>
        <TouchableOpacity
          onPress={handleSave}
          disabled={saving}
          style={{
            flex: 1,
            paddingVertical: 12,
            borderRadius: 8,
            backgroundColor: Colors.primary,
            alignItems: 'center',
            opacity: saving ? 0.7 : 1,
          }}
        >
          {saving ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={{ color: '#fff', fontWeight: '700' }}>Registrar leitura</Text>
          )}
        </TouchableOpacity>
      </View>
    </Card>
  );
}
