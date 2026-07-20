Issue: https://github.com/cclautert/StarkAgro/issues/7

# NDVI F2 — Integração CDSE (Statistical) + worker NdviProcessor + tendência

## Context

Fase que efetivamente busca o NDVI: integra o **Copernicus Data Space Ecosystem (CDSE)** (grátis, OAuth2 client-credentials + Statistical API), um worker `NdviProcessor` que agenda e processa as áreas (clonando `PlantDiagnosisProcessor`), e o endpoint de tendência. Depende de #6 (branch a partir de `feat/ndvi-monitored-area`). **Backend + API**; a UI (gráfico Chart.js) fica para follow-up.

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| Admin configura CDSE + liga `Sentinel2Enabled` em `/admin/ia` | Campos novos em `PlatformAiSettings` (`CdseClientId/Secret`, `Sentinel2Enabled`, `NdviCostCents`) surfaçados via `UpdatePlatformAiSettingsHandler` + request/response |
| Worker reivindica área, busca Statistical, grava `NdviReading` (mean 0,2–0,9) | `NdviProcessor` (claim atômico) → `NdviFetchService` → `CdseStatisticalService` |
| Refetch da mesma passagem é no-op | Índice **único** `{AreaId, AcquisitionDate}` + check `LastAcquisitionDate`; `catch DuplicateKey` como backstop de corrida |
| Nuvem é terminal (grava `CloudRejected`, sem retry-storm) | Passagem nublada vira `NdviReading{CloudRejected=true}`; só falha real (HTTP/parse) entra no retry/backoff |
| Kill-switch off interrompe fetches | `NdviFetchService` lê `Sentinel2Enabled` e sai cedo |

## Affected layers

`PlatformAiSettings` (+admin surface), serviços CDSE + fetch (`Services/Ndvi/`), entidade `NdviReading` + `agpDBContext`, worker (`NdviProcessor`), DI (`ApiConfig` + `Program.cs`), handler+endpoint de tendência. UI adiada.

## New REST endpoints

| Método | Rota | Auth | Response |
|---|---|---|---|
| GET | `/v1/areas/{id}/trend` | `[Authorize]`, ownership-checked | `NdviTrendResponse` (série) |

(+ os campos CDSE entram no `GET/PUT /v1/admin/ai-settings` já existente.)

## Files to create

- `Models/Entities/NdviReading.cs` (coleção `ndvi_readings`): `int Id`, `int AreaId`, `int UserId`, `DateTime AcquisitionDate`, `double NdviMean/NdviMin/NdviMax/NdviStdev`, `double CloudCoveragePct`, `bool CloudRejected`, `ObjectId? OverlayImageFileId`, `int NdviCostCents`, `DateTime CreatedAt`.
- `Services/Ndvi/CdseTokenProvider.cs` (`ICdseTokenProvider`) — OAuth2 client-credentials no endpoint de identidade da CDSE; token em `IMemoryCache` (expiry = `expires_in` − margem). Typed `HttpClient`.
- `Services/Ndvi/CdseStatisticalService.cs` (`ICdseStatisticalService`) — typed `HttpClient`; monta a request da Statistical API (bounds = geometria da área, `dataFilter.maxCloudCoverage`, evalscript NDVI = (B08−B04)/(B08+B04) + dataMask, `aggregationInterval P1D`), parsing defensivo com `JsonDocument`, `catch → null`. Retorna `IReadOnlyList<NdviStat>` (data, mean/min/max/stdev, cloudPct). Evalscript = constante.
- `Services/Ndvi/NdviFetchService.cs` (`INdviFetchService`) — **serviço puro** (não handler): lê `PlatformAiSettings` (kill-switch/keys), pega token, chama Statistical p/ a janela desde `LastAcquisitionDate`, para cada passagem nova monta `NdviReading` (nuvem→`CloudRejected`), insere com dedup (índice único + `catch DuplicateKey`), avança `LastAcquisitionDate`/`LastFetchAt`/`NextFetchAt`. Retorna outcome (`Success`/`Failed`/`Disabled`).
- `Domain/Commands/Requests/Ndvi/GetNdviTrendRequest.cs` + `Responses/Ndvi/NdviTrendResponse.cs`.
- `Domain/Handlers/Ndvi/GetNdviTrendHandler.cs` — verifica `area.UserId == _currentUser.UserId`, devolve os `NdviReading` da área ordenados por `AcquisitionDate`.
- `StarkAgroWorker/Services/NdviProcessor.cs` — clona `PlantDiagnosisProcessor`. Claim atômico das áreas devidas: `MonitoringEnabled && Status==Idle && (NextFetchAt==null || NextFetchAt<=now) && (NextAttemptAt==null || NextAttemptAt<=now)` → `Status=Fetching`. Sucesso → `Status=Idle`, `LastFetchAt=now`, `NextFetchAt=now+5d`, `NextAttemptAt=null`. Falha → retry/backoff 1/5/15 (`MaxRetries=3`) via `NextAttemptAt`, `Status=Idle` (ou `Failed`). Zombie: `Status==Fetching && ProcessingStartedAt<cutoff` → volta p/ `Idle`. Tenant lido do documento.
- Testes: parsing do `CdseStatisticalService` (contra JSON de amostra), `NdviFetchService` (com `ICdseStatisticalService` mockado: nova passagem, nuvem, dedup, kill-switch), `GetNdviTrendHandler` (ownership), e a lógica de claim/fail do `NdviProcessor`.

