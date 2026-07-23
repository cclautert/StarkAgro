Issue: https://github.com/cclautert/StarkAgro/issues/38

# F5 — Sentinel-1 (SAR): enxergar através da nuvem

## Context

Radar (Sentinel-1 GRD) atravessa nuvem e opera à noite — vê justamente nas datas em que o NDVI
tem buraco de nuvem. Índice RVI (proxy de biomassa/estrutura por radar) + VV/VH crus. Mesma
Sentinel Hub API do NDVI (mesmo host, mesmo OAuth), custo baixo (fator PU 1,0).

## Validação de risco (feita ANTES do plano, contra a CDSE real)

Request S1 GRD Statistical → **HTTP 200**, 7 passagens no mês (ciclo ~6 d confirmado), `rvi=0.51`,
`vv=0.14`, `vh=0.021`. Formato validado: `type: sentinel-1-grd`, `dataFilter{acquisitionMode:IW,
polarization:DV, orbitDirection:DESCENDING}`, `processing{backCoeff:GAMMA0_ELLIPSOID}`, evalscript
VV/VH/dataMask → RVI. Gate real repetido no fim com o corpo do C#.

## Duas decisões de arquitetura (desvios do issue, mais limpos — flagados)

1. **Serviço S1 SEPARADO, não parametrizar o `CdseStatisticalService`.** O issue sugere trocar o
   `type` em 2 lugares. Mas o S1 tem evalscript, `dataFilter` (sem nuvem) e parsing totalmente
   diferentes — enfiar no serviço do NDVI o polui e arrisca o caminho S2. Um `CdseSentinel1Service`
   novo deixa o NDVI **100% intocado** (satisfaz "não quebrar o S2" com mais força). O critério
   "type parametrizado" vira "S2 segue `sentinel-2-l2a`" — testado, e verdadeiro por construção.
2. **Worker itera + dedup, NÃO clona o claim atômico do `NdviProcessor`.** O claim do NDVI usa os
   campos de worker do `MonitoredArea` (`Status`/`NextFetchAt`/`WorkerId`). Um 2º processor
   clamando os mesmos campos **brigaria com o NDVI**. O padrão certo é o do `FireWatchProcessor`/
   `ClimateWatchProcessor` (itera áreas `MonitoringEnabled`, dedup por índice único no insert) — sem
   claim, sem conflito, já provado em F2/F3.

## Acceptance Criteria → decisão

| Critério | Como |
|---|---|
| `orbitDirection` fixo, persistido, série nunca mistura geometrias | const `DESCENDING`; gravado em cada `Sentinel1Reading.OrbitDirection`; índice único inclui a órbita |
| `type` sem quebrar o S2 | serviço separado; teste asserindo que o request de NDVI segue `sentinel-2-l2a` |
| `Sentinel1Enabled` off ⇒ nenhuma chamada à CDSE | worker checa a flag no topo do tick |
| Índice único barra reprocessamento | `{AreaId, AcquisitionDate, OrbitDirection}` único; `catch DuplicateKey` → no-op |
| UI deixa claro que é radar | aba "RVI (radar)" no seletor, com copy: existe nas datas de nuvem, não é "NDVI na chuva" |
| Testes verdes | 1141 API + 107 worker atuais + novos |

## Files to create

- `Services/Sentinel1/CdseSentinel1Service.cs` (+`ICdseSentinel1Service`, record `Sentinel1Stat`, parser estático) — evalscript VV/VH/RVI, `BuildRequestBody`, `Parse` (lê `outputs.rvi/vv/vh.bands.B0.stats`).
- `Services/Sentinel1/Sentinel1FetchService.cs` (+`ISentinel1FetchService`, `Sentinel1FetchOutcome`) — busca S1 e grava readings com dedup + custo; órbita fixa `DESCENDING`. Serviço puro (tenant do documento), como `NdviFetchService`.
- `Services/Sentinel1/Sentinel1CostService.cs` (+ interface) — espelho puro de `NdviCostService`: soma `Sentinel1Reading.Sentinel1CostCents` do mês para `/admin/ia`.
- `Models/Entities/Sentinel1Reading.cs` — `: Entity`.
- `StarkAgroWorker/Services/Sentinel1Processor.cs` — BackgroundService (itera + dedup).

## Files to modify

