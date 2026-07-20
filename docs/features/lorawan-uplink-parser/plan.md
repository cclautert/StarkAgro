Issue: https://github.com/cclautert/StarkAgro/issues/70

# Plan — Parser Uplink LoRaWAN ChirpStack

## Context

O `MqttWorkerService` atualmente só entende o payload legado `{ "code": "...", "value": ... }`. O ChirpStack publica uplinkss LoRaWAN no mesmo tópico MQTT com formato `{ "DevEUI": "...", "data": { "BatV", "TempC_SHT", "Hum_SHT" }, "time": "...", "fcnt": N }`. Este issue cria um `LoRaWanUplinkParser` e atualiza o worker para detectar o formato e emitir até 3 `CreateDeviceReadRequest` por uplink, com filtro de anomalia apenas para `_H`.

## Acceptance Criteria → Implementation Decisions

| Criterion | Decision |
|-----------|----------|
| Payload de exemplo gera 3 leituras com valores corretos e mesmo timestamp | Parser mapeia `Hum_SHT→_H`, `TempC_SHT→_T`, `BatV→_B`; `ReadAt = uplink.Time` |
| `TempC1: "NULL"` e métricas inválidas são ignoradas | `DecimalNullableConverter` converte string e JSON null → `null`; apenas valores não-nulos geram request |
| Payload legado `{code, value}` continua funcionando | Detecção por presença de campo `DevEUI` (case-insensitive); legado usa caminho existente |
| Sensor inexistente loga erro sem derrubar o worker | Comportamento herdado do `CreateDeviceReadHandler` (#69); worker continua para próxima leitura |
| Anomaly detection só dispara para `_H` | Guard: `read.Code.EndsWith("_H", StringComparison.OrdinalIgnoreCase)` antes de `DetectSensorAnomalyRequest` |

## Affected Layers

- **Novo serviço:** `StarkAgroWorker/Services/LoRaWanUplinkParser.cs` (interface + implementação + DTOs + converter)
- **Modificado:** `StarkAgroWorker/Services/MqttWorkerService.cs` — detecção de formato, caminho LoRaWAN, filtro anomalia `_H`
- **Modificado:** `StarkAgroWorker/Program.cs` — registrar `ILoRaWanUplinkParser`

## New REST Endpoints

Nenhum — ingestão MQTT-only.

## Files to Create

| Path | Type |
|------|------|
| `StarkAgroWorker/Services/LoRaWanUplinkParser.cs` | Interface + implementação + DTOs internos + `DecimalNullableConverter` |
| `StarkAgroWorker.Tests/Services/LoRaWanUplinkParserTests.cs` | xUnit tests |

## Files to Modify

| Path | Change |
|------|--------|
| `StarkAgroWorker/Services/MqttWorkerService.cs` | Injetar `ILoRaWanUplinkParser`; adicionar detecção de formato; emitir múltiplos requests para LoRaWAN; filtro anomalia `_H`-only |
| `StarkAgroWorker/Program.cs` | `services.AddSingleton<ILoRaWanUplinkParser, LoRaWanUplinkParser>()` |

## MongoDB Changes

None.

## Tenant Isolation Plan

Herdado de `CreateDeviceReadHandler` (#69): `UserId` sempre de `sensor.UserId`. O parser não acessa MongoDB.

## Parser Design

### Interface
```
ILoRaWanUplinkParser.Parse(string json) → IReadOnlyList<CreateDeviceReadRequest>
```

### Detecção no MqttWorkerService
```
JsonDocument.Parse(payload) → verificar se elemento raiz tem propriedade "DevEUI" (EnumerateObject case-insensitive)
Se sim → caminho LoRaWAN
Se não → caminho legado (existente)
```

### Mapeamento de campos
| Campo JSON | Código do sensor | Sufixo |
|------------|-----------------|--------|
| `Hum_SHT` | `{DevEUI}_H` | `_H` |
| `TempC_SHT` | `{DevEUI}_T` | `_T` |
| `BatV` | `{DevEUI}_B` | `_B` |

### DecimalNullableConverter
- `JsonTokenType.String` → `null` (captura `"NULL"` e outras strings)
- `JsonTokenType.Null` → `null`
- `JsonTokenType.Number` → `decimal`

### Filtro de anomalia no worker (caminho LoRaWAN)
```
foreach read in uplinkReads:
    response = await mediator.Send(read)
    if response != null && response.Id > 0 && read.Code.EndsWith("_H"):
        await mediator.Send(DetectSensorAnomalyRequest)
```

### Log estruturado
- `LogInformation`: `"LoRaWAN uplink from DevEUI '{DevEUI}' fcnt={Fcnt}: {Count} metric(s) parsed"`
- `LogWarning`: quando `uplinkReads.Count == 0` (nenhuma métrica válida)

## DI Registration

```csharp
services.AddSingleton<ILoRaWanUplinkParser, LoRaWanUplinkParser>();
```

Adicionado em `StarkAgroWorker/Program.cs` antes de `AddHostedService<MqttWorkerService>()`.

## Risks & Flags

- `JsonDocument` e `JsonSerializer` usados em conjunto — liberar `JsonDocument` com `using`
- DevEUI normalizado para uppercase antes de concatenar com sufixo
- `PropertyNameCaseInsensitive = true` nos `JsonSerializerOptions` do parser para resiliência

## Test Coverage Plan

### LoRaWanUplinkParserTests

| Test | Cenário |
|------|---------|
| `Parse_ValidUplink_ReturnsThreeReads` | Payload completo → 3 reads com valores e timestamp corretos |
| `Parse_NullStringMetric_IsIgnored` | `TempC1: "NULL"` (e qualquer string) → ignorado |
| `Parse_NullJsonMetric_IsIgnored` | `BatV: null` → ignorado |
| `Parse_AbsentMetric_IsIgnored` | Campo ausente no data object → ignorado |
| `Parse_DevEUINormalized_ToUppercase` | DevEUI `a84041691d5f1794` → code `A84041691D5F1794_H` |
| `Parse_InvalidJson_ReturnsEmpty` | JSON mal-formado → lista vazia sem throw |
| `Parse_MissingDevEUI_ReturnsEmpty` | Uplink sem DevEUI → lista vazia |
| `Parse_MissingData_ReturnsEmpty` | Uplink sem campo `data` → lista vazia |
| `Parse_TimeAbsent_ReadAtIsNull` | Uplink sem campo `time` → `ReadAt == null` em todos os reads |

### MqttWorkerServiceTests (novos)

| Test | Cenário |
|------|---------|
| `MessageReceived_LoRaWanPayload_ParsesAndSendsThreeRequests` | DevEUI presente → parser chamado, 3 sends |
| `MessageReceived_LoRaWanPayload_AnomalyOnlyForHumidity` | 3 reads → anomaly dispatch apenas para `_H` |
| `MessageReceived_LegacyPayload_UsesLegacyPath` | Sem DevEUI → parser NÃO chamado |

## Verification

```bash
dotnet build StarkAgroAPI/StarkAgroAPI.csproj
dotnet build StarkAgroWorker/StarkAgroWorker.csproj
dotnet test StarkAgroWorker.Tests/StarkAgroWorker.Tests.csproj
```
