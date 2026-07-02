# Downlink LoRaWAN — configuração do gateway Dragino

Fluxo: UI "Sincronizar" → `POST v1/sensor/sync-downlink` → `MqttDownlinkService`
publica no tópico **`writes`** do broker da VPS (QoS 1) → forwarder-mqtt-downlink
do gateway (inscrito em `writes`) → fila do device no ChirpStack local → comando
desce ao sensor (Classe A) na **próxima transmissão** dele.

Payload publicado (formato ChirpStack enqueue):

```json
{ "devEUI": "a84041691d5f1794", "fPort": 1, "confirmed": false, "data": "AQAqMA==" }
```

`data` = Base64 de `0x01` + intervalo em segundos (24 bits, big-endian) — comando
de intervalo de uplink do Khomp DTL-300 (porta 1).

## Configuração do forwarder-mqtt-downlink (Dragino)

Em `http://<ip-do-gateway>/cgi-bin/forwarder-mqtt-downlink.has`, usar exatamente a
mesma conexão do forwarder de uplink que já funciona:

| Campo | Valor |
|-------|-------|
| Broker/Host | `agripeweb.com` |
| Porta | `8883` |
| **TLS/SSL** | **Habilitado** (obrigatório — a porta 8883 é TLS-only; sem TLS o broker registra `SSL routines::wrong version number` e derruba a conexão) |
| Certificado | Mesma opção do uplink (CA ou sem verificação) |
| Usuário/Senha | `MQTT_USERNAME` / `MQTT_PASSWORD` (mesmos do uplink) |
| Tópico (subscribe) | `writes` |

## Troubleshooting

| Sintoma | Causa provável |
|---------|----------------|
| Broker loga `SSL routines::wrong version number` a cada poucos segundos | Forwarder de downlink com TLS desabilitado apontando para 8883 |
| "Downlink enfileirado com sucesso" no UI mas nada na fila do ChirpStack | Forwarder não inscrito em `writes` no momento do clique — QoS 1 ajuda, mas sem sessão persistente no cliente a mensagem publicada offline ainda se perde; corrigir a conexão e clicar Sincronizar de novo |
| Downlink na fila do ChirpStack mas sensor não aplica | Normal até a próxima transmissão (Classe A só recebe após um uplink); conferir FPort=1 |
| ACL habilitado e downlink parou | `docker/mosquitto/acl` precisa de `topic read writes` (gateway) e `topic write writes` (API) — ver `acl.example` |

Verificação no broker (VPS):

```bash
docker logs agripeweb-mqtt --since 10m          # conexões/TLS
docker exec agripeweb-mqtt mosquitto_sub -p 1883 -u "$MQTT_USERNAME" -P "$MQTT_PASSWORD" -t writes -C 1 -W 120
# clicar "Sincronizar" no UI e ver o JSON aparecer
```
