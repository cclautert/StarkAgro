import { Ionicons } from '@expo/vector-icons';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { router } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Image,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Colors } from '../../../constants/colors';
import { networkMonitor } from '../../../services/offline/networkMonitor';
import { syncQueue } from '../../../services/offline/syncQueue';
import { diagnosisService } from '../../../services/diagnosisService';
import { pivotService } from '../../../services/pivotService';
import { Pivot } from '../../../types/api';

export default function NewDiagnosisScreen() {
  const [permission, requestPermission] = useCameraPermissions();
  const cameraRef = useRef<CameraView>(null);

  const [photoUri, setPhotoUri] = useState<string | null>(null);
  const [pivots, setPivots] = useState<Pivot[]>([]);
  const [pivotId, setPivotId] = useState<number | null>(null);
  const [cropName, setCropName] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    pivotService
      .getAll()
      .then(setPivots)
      .catch(() => {
        /* o pivô é opcional: sem a lista, o laudo ainda pode ser enviado */
      });
  }, []);

  async function takePhoto() {
    if (!cameraRef.current) return;

    const photo = await cameraRef.current.takePictureAsync({
      // Foto de celular vem com 2–8 MB. Reduzir aqui corta upload no 4G, custo e latência —
      // e é o que faz a foto caber no limite de 12 MB da API.
      quality: 0.7,
      imageType: 'jpg',
    });

    if (photo?.uri) setPhotoUri(photo.uri);
  }

  async function submit() {
    if (!photoUri || saving) return;

    setSaving(true);

    const payload = {
      localUri: photoUri,
      pivotId,
      cropName: cropName.trim() || null,
      notes: notes.trim() || null,
      capturedAt: new Date().toISOString(),
    };

    // Offline é a regra no talhão, não a exceção: a foto entra na fila e sobe sozinha quando
    // a conexão voltar. O produtor não fica esperando sinal com a planta doente na frente.
    if (!networkMonitor.getIsOnline()) {
      await syncQueue.enqueueDiagnosisPhoto(payload);
      setSaving(false);

      Alert.alert(
        'Foto guardada',
        'Você está sem conexão. A foto foi salva e será enviada automaticamente quando o sinal voltar.',
        [{ text: 'OK', onPress: () => router.replace('/(app)/diagnosis') }]
      );
      return;
    }

    try {
      await diagnosisService.upload(payload);
      router.replace('/(app)/diagnosis');
    } catch (error) {
      const status = (error as { response?: { status?: number } })?.response?.status;
      const message =
        (error as { response?: { data?: { errors?: string[] } } })?.response?.data?.errors?.[0];

      // 4xx é veredito do servidor (cota estourada, foto inválida): enfileirar não adianta.
      if (typeof status === 'number' && status >= 400 && status < 500) {
        setSaving(false);
        Alert.alert('Não foi possível enviar', message ?? 'A foto foi recusada pelo servidor.');
        return;
      }

      // Rede caiu no meio do upload: guarda na fila em vez de perder a foto.
      await syncQueue.enqueueDiagnosisPhoto(payload);
      setSaving(false);

      Alert.alert(
        'Foto guardada',
        'Não conseguimos enviar agora. A foto ficou na fila e sobe sozinha quando a conexão melhorar.',
        [{ text: 'OK', onPress: () => router.replace('/(app)/diagnosis') }]
      );
    }
  }

  if (!permission) {
    return (
      <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
        <ActivityIndicator color={Colors.primary} style={{ marginTop: 40 }} />
      </SafeAreaView>
    );
  }

  if (!permission.granted) {
    return (
      <SafeAreaView
        style={{ flex: 1, backgroundColor: Colors.background, justifyContent: 'center', padding: 24 }}
      >
        <Ionicons
          name="camera-outline"
          size={48}
          color={Colors.textSecondary}
          style={{ alignSelf: 'center', marginBottom: 16 }}
        />
        <Text style={{ color: Colors.textPrimary, fontSize: 16, textAlign: 'center', marginBottom: 8 }}>
          Precisamos da câmera
        </Text>
        <Text
          style={{ color: Colors.textSecondary, fontSize: 14, textAlign: 'center', marginBottom: 20 }}
        >
          É a câmera que fotografa a planta com sintoma para gerar o laudo.
        </Text>
        <TouchableOpacity
          onPress={requestPermission}
          style={{ backgroundColor: Colors.primary, borderRadius: 8, padding: 14 }}
        >
          <Text style={{ color: Colors.background, fontWeight: '700', textAlign: 'center' }}>
            Permitir câmera
          </Text>
        </TouchableOpacity>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: Colors.background }}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={{ flex: 1 }}
      >
        <ScrollView contentContainerStyle={{ padding: 16 }}>
          <Text style={{ color: Colors.textPrimary, fontSize: 20, fontWeight: '700', marginBottom: 12 }}>
            Nova foto
          </Text>

          {!photoUri ? (
            <View
              style={{
                height: 380,
                borderRadius: 12,
                overflow: 'hidden',
                borderWidth: 1,
                borderColor: Colors.cardBorder,
              }}
            >
              <CameraView ref={cameraRef} style={{ flex: 1 }} facing="back" />

              <TouchableOpacity
                onPress={takePhoto}
                style={{
                  position: 'absolute',
                  bottom: 16,
                  alignSelf: 'center',
                  width: 66,
                  height: 66,
                  borderRadius: 33,
                  backgroundColor: '#fff',
                  borderWidth: 4,
                  borderColor: Colors.primary,
                }}
              />
            </View>
          ) : (
            <View>
              <Image
                source={{ uri: photoUri }}
                style={{ width: '100%', height: 300, borderRadius: 12 }}
                resizeMode="cover"
              />
              <TouchableOpacity onPress={() => setPhotoUri(null)} style={{ padding: 10 }}>
                <Text style={{ color: Colors.primary, textAlign: 'center' }}>Tirar outra foto</Text>
              </TouchableOpacity>
            </View>
          )}

          {!photoUri && (
            <Text
              style={{
                color: Colors.textSecondary,
                fontSize: 13,
                textAlign: 'center',
                marginTop: 10,
                lineHeight: 19,
              }}
            >
              Aproxime a folha, foque na lesão e evite contraluz.
            </Text>
          )}

          <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 18, marginBottom: 6 }}>
            PIVÔ (OPCIONAL)
          </Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            <TouchableOpacity
              onPress={() => setPivotId(null)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 8,
                borderWidth: 1,
                borderColor: pivotId === null ? Colors.primary : Colors.cardBorder,
                backgroundColor: pivotId === null ? Colors.card : 'transparent',
              }}
            >
              <Text style={{ color: pivotId === null ? Colors.primary : Colors.textSecondary }}>
                Não informar
              </Text>
            </TouchableOpacity>

            {pivots.map((pivot) => (
              <TouchableOpacity
                key={pivot.id}
                onPress={() => setPivotId(pivot.id ?? null)}
                style={{
                  paddingHorizontal: 12,
                  paddingVertical: 8,
                  borderRadius: 8,
                  borderWidth: 1,
                  borderColor: pivotId === pivot.id ? Colors.primary : Colors.cardBorder,
                  backgroundColor: pivotId === pivot.id ? Colors.card : 'transparent',
                }}
              >
                <Text style={{ color: pivotId === pivot.id ? Colors.primary : Colors.textSecondary }}>
                  {pivot.name}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 18, marginBottom: 6 }}>
            CULTURA (OPCIONAL)
          </Text>
          <TextInput
            value={cropName}
            onChangeText={setCropName}
            placeholder="Ex.: soja, milho, café"
            placeholderTextColor={Colors.textSecondary}
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              borderRadius: 8,
              padding: 12,
              color: Colors.textPrimary,
            }}
          />

          <Text style={{ color: Colors.textSecondary, fontSize: 12, marginTop: 18, marginBottom: 6 }}>
            O QUE VOCÊ OBSERVOU (OPCIONAL)
          </Text>
          <TextInput
            value={notes}
            onChangeText={setNotes}
            placeholder="Ex.: manchas escuras nas folhas mais velhas, começou há 3 dias"
            placeholderTextColor={Colors.textSecondary}
            multiline
            numberOfLines={3}
            style={{
              backgroundColor: Colors.card,
              borderWidth: 1,
              borderColor: Colors.cardBorder,
              borderRadius: 8,
              padding: 12,
              color: Colors.textPrimary,
              textAlignVertical: 'top',
            }}
          />

          <TouchableOpacity
            onPress={submit}
            disabled={!photoUri || saving}
            style={{
              marginTop: 20,
              backgroundColor: !photoUri || saving ? Colors.cardBorder : Colors.success,
              borderRadius: 8,
              padding: 16,
            }}
          >
            {saving ? (
              <ActivityIndicator color={Colors.background} />
            ) : (
              <Text
                style={{
                  color: !photoUri ? Colors.textSecondary : '#fff',
                  fontWeight: '700',
                  textAlign: 'center',
                }}
              >
                Enviar para análise
              </Text>
            )}
          </TouchableOpacity>

          <Text
            style={{
              color: Colors.textSecondary,
              fontSize: 11,
              textAlign: 'center',
              marginTop: 12,
              marginBottom: 24,
            }}
          >
            A pré-análise é informativa e não constitui receituário agronômico nem ART.
          </Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
