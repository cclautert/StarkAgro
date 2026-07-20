import { Ionicons } from '@expo/vector-icons';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Colors } from '../../../constants/colors';
import { useOfflineSync } from '../../../hooks/useOfflineSync';
import {
  DiagnosisQuota,
  PlantDiagnosisStatus,
  PlantDiagnosisSummary,
  diagnosisService,
} from '../../../services/diagnosisService';

const STATUS_LABEL: Record<PlantDiagnosisStatus, string> = {
  Uploaded: 'Na fila',
  Processing: 'Analisando',
  PendingReview: 'Com o agrônomo',
  InReview: 'Em revisão',
  AiCompleted: 'Pré-análise pronta',
  Signed: 'Assinado',
  Rejected: 'Foto rejeitada',
  Failed: 'Falhou',
};

function statusColor(status: PlantDiagnosisStatus): string {
  if (status === 'Signed' || status === 'AiCompleted') return Colors.success;
  if (status === 'Rejected' || status === 'Failed') return Colors.danger;
  return Colors.warning;
}

export default function DiagnosisListScreen() {
  const [items, setItems] = useState<PlantDiagnosisSummary[]>([]);
  const [quota, setQuota] = useState<DiagnosisQuota | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const { pendingCount, isSyncing } = useOfflineSync(() => load());

  const load = useCallback(async () => {
    try {
      const [list, q] = await Promise.all([
        diagnosisService.getAll(),
        diagnosisService.getQuota().catch(() => null),
      ]);
      setItems(list);
      setQuota(q);
    } catch {
      /* offline: a tela mostra o que já tem e a fila continua funcionando */
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: 16,
        }}
      >
        <Text style={{ color: Colors.textPrimary, fontSize: 20, fontWeight: '700' }}>Laudos</Text>

        <TouchableOpacity
          onPress={() => router.push('/(app)/diagnosis/new')}
          disabled={quota?.isExhausted}
          style={{
            backgroundColor: quota?.isExhausted ? Colors.cardBorder : Colors.primary,
            borderRadius: 8,
            paddingHorizontal: 12,
            paddingVertical: 8,
            flexDirection: 'row',
            alignItems: 'center',
            gap: 6,
          }}
        >
          <Ionicons
            name="camera"
            size={18}
            color={quota?.isExhausted ? Colors.textSecondary : Colors.background}
          />
          <Text
            style={{
              color: quota?.isExhausted ? Colors.textSecondary : Colors.background,
              fontWeight: '700',
            }}
          >
            Nova foto
          </Text>
        </TouchableOpacity>
      </View>

      {/* Fotos aguardando sinal — o caso de uso central no campo */}
      {pendingCount > 0 && (
        <View
          style={{
            marginHorizontal: 16,
            marginBottom: 10,
            padding: 12,
            borderRadius: 8,
            backgroundColor: Colors.card,
            borderWidth: 1,
            borderColor: Colors.warning,
            flexDirection: 'row',
            alignItems: 'center',
            gap: 8,
          }}
        >
          {isSyncing ? (
            <ActivityIndicator size="small" color={Colors.warning} />
          ) : (
            <Ionicons name="cloud-offline-outline" size={18} color={Colors.warning} />
          )}
          <Text style={{ color: Colors.warning, flex: 1, fontSize: 13 }}>
            {isSyncing
              ? 'Enviando o que ficou na fila...'
              : `${pendingCount} item(ns) aguardando conexão. Sobem sozinhos quando o sinal voltar.`}
          </Text>
        </View>
      )}

      {/* Cota do plano */}
      {quota && !quota.isUnlimited && (
        <View
          style={{
            marginHorizontal: 16,
            marginBottom: 10,
            padding: 12,
            borderRadius: 8,
            backgroundColor: Colors.card,
            borderWidth: 1,
            borderColor: quota.isExhausted ? Colors.warning : Colors.cardBorder,
          }}
        >
          <Text style={{ color: quota.isExhausted ? Colors.warning : Colors.textSecondary, fontSize: 13 }}>
            {quota.isExhausted
              ? `Você usou os ${quota.limit} laudos do seu plano neste mês.`
              : `${quota.used} de ${quota.limit} laudos usados neste mês · restam ${quota.remaining}`}
          </Text>
        </View>
      )}

      {loading ? (
        <ActivityIndicator color={Colors.primary} style={{ marginTop: 40 }} />
      ) : (
        <FlatList
          data={items}
          keyExtractor={(item) => String(item.id)}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 24 }}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={() => {
                setRefreshing(true);
                load();
              }}
              tintColor={Colors.primary}
            />
          }
          ListEmptyComponent={
            <Text
              style={{
                color: Colors.textSecondary,
                textAlign: 'center',
                marginTop: 40,
                lineHeight: 20,
              }}
            >
              Nenhum laudo ainda.{'\n'}Fotografe uma planta com sintoma para começar.
            </Text>
          }
          renderItem={({ item }) => (
            <TouchableOpacity
              onPress={() => router.push(`/(app)/diagnosis/${item.id}`)}
              style={{
                backgroundColor: Colors.card,
                borderWidth: 1,
                borderColor: Colors.cardBorder,
                borderRadius: 10,
                padding: 14,
                marginBottom: 10,
              }}
            >
              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <Text style={{ color: Colors.textPrimary, fontWeight: '600', fontSize: 15 }}>
                  {item.cropName || 'Cultura não informada'}
                </Text>
                <Text style={{ color: statusColor(item.status), fontSize: 12, fontWeight: '700' }}>
                  {STATUS_LABEL[item.status] ?? item.status}
                </Text>
              </View>

              <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 4 }}>
                {new Date(item.createdAt).toLocaleString('pt-BR')}
              </Text>

              {!!item.failureReason && (
                <Text style={{ color: Colors.danger, fontSize: 12, marginTop: 4 }}>
                  {item.failureReason}
                </Text>
              )}
            </TouchableOpacity>
          )}
        />
      )}
    </SafeAreaView>
  );
}
