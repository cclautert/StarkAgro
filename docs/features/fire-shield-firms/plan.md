Issue: https://github.com/cclautert/StarkAgro/issues/34

# F2 — Fire Shield: focos de calor via NASA FIRMS

## Context

Alerta de foco de calor perto das áreas monitoradas, com push e no sino. API do FIRMS é
**gratuita** (MAP_KEY, 5.000 req/10 min) — **zero Processing Unit**. Reusa push VAPID, sino,
e-mail de alerta e a mecânica de fila do worker que já existem.

## ⚠️ Latência honesta (critério de aceite, não cosmético)

O deck promete <5 min. O FIRMS entrega URT <60 s **só para EUA/Canadá**; no Brasil é RT (~1 h)
ou NRT. **A UI declara ~1 h.** É antecedência para acionar brigada, não vigilância em tempo real.

## Acceptance Criteria → decisão

| Critério | Como |
|---|---|
| Parser CSV: linha real / cabeçalho ausente / campo faltando / vazio | **parse por NOME de coluna** (mapa header→índice), não por posição. Cabeçalho ausente ou coluna faltando → devolve vazio, nunca linha desalinhada |
| Índice único barra reentrega | `{AreaId, Latitude, Longitude, AcquiredAt, Satellite}` único; `catch DuplicateKey` → no-op, como no NDVI |
| `FireAlertsEnabled` off ⇒ nenhuma chamada externa | worker checa a flag no início do tick e retorna antes de tocar o FIRMS |
| Falha de push/e-mail não perde o alerta | grava o hotspot primeiro; envio em `try/catch` separado (padrão do `IrrigationAlertScheduler`) |
| UI declara ~1 h | copy no sino/tela, sem "5 min" |
| Testes verdes | 1095 API + 85 worker atuais + novos |

## Colunas CSV do VIIRS (confirmadas na doc NASA)

`latitude, longitude, bright_ti4, scan, track, acq_date, acq_time, satellite, instrument,
confidence, version, bright_ti5, frp, daynight`

- `acq_date` = `YYYY-MM-DD`; `acq_time` = `HHMM` UTC (ex.: `0742`) → combinar em `AcquiredAt` UTC.
- `confidence` VIIRS = `low`/`nominal`/`high` (ou `l`/`n`/`h` em algumas respostas) — **guardar cru
  como string, não gatilhar lógica no encoding** (evita a classe de bug de hoje). Filtro de `low`
  fica como melhoria futura.
- `frp` (Fire Radiative Power) e `bright_ti4` guardados para contexto; não são gatilho.

## Affected layers

- **Service novo:** `Services/Fire/FirmsHotspotService` (HttpClient tipado, CSV → `IReadOnlyList<FireHotspotDto>?`).
- **Bbox:** reusar `CdseProcessService.ComputeBbox`; helper novo puro para expandir pelo raio.
- **Entity + coleção:** `FireHotspot` / `fire_hotspots` + índice único.
- **Settings:** `PlatformAiSettings` +`FirmsMapKey`, `FireAlertsEnabled`, `FireAlertRadiusKm`.
- **Admin AI settings:** 4 pontos (request/response/2 handlers) + tela `/admin/ia`.
- **Worker novo:** `StarkAgroWorker/Services/FireWatchProcessor` (BackgroundService).
- **Sino:** `GetUserAlertsHandler` sintetiza `FireHotspot`; `layout.component.ts` rótulo+rota.
- **DI:** `ApiConfig.cs` **e** `StarkAgroWorker/Program.cs` (HttpClient + serviço + hosted service).

## New REST endpoints

**Nenhum.** O front consome via `GET /v1/user/alerts` (já existe). Push via canal existente.

## Files to create

| Path | Tipo |
|---|---|
| `StarkAgroAPI/Services/Fire/FirmsHotspotService.cs` | serviço + `IFirmsHotspotService` + `FireHotspotDto` record + parser estático testável |
| `StarkAgroAPI/Services/Fire/FireAreaBbox.cs` | helper puro: expande `NdviBbox` pelo raio (correção de lng por `cos(lat)`) |
| `StarkAgroAPI/Models/Entities/FireHotspot.cs` | entidade `: Entity` |
| `StarkAgroWorker/Services/FireWatchProcessor.cs` | BackgroundService |
| testes espelhando cada um | — |

## Files to modify