- `Models/agpDBContext.cs` — coleção `sentinel1_readings` + índice único `{AreaId, AcquisitionDate, OrbitDirection}` + índice `{UserId, AcquisitionDate}`.
- `Models/Entities/PlatformAiSettings.cs` — `Sentinel1Enabled` (bool) + `Sentinel1CostCents` (int, default 1).
- Admin 4 arquivos — propagar os 2 campos + `CurrentMonthSentinel1CostCents` na response/get.
- `Domain/Commands/Responses/Ndvi/NdviTrendResponse.cs` — `List<Sentinel1TrendPoint> Radar` (`{AcquisitionDate, RviMean, VvMean, VhMean}`).
- `Domain/Handlers/Ndvi/GetNdviTrendHandler.cs` — ler `sentinel1_readings` do tenant e projetar em `Radar` (a série S1 tem datas próprias, não casa com os pontos do NDVI).
- UI `area-detail` — aba "RVI (radar)" que plota `trend.radar` (série separada, datas próprias); eixo Y solto (RVI ~0–1, mas não fixar).
- UI `models/monitored-area.model.ts` — `Sentinel1TrendPoint` + `radar?` no `NdviTrendResponse`.
- UI admin `ai-settings` — toggle `Sentinel1Enabled` + custo/busca + gasto do mês.
- `Configuration/ApiConfig.cs` + `StarkAgroWorker/Program.cs` — `AddHttpClient<CdseSentinel1Service>` (host `sh.dataspace...`), `AddScoped` das interfaces; `AddHostedService<Sentinel1Processor>` (só worker).
- `CLAUDE.md` — coleção nova, kill-switch, a pegadinha da órbita, os 2 desvios de arquitetura.

## MongoDB changes

- **Coleção nova:** `sentinel1_readings`.
- **Índice novo:** único `{AreaId, AcquisitionDate, OrbitDirection}` + `{UserId, AcquisitionDate}`.
- **Campos novos:** `PlatformAiSettings` +2.

## Tenant isolation plan

`Sentinel1Reading.UserId` denormalizado do dono da área (padrão NDVI). `GetNdviTrendHandler` já filtra
a área por `UserId`; a query de `sentinel1_readings` filtra `AreaId == request.AreaId && UserId == userId`.
`Sentinel1Processor`/`Sentinel1FetchService` são puros — tenant do documento da área. Sem endpoint novo.

## Riscos & decisões

1. **Órbita fixa** — `DESCENDING` const. Misturar asc/desc faz o backscatter pular por geometria de
   visada, não por lavoura (o "ramp desalinhado" do SAR). A órbita vai no índice único e em cada
   reading, então trocar depois não corrompe série antiga.
2. **RVI** — `4·VH/(VV+VH)` com `GAMMA0` linear (validado, deu 0,51). Variante padrão; documentar a fórmula.
3. **Custo/orçamento** — kill-switch `Sentinel1Enabled` é o controle primário. `Sentinel1CostCents`
   gravado por reading + gasto do mês em `/admin/ia`. **Sem teto de orçamento dedicado no MVP** (o
   `NdviMonthlyBudgetCents` é do NDVI; S1 é ~18% do total e o free tier é 40k PU) — nota de follow-up.
4. **Gate real repetido** — POST do corpo do C# à CDSE no fim, exigindo 200 + as 3 saídas.

## DI registration

`ApiConfig.cs` (API lê o trend) + `Program.cs` (worker busca): `AddHttpClient<CdseSentinel1Service>`,
`AddScoped<ICdseSentinel1Service|ISentinel1FetchService|ISentinel1CostService>`. `Program.cs`:
`AddHostedService<Sentinel1Processor>`.

## Verification

1. `dotnet build` API + worker.
2. `dotnet test` — verdes, ≥90% nos novos. Testes: `BuildRequestBody` pede `sentinel-1-grd` +
   `orbitDirection DESCENDING` + sem `maxCloudCoverage`; `Parse` lê rvi/vv/vh; NDVI segue
   `sentinel-2-l2a` (regressão do S2); fetch grava com `OrbitDirection` e dedup; worker flag-off ⇒
   sem chamada; `GetNdviTrendHandler` projeta `Radar` filtrado por tenant.
3. **Gate CDSE real** (custa PU mínima): POST do corpo do `CdseSentinel1Service.BuildRequestBody` → 200 + rvi/vv/vh; `node --check` no evalscript.
4. `npm run start` → `/areas/:id` com a aba "RVI (radar)"; `/admin/ia` com o toggle + gasto S1.