## Files to modify

- `Models/Entities/PlatformAiSettings.cs` — `CdseClientId`, `CdseClientSecret`, `bool Sentinel2Enabled=false`, `int NdviCostCents` (default ~1).
- `Domain/Commands/Requests/Admin/UpdatePlatformAiSettingsRequest.cs` + `Handlers/Admin/UpdatePlatformAiSettingsHandler.cs` + `Responses/Admin/AdminAiSettingsResponse.cs` — surfaçar os campos CDSE.
- `Models/agpDBContext.cs` — coleção `NdviReadings` + índices `{AreaId, AcquisitionDate desc}`, **único `{AreaId, AcquisitionDate}`**, `{UserId}`.
- `Configuration/ApiConfig.cs` + `StarkAgroWorker/Program.cs` — `AddHttpClient<CdseTokenProvider>`/`<CdseStatisticalService>`, `AddScoped` dos serviços Ndvi; no worker também `AddHostedService<NdviProcessor>()`.
- `Controllers/NdviController.cs` — `GET /areas/{id}/trend`.
- `CLAUDE.md` — coleção `ndvi_readings` + a pitfall do CDSE (secrets no `platform_ai_settings`, kill-switch, dedup por índice único).

## MongoDB changes

Coleção `ndvi_readings` (IDs via `GetNextIdAsync`). Índices acima (o único `{AreaId, AcquisitionDate}` é o backstop de idempotência). Campos novos em `platform_ai_settings` (backward-compatible).

## Tenant isolation plan

Trend: `GetNdviTrendHandler` casa `area.Id` **e** `area.UserId == _currentUser.UserId` antes de ler os readings; `NdviReading.UserId` denormalizado confirma. Worker: tenant vem do documento da área (`area.UserId`), nunca de contexto de usuário (`WorkerUserContext.UserId` é null). Fetch escreve `NdviReading.UserId = area.UserId`.

## Risks & flags

- **Request/evalscript da CDSE não são testáveis ao vivo aqui** (as credenciais estão `CHANGE_ME`). O código de **parsing** é unit-testado contra um JSON de amostra; a **montagem da request/evalscript** precisa de validação ao vivo quando o admin puser as credenciais. Documentar isso.
- **Idempotência sob concorrência**: índice único `{AreaId, AcquisitionDate}` + `catch MongoWriteException/DuplicateKey` → no-op (nunca erro).
- **Nuvem terminal**: grava `CloudRejected` e avança a passagem; só falha real re-tenta (espelha "recusa é terminal" do laudo).
- **Custo de PU**: kill-switch + cadência ~5 dias + `NdviCostCents` congelado por reading. Teto global fica para a #9.
- **Agendamento**: claim combina "devido" (`NextFetchAt`) + "não em backoff" (`NextAttemptAt`) num só `FindOneAndUpdate` — sem estado `Queued` separado.

## DI registration

`ICdseTokenProvider`→`CdseTokenProvider` (typed HttpClient), `ICdseStatisticalService`→`CdseStatisticalService` (typed HttpClient), `INdviFetchService`→`NdviFetchService` (scoped), todos em `ApiConfig` **e** `Program.cs`. `AddHostedService<NdviProcessor>()` só no worker. `AddMemoryCache()` já existe nos dois.

## Verification

- `dotnet build StarkAgro.sln` + `dotnet test StarkAgro.sln` (solution inteira).
- Cobertura ≥90% nos arquivos novos (parsing/fetch/handler/claim; a I/O HTTP da CDSE é integração).
- Live (após credenciais): admin liga `Sentinel2Enabled` + põe `CdseClientId/Secret` → worker reivindica uma área conhecida → `NdviReading` com mean 0,2–0,9 → `GET /v1/areas/{id}/trend` devolve a série; refetch no mesmo dia é no-op.

## Escopo (a confirmar)

**Backend + API**; a UI (`areas/:id` com gráfico Chart.js + badge de nuvem/data) fica para follow-up — mesmo padrão das fases anteriores. Confirmar.