- **`Models/agpDBContext.cs`** — `IMongoCollection<FireHotspot> FireHotspots`, `GetCollection("fire_hotspots")`, índice único `{AreaId, Latitude, Longitude, AcquiredAt, Satellite}`.
- **`Models/Entities/PlatformAiSettings.cs`** — 3 campos novos (string? key, bool, int radius default 10).
- **Admin (4 arquivos)** — propagar os 3 campos (a `FirmsMapKey` é segredo: **write-only na tela**, como as chaves da CDSE — nunca devolver o valor cru no response, seguir o padrão existente de `CdseClientSecret`).
- **`Domain/Handlers/Users/GetUserAlertsHandler.cs`** — ler `fire_hotspots` do tenant na janela; **agrupar por `{AreaId, acq_date}`** num alerta só ("{N} foco(s) de calor a até {raio} km de {área}"), id `fire-{areaId}-{yyyyMMdd}`. Sem agrupar, um incêndio grande viraria 50 linhas no sino.
- **UI `layout.component.ts`** — título `FireHotspot`, ícone/rota (abre `/areas/{id}`); copy de latência.
- **UI admin `ai-settings`** — campos FIRMS (key write-only, toggle, raio) + hint de latência.
- **`CLAUDE.md`** — coleção nova, kill-switch, latência real do FIRMS, parse-por-header.

## MongoDB changes

- **Coleção nova:** `fire_hotspots`.
- **Índice novo:** único `{AreaId, Latitude, Longitude, AcquiredAt, Satellite}`.
- **Campos novos:** `PlatformAiSettings` +3.

## Tenant isolation plan

`FireHotspot.UserId` denormalizado do dono da área (padrão `NdviReading.UserId`). `GetUserAlertsHandler`
filtra `x.UserId == userId` — **mesma linha dos outros alertas**. `FireWatchProcessor` é serviço puro,
tenant vem do documento da área (`WorkerUserContext.UserId` é null, como no NDVI). Sem endpoint novo,
sem vetor cross-tenant.

## Riscos & decisões (precisam de OK antes de codar)

1. **BLOQUEADOR do gate de validação real.** Diferente do NDVI (credencial já em prod), **não há
   FIRMS MAP_KEY** cadastrado. Sem ele **não dá para rodar o POST real que pegou os bugs de hoje**.
   Duas saídas: (a) você registra um MAP_KEY gratuito (~2 min em firms.modaps.eosdis.nasa.gov) e eu
   valido de verdade antes do PR; (b) shipar com o parser **exaustivamente testado offline** (o
   parse-por-header reduz muito o risco) e o gate real fica como passo pós-merge, ao ligar a chave.
   **Recomendo (a)** — é o único jeito de não repetir o erro de hoje. *Precisa da sua escolha.*
2. **Granularidade do push (decidido, mas registrando).** Um push **por área por tick** com a
   contagem, não um por foco — um incêndio com 40 focos não pode virar 40 notificações. Os focos
   individuais ficam em `fire_hotspots` (mapa/histórico + idempotência); o alerta é agregado.
3. **Quais áreas vigiar.** As mesmas com `MonitoringEnabled == true` (população do NDVI). Fogo não
   depende do Sentinel-2, mas reusa o flag de "área ativa" para não vigiar talhão arquivado.
4. **Rate limit.** 5.000 req/10 min. Uma req por área por tick; com tick de 15 min e milhares de
   áreas ainda folgado. Sem teto de custo (não há PU), mas logar se aproximar do limite.

## DI registration

- `ApiConfig.cs` + `StarkAgroWorker/Program.cs`: `AddHttpClient<FirmsHotspotService>` (BaseAddress
  `https://firms.modaps.eosdis.nasa.gov/`), `AddScoped<IFirmsHotspotService>`.
- `Program.cs`: `AddHostedService<FireWatchProcessor>()` (só no worker).

## Verification

1. `dotnet build` API + worker (matar API no Windows).
2. `dotnet test` API + worker — verdes, ≥90% nos arquivos novos. Testes do parser: header real +
   1 linha; **cabeçalho ausente**; **coluna faltando**; **CSV vazio**; linha com `acq_time` de 1–4
   dígitos; `confidence` em ambos os encodings. Índice único: 2º insert do mesmo foco → DuplicateKey
   tratado. Worker: flag off ⇒ `IFirmsHotspotService` nunca chamado (Moq `Times.Never`); falha de
   push não impede o insert.
3. **Gate FIRMS real** (depende da decisão #1): `GET /api/area/csv/{key}/VIIRS_SNPP_NRT/{bbox}/1`
   com bbox de uma área real, exigir 200 + CSV parseável. Sem isso, marcar no PR que a validação
   real está **pendente de MAP_KEY**, honestamente — não afirmar que foi validado.
4. `npm run start` → sino com alerta de fogo (após seed manual de um `fire_hotspots`); `/admin/ia`
   com os campos FIRMS.
