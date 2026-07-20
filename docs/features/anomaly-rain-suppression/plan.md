# Implementation Plan: Supressão de anomalias de sensor em período de chuva
Issue: (sem issue — decisão em sessão 2026-07-02)
Generated: 2026-07-02

## Context
Choveu todos os dias nos últimos ~10 dias e o sensor 1 ficou em 99,9% de umidade (solo saturado) — comportamento real, não defeito. O detector de anomalias (média ± 2,5σ) gerou 437 alertas falsos. É preciso cruzar a detecção com a precipitação recente no local do pivô: **umidade alta + chuva recente = normal, não alertar**.

## Acceptance Criteria
1. Anomalia de **alta** (valor acima da média da baseline) é **suprimida** quando a precipitação acumulada recente na localização do pivô ≥ limiar de chuva → leitura não é marcada `IsAnomaly`, nenhum registro em `sensor_anomalies`, nenhum push. Efeito colateral desejado: a leitura entra na baseline e a faixa esperada se adapta ao período chuvoso (evita o self-lock na raiz).
2. Anomalia de **baixa** (valor abaixo da média) **nunca** é suprimida por chuva (queda de umidade chovendo é ainda mais suspeita).
3. Limiar de chuva reutiliza a precedência existente: `pivot.RainThresholdMm ?? user.RainThresholdMm ?? settings.RainThresholdMm` (padrão 5 mm) — mesmo padrão de `GetIrrigationTrendHandler`.
4. Janela de chuva observada: `AnomalyRainLookbackDays` (novo em `WeatherForecastSettings`, padrão 2 dias) — Open-Meteo `past_days` + hoje.
5. Pivô **sem lat/long**: comportamento atual inalterado (detecção normal, sem consulta de chuva) — convenção do projeto.
6. Falha na consulta Open-Meteo: fail-open (detecção normal), com log.
7. Consulta de chuva cacheada em memória (TTL `CacheDurationMinutes`, 60 min) — leituras chegam a cada ~20 min por sensor.

## Affected Layers
handlers / services (Forecast) / configuration / Worker DI — sem mudanças em controllers, entidades, MongoDB ou UI.

## New REST Endpoints
Nenhum.

## Files to Create
| Path | Type | Summary |
|---|---|---|
| (nenhum arquivo novo de produção — só testes) | | |

## Files to Modify
| Path | Change |
|---|---|
| `StarkAgroAPI/Models/Interfaces/IAgricultureWeatherService.cs` | + `Task<double?> GetRecentPrecipitationAsync(double lat, double lon, int pastDays, CancellationToken ct)` (mm acumulados; null = indisponível) |
| `StarkAgroAPI/Services/Forecast/OpenMeteoForecastService.cs` | Implementar o método: URL `v1/forecast?latitude=..&longitude=..&daily=precipitation_sum&past_days={n}&forecast_days=1&timezone=UTC`, somar `precipitation_sum` |
| `StarkAgroAPI/Configuration/WeatherForecastSettings.cs` | + `public int AnomalyRainLookbackDays { get; set; } = 2;` |
| `StarkAgroAPI/Domain/Handlers/Anomalies/DetectSensorAnomalyHandler.cs` | Injetar `IAgricultureWeatherService`, `IOptions<WeatherForecastSettings>`, `IMemoryCache`; carregar `Pivot` e `User`; antes da detecção: se valor > média da baseline e chuva recente ≥ limiar → return (suprimido, log info) |
| `StarkAgroWorker/Program.cs` | Registrar `AddMemoryCache()`, `Configure<WeatherForecastSettings>`, `AddHttpClient<OpenMeteoForecastService>` (BaseAddress open-meteo, timeout 8 s, mesmo padrão do `ApiConfig`), `IAgricultureWeatherService` |
| `StarkAgroAPI.Tests/Domain/Handlers/Anomalies/DetectSensorAnomalyHandlerTests.cs` | Atualizar construtor + novos cenários |
| `StarkAgroAPI.Tests/Services/Forecast/OpenMeteoForecastServiceTests.cs` | + testes do novo método (URL com `past_days`, parse, falha → null) |

## MongoDB Changes
Nenhuma.

## Tenant Isolation Plan
Sem novas consultas por usuário vindas de request — o handler já opera com `UserId` da leitura MQTT (fluxo interno do worker). Consulta de `User` apenas para `RainThresholdMm` do dono da leitura.

## Risks & Flags
- [CRITICAL] Worker DI: sem os registros em `Program.cs` do worker, o MediatR lança exceção ao resolver o handler dentro do worker — é o ponto mais sensível da mudança.
- [WARNING] Fail-open: se Open-Meteo cair, voltam a valer as regras atuais (pode alertar em dia de chuva) — aceitável; logar warning.
- [WARNING] `past_days` retorna também "hoje" (parcialmente previsto) — somamos tudo; semanticamente ok para "choveu recentemente".
- [INFO] A supressão faz a leitura chuvosa entrar na baseline → faixa esperada acompanha o período chuvoso naturalmente.

## DI Registration
| Interface | Implementation | Lifetime | File |
|---|---|---|---|
| `IAgricultureWeatherService` | `OpenMeteoForecastService` | Scoped (via HttpClient factory) | `StarkAgroWorker/Program.cs` (já existe no `ApiConfig`) |
| `IMemoryCache` | `AddMemoryCache()` | Singleton | `StarkAgroWorker/Program.cs` |

## Verification
```bash
dotnet build StarkAgroAPI/StarkAgroAPI.csproj
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj
dotnet test StarkAgroWorker.Tests/StarkAgroWorker.Tests.csproj
```
Cenários de teste do handler: (a) chuva ≥ limiar + valor alto → suprime (sem insert/push); (b) chuva ≥ limiar + valor baixo → detecta normal; (c) sem lat/long → detecta normal sem chamar forecast; (d) forecast null → detecta normal; (e) chuva < limiar → detecta normal.
Pós-deploy: sensor 1 continua em 99,9 e está chovendo → não deve surgir nenhuma anomalia nova; conferir via MongoDB na VPS e logs do worker (`docker logs agripwebworker | grep -i rain`).
