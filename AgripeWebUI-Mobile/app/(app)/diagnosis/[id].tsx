import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Colors } from '../../../constants/colors';
import { PlantDiagnosis, diagnosisService } from '../../../services/diagnosisService';

/** Enquanto o laudo está sendo analisado, o produtor fica olhando a tela. */
const POLL_MS = 3000;

export default function DiagnosisDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const diagnosisId = Number(id);

  const [diagnosis, setDiagnosis] = useState<PlantDiagnosis | null>(null);
  const [loading, setLoading] = useState(true);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const load = useCallback(async () => {
    try {
      setDiagnosis(await diagnosisService.getById(diagnosisId));
    } catch {
      /* offline: mantém o que já está na tela */
    } finally {
      setLoading(false);
    }
  }, [diagnosisId]);

  useEffect(() => {
    load();

    timer.current = setInterval(() => {
      setDiagnosis((current) => {
        if (current && (current.status === 'Uploaded' || current.status === 'Processing')) {
          load();
        }
        return current;
      });
    }, POLL_MS);

    return () => {
      if (timer.current) clearInterval(timer.current);
    };
  }, [load]);

  if (loading) {
    return (
      <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
        <ActivityIndicator color={Colors.primary} style={{ marginTop: 40 }} />
      </SafeAreaView>
    );
  }

  if (!diagnosis) {
    return (
      <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background, padding: 16 }}>
        <Text style={{ color: Colors.textSecondary }}>Laudo não encontrado.</Text>
      </SafeAreaView>
    );
  }

  const isPending = diagnosis.status === 'Uploaded' || diagnosis.status === 'Processing';
  const report = diagnosis.agronomistReportMarkdown ?? diagnosis.aiReportMarkdown;

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: 32 }}>
        <Text style={{ color: Colors.textPrimary, fontSize: 20, fontWeight: '700' }}>
          Laudo #{diagnosis.id}
        </Text>
        <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 2, marginBottom: 14 }}>
          {new Date(diagnosis.createdAt).toLocaleString('pt-BR')}
        </Text>

        {/* Assinado */}
        {diagnosis.signature && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.success,
              borderRadius: 10,
              padding: 12,
              marginBottom: 14,
              flexDirection: 'row',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <Ionicons name="checkmark-circle" size={20} color={Colors.success} />
            <Text style={{ color: Colors.success, flex: 1, fontSize: 13 }}>
              Assinado por {diagnosis.signature.agronomistName}
              {diagnosis.signature.crea ? ` — ${diagnosis.signature.crea}` : ''}
            </Text>
          </View>
        )}

        {/* Processando */}
        {isPending && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.warning,
              borderRadius: 10,
              padding: 12,
              marginBottom: 14,
              flexDirection: 'row',
              alignItems: 'center',
              gap: 10,
            }}
          >
            <ActivityIndicator size="small" color={Colors.warning} />
            <Text style={{ color: Colors.warning, flex: 1, fontSize: 13 }}>
              Analisando a foto e cruzando com os dados do seu pivô.
            </Text>
          </View>
        )}

        {/* Recusado / falhou */}
        {(diagnosis.status === 'Rejected' || diagnosis.status === 'Failed') && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.danger,
              borderRadius: 10,
              padding: 12,
              marginBottom: 14,
            }}
          >
            <Text style={{ color: Colors.danger, fontSize: 13 }}>
              {diagnosis.failureReason ??
                diagnosis.rejectionReason ??
                'Não foi possível analisar esta foto.'}
            </Text>
          </View>
        )}

        {/* Diagnóstico */}
        {diagnosis.diseases.length > 0 && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              borderRadius: 10,
              padding: 14,
              marginBottom: 14,
            }}
          >
            <Text style={{ color: Colors.textPrimary, fontWeight: '700', marginBottom: 10 }}>
              Diagnóstico provável
            </Text>

            {diagnosis.diseases.slice(0, 3).map((disease, index) => (
              <View key={`${disease.name}-${index}`} style={{ marginBottom: 8 }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                  <Text style={{ color: Colors.textPrimary, fontSize: 14, flex: 1 }}>
                    {disease.name}
                  </Text>
                  <Text
                    style={{
                      color: index === 0 ? Colors.success : Colors.textSecondary,
                      fontWeight: '700',
                    }}
                  >
                    {Math.round(disease.probability * 100)}%
                  </Text>
                </View>

                <View
                  style={{
                    height: 6,
                    backgroundColor: Colors.background,
                    borderRadius: 3,
                    marginTop: 4,
                  }}
                >
                  <View
                    style={{
                      height: 6,
                      width: `${Math.round(disease.probability * 100)}%`,
                      backgroundColor: index === 0 ? Colors.success : Colors.cardBorder,
                      borderRadius: 3,
                    }}
                  />
                </View>
              </View>
            ))}
          </View>
        )}

        {/* Contexto da lavoura — o diferencial */}
        {diagnosis.context && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              borderRadius: 10,
              padding: 14,
              marginBottom: 14,
            }}
          >
            <Text style={{ color: Colors.textPrimary, fontWeight: '700', marginBottom: 8 }}>
              Contexto da sua lavoura
            </Text>

            {diagnosis.context.moistureAvg7d != null && (
              <Text style={{ color: Colors.textSecondary, fontSize: 13, marginBottom: 3 }}>
                Umidade média (7 dias): {diagnosis.context.moistureAvg7d}%
              </Text>
            )}

            {diagnosis.context.daysAboveUpperLimit > 0 && (
              <Text style={{ color: Colors.warning, fontSize: 13, marginBottom: 3 }}>
                {diagnosis.context.daysAboveUpperLimit} dia(s) acima do limite superior
              </Text>
            )}

            {!!diagnosis.context.forecastSummary && (
              <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 4 }}>
                {diagnosis.context.forecastSummary}
              </Text>
            )}
          </View>
        )}

        {/* Laudo */}
        {!!report && (
          <View
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              borderRadius: 10,
              padding: 14,
            }}
          >
            <Text style={{ color: Colors.textPrimary, fontWeight: '700', marginBottom: 8 }}>Laudo</Text>
            <Text style={{ color: Colors.textSecondary, fontSize: 13, lineHeight: 20 }}>
              {stripMarkdown(report)}
            </Text>

            {!!diagnosis.prescription && (
              <View
                style={{
                  marginTop: 12,
                  padding: 10,
                  borderRadius: 8,
                  borderWidth: 1,
                  borderColor: Colors.warning,
                }}
              >
                <Text style={{ color: Colors.warning, fontSize: 11, fontWeight: '700' }}>
                  PRESCRIÇÃO DO AGRÔNOMO
                </Text>
                <Text style={{ color: Colors.textPrimary, fontSize: 13, marginTop: 4 }}>
                  {diagnosis.prescription}
                </Text>
              </View>
            )}
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

/** O laudo vem em markdown; aqui só precisamos do texto legível. */
function stripMarkdown(text: string): string {
  return text
    .replace(/^#{1,6}\s+/gm, '')
    .replace(/\*\*(.+?)\*\*/g, '$1')
    .replace(/\*(.+?)\*/g, '$1')
    .replace(/^[-*]\s+/gm, '• ')
    .replace(/_{1,2}(.+?)_{1,2}/g, '$1')
    .trim();
}
