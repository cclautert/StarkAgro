Issue: https://github.com/cclautert/StarkAgro/issues/35

# F3 — Alertas de geada e calor extremo por área

## Context

Alerta de risco de geada (mín ≤ limiar, default 3 °C) e calor extremo (máx ≥ limiar, default
35 °C) nos próximos dias, por área. **Custo zero, nenhuma chave nova** — a frente mais barata do
épico. Geada é perda total em café; o valor/esforço é o melhor do roadmap.

## Correção da premissa do issue (importante)

O issue manda usar `IWeatherForecastService.GetForecastAsync`. **Esse serviço só traz precipitação**
(`DailyForecast` = Date/PrecipitationMm/Probability — sem temperatura). Quem tem temperatura é
`IAgricultureWeatherService.GetAgricultureDataAsync(lat, lng, days, ct)` → `DailyAgricultureData`
com `TempMax`/`TempMin` por dia, **já registrado no worker** (`Program.cs:62`). É este o dependency
correto. Sem tocar em modelo nem em serviço de previsão — mais barato ainda que o issue supunha.

## Acceptance Criteria → decisão

| Critério | Como |
|---|---|
| Centroide correto p/ círculo **e** polígono (teste puro) | `AreaCentroid.Of(area)`: usa `CenterLat/CenterLng` quando existem (círculo); senão o centro de `CdseProcessService.ComputeBbox(geometry)` (polígono). Estático e puro |
| Dedup: 2 ticks, mesma previsão → 1 alerta | índice único `{AreaId, AlertType, ForecastDate}`; `catch DuplicateKey` → no-op, como no NDVI/fogo |
| `ClimateAlertsEnabled` off ⇒ nenhuma chamada de previsão | worker checa a flag no início do tick e retorna antes de tocar o serviço |
| Área sem geometria utilizável é pulada | `AreaCentroid.Of` devolve null → área ignorada, sem estourar |
| Testes verdes | 1115 API + 96 worker atuais + novos |

## Affected layers

- **Helper novo:** `Services/Climate/AreaCentroid` (puro).
- **Entity + coleção:** `ClimateAlert` / `climate_alerts` + índice único.
- **Settings:** `PlatformAiSettings` +`ClimateAlertsEnabled`, `FrostAlertTempC` (3), `HeatAlertTempC` (35).
- **Admin AI settings:** 4 pontos + tela `/admin/ia`.
- **Worker novo:** `StarkAgroWorker/Services/ClimateWatchProcessor` (scheduler próprio — kill-switch independente do fogo, uma falha não derruba a outra).
- **Sino:** `GetUserAlertsHandler` sintetiza `FrostRisk`/`HeatRisk`; `layout.component.ts` rótulos.
- **DI:** só o worker (`Program.cs`): `AddHostedService<ClimateWatchProcessor>()`. `IAgricultureWeatherService` já registrado.

## New REST endpoints

**Nenhum.** Consome `GET /v1/user/alerts` (existe). Push pelo canal existente.

## Files to create

| Path | Tipo |
|---|---|
| `StarkAgroAPI/Services/Climate/AreaCentroid.cs` | helper puro `(double lat, double lng)? Of(MonitoredArea)` |
| `StarkAgroAPI/Models/Entities/ClimateAlert.cs` | `: Entity` |
| `StarkAgroWorker/Services/ClimateWatchProcessor.cs` | BackgroundService |
| testes espelhando cada um | — |

## Files to modify

- **`Models/agpDBContext.cs`** — `IMongoCollection<ClimateAlert> ClimateAlerts`, `GetCollection("climate_alerts")`, índice único `{AreaId, AlertType, ForecastDate}` + índice `{UserId, CreatedAt}` p/ o sino.
- **`Models/Entities/PlatformAiSettings.cs`** — 3 campos (bool + 2 int, defaults 3/35).
- **Admin (4 arquivos)** — propagar os 3 campos (padrão de `Sentinel2Enabled`).
- **`Domain/Handlers/Users/GetUserAlertsHandler.cs`** — ler `climate_alerts` do tenant na janela; mapear cada um (id `climate-{id}`, `AlertType` = `FrostRisk`/`HeatRisk`); nome da área via o mesmo lookup do fogo.
- **UI `layout.component.ts`** — rótulos `FrostRisk` ("Risco de geada"), `HeatRisk` ("Calor extremo"); clicável → `/areas/{id}`.
- **UI admin `ai-settings`** — toggle + 2 limiares (°C).
- **`CLAUDE.md`** — coleção nova, kill-switch, a correção do serviço de temperatura, guarda do limiar 0.

## MongoDB changes

- **Coleção nova:** `climate_alerts`.
- **Índice novo:** único `{AreaId, AlertType, ForecastDate}` + `{UserId, CreatedAt}`.
- **Campos novos:** `PlatformAiSettings` +3.

## Tenant isolation plan

`ClimateAlert.UserId` denormalizado do dono da área (padrão NDVI/fogo). `GetUserAlertsHandler` filtra
`x.UserId == userId`. `ClimateWatchProcessor` é serviço puro — tenant do documento da área. Sem
endpoint novo, sem vetor cross-tenant.

## Riscos & decisões

1. **Limiar 0 de documento legado.** O doc de settings de produção é anterior a esta feature → os
   3 campos desserializam com `false`/`0`. `HeatAlertTempC == 0` faria "máx ≥ 0" disparar **sempre**.
   Guarda no worker: `heat = HeatAlertTempC > 0 ? HeatAlertTempC : 35`. Frost em 0 é conservador
   (raramente ≤ 0 °C fora de geada real), não spamma — deixo `frost = FrostAlertTempC` como veio.
   Documentar que o admin deve ajustar em `/admin/ia`.
2. **Janela de previsão.** 3 dias — antecedência suficiente para acionar proteção sem alarme cedo demais.
3. **Push sem spam.** Um push por área por tick, nomeando o risco mais próximo, mesmo que 2 dias
   cruzem o limiar no mesmo tick. Os alertas individuais (por dia) ficam no sino via `climate_alerts`.
4. **Previsão indisponível.** `GetAgricultureDataAsync` devolve null em falha → área pulada, sem alerta
   falso. Sem fallback inventado.

## DI registration

`Program.cs`: `AddHostedService<ClimateWatchProcessor>()`. Nenhum serviço novo — `IAgricultureWeatherService`
e push já registrados.

## Verification

1. `dotnet build` API + worker.
2. `dotnet test` — verdes, ≥90% nos arquivos novos. Testes:
   - `AreaCentroid`: círculo (usa Center) e polígono (usa bbox) dão o ponto certo; sem geometria → null.
   - Worker: flag off ⇒ `IAgricultureWeatherService` nunca chamado; TempMin ≤ frost → FrostRisk;
     TempMax ≥ heat → HeatRisk; dedup (DuplicateKey) → sem push; previsão null → área pulada;
     `HeatAlertTempC=0` cai para 35 (não dispara sempre).
   - `GetUserAlertsHandler`: climate alerts entram no sino com o tipo certo.
3. `npm run start` → sino com FrostRisk/HeatRisk (após seed manual); `/admin/ia` com os campos.
4. **E2E real (opcional, sem custo):** ligar em `/admin/ia`, área com geometria; como o Open-Meteo é
   gratuito, um tick real busca a previsão de verdade — validar que um limiar alto (ex.: heat 15 °C)
   dispara e o dedup segura o 2º tick.
