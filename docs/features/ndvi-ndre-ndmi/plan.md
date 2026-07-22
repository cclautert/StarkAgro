Issue: https://github.com/cclautert/StarkAgro/issues/33

# F1 — NDRE + NDMI na mesma passagem do NDVI

## Context

Hoje cada passagem grava só NDVI. NDVI **satura** em dossel denso (café adulto, cana fechada,
soja no pico), onde para de discriminar. Red-Edge (**NDRE**, B05) não satura e responde a
N/clorofila; **NDMI** (SWIR B11) pega estresse hídrico antes de sintoma visível. Esta fase busca
os três numa **única** requisição CDSE — 6 bandas de entrada → fator PU 2,0 contra 1,33, metade
do preço de três chamadas.

## Reconciliação com o estado atual (o issue é anterior aos fixes de hoje)

O issue foi escrito antes do trabalho de histograma/overlay de hoje. Dois pontos precisam de
decisão explícita **antes** de codar — estão em *Riscos & decisões*.

## Acceptance Criteria → decisão de implementação

| Critério | Como | 
|---|---|
| `BuildRequestBody` declara as três saídas | evalscript com outputs `ndvi`/`ndre`/`ndmi`/`dataMask`, bandas B04,B05,B08,B11,SCL,dataMask |
| três blocos de histograma | `calculations` com ndvi+ndre+ndmi (mesmo spec uniforme -1..1/200). **Parse agrega só o do ndvi em ClassCounts**; os de ndre/ndmi voltam e ficam guardados para quando houver faixas de classe |
| `Parse` lê os três; legado (só ndvi) não quebra | `TryGetStats(interval, outputId)` parametrizado; ndre/ndmi ausentes → 0, não exceção |
| `ExtraIndicesEnabled` off ⇒ 4 bandas | flag threaded até `BuildRequestBody`; off = request idêntico ao de hoje |
| evalscript validado com `node --check` antes de gastar PU | passo obrigatório de verificação + POST real à CDSE |
| `/admin/ia` mostra gasto ~1,5× | **ver Riscos #2:** `NdviCostCents` é proxy manual; não escala sozinho |
| Testes verdes | manter os 1087 atuais + novos |

## Affected layers

- **Service:** `CdseStatisticalService` — evalscript, `BuildRequestBody`, `Parse`, `TryGetStats`, e o record `NdviStat`. Interface `ICdseStatisticalService.GetStatisticsAsync` ganha `bool extraIndices`.
- **Fetch:** `NdviFetchService` — lê `settings.ExtraIndicesEnabled`, passa adiante, grava os campos novos no `NdviReading`.
- **Entity:** `NdviReading` — 8 campos novos (NDRE/NDMI × mean/min/max/stdev).
- **Settings:** `PlatformAiSettings` — `ExtraIndicesEnabled` (bool, default false).
- **Admin:** request/response/2 handlers de AI settings expõem o toggle (mesmo padrão de `Sentinel2Enabled`).
- **Response + handler:** `NdviTrendPoint` ganha os três índices; `GetNdviTrendHandler` projeta (sem tocar `BuildClasses`).
- **UI:** `area-detail` seletor NDVI/NDRE/NDMI no gráfico de tendência; modelo `NdviTrendPoint`.

## New REST endpoints

**Nenhum.** Reusa `GET /v1/areas/{id}/trend` (payload cresce) e `PUT` de admin AI settings existente.

## Files to create

| Path | Tipo |
|---|---|
| `docs/features/ndvi-ndre-ndmi/plan.md` | este plano |

Nenhum arquivo de produção novo — a feature é aditiva sobre o pipeline existente.

## Files to modify

- **`Services/Ndvi/CdseStatisticalService.cs`**
  - `record NdviStat`: +`NdreMean/Min/Max/Stdev`, `NdmiMean/Min/Max/Stdev` como `init` props default 0 (ctor posicional do NDVI intacto → testes e call-sites atuais seguem compilando).
  - Evalscript: **dois** — o atual (4 bandas, só ndvi) e um estendido (6 bandas, 3 saídas). Ou um único parametrizado; preferir dois `const` explícitos e escolher por flag (mais legível, testável por igualdade de string).
  - `BuildRequestBody(geometry, from, to, bool extraIndices)`: escolhe evalscript; `calculations` só com `ndvi` (histograma), independente da flag.
  - `Parse`: lê ndvi (como hoje) + ndre/ndmi via `TryGetStats(interval, "ndre")` etc.; ausência → 0.
  - `TryGetStats(JsonElement, string outputId)`: hoje `"ndvi"` fixo → parâmetro. `ParseHistogram` continua só ndvi.
  - `ICdseStatisticalService.GetStatisticsAsync(..., bool extraIndices, ct)`.
- **`Models/Entities/NdviReading.cs`** — 8 campos `double` default 0, backward-compatible (doc legado desserializa com 0).
- **`Models/Entities/PlatformAiSettings.cs`** — `bool ExtraIndicesEnabled = false` com XML-doc de kill-switch, ao lado de `Sentinel2Enabled`.
- **`Domain/Commands/Requests/Admin/UpdatePlatformAiSettingsRequest.cs`** + **`Responses/Admin/AdminAiSettingsResponse.cs`** + **`Handlers/Admin/GetPlatformAiSettingsHandler.cs`** + **`UpdatePlatformAiSettingsHandler.cs`** — propagar o toggle (grep confirmou serem os 4 pontos de `Sentinel2Enabled`).
- **`Services/Ndvi/NdviFetchService.cs`** — passar `settings.ExtraIndicesEnabled` ao serviço; gravar `NdreMean = s.NdreMean` etc. no `NdviReading`.
- **`Domain/Commands/Responses/Ndvi/NdviTrendResponse.cs`** — `NdviTrendPoint` +6 campos (min/max não são usados no gráfico; expor só mean dos três + manter ndviMin/max já existentes). *Decisão:* expor `NdreMean`/`NdmiMean` (o gráfico usa mean); min/max ficam para fase futura.
- **`Domain/Handlers/Ndvi/GetNdviTrendHandler.cs`** — mapear os dois means novos.
- **UI `area-detail.component.ts` / `.html`** — seletor de índice; `buildChart` lê o índice selecionado; NDRE/NDMI só habilitados se algum ponto tiver valor não-zero (legado tem 0 e é indistinguível — esconder é mais honesto que mostrar linha reta falsa).
- **UI `models/monitored-area.model.ts`** — `ndreMean?`, `ndmiMean?` em `NdviTrendPoint`.
- **UI admin/ia** — checkbox `ExtraIndicesEnabled` (mesmo padrão do toggle Sentinel-2).
- **`CLAUDE.md`** — dívida de nome de `ndvi_readings` guardando 3 índices; fator PU 2,0 com a flag; NDMI≠NDWI.

## MongoDB changes

- **Coleção nova:** nenhuma.
- **Índice novo:** nenhum.
- **Campos novos:** `NdviReading` +8 doubles; `PlatformAiSettings` +1 bool. Todos aditivos, default 0/false, sem migração (mesma disciplina de `ClassCounts`).

## Tenant isolation plan

Nenhuma query nova. `GetNdviTrendHandler` já filtra `a.Id == request.AreaId && a.UserId == userId` e as readings por `UserId` — **inalterado**. `NdviFetchService` é serviço puro, tenant vem do documento da área. Admin AI settings é global (sem tenant, como hoje). **Sem novo vetor cross-tenant.**

## Riscos & decisões (precisam de OK antes de codar)

1. **DECIDIDO — histograma para os três.** `calculations` pede ndvi+ndre+ndmi com o mesmo spec uniforme. `ParseHistogram` agrega **só o do ndvi** em `ClassCounts` (as faixas de NDRE/NDMI não existem ainda); os histogramas de ndre/ndmi voltam na resposta e são ignorados por ora — custo de PU idêntico, e o request já fica pronto para quando as faixas forem definidas.
2. **DECIDIDO — custo manual + documentado.** `NdviCostCents` segue proxy congelado por leitura; ligar `ExtraIndicesEnabled` não sobe o gasto sozinho. O admin sobe o `NdviCostCents` ao ligar a flag, como já calibra o resto. O critério "~1,5×" vira nota de calibração no `CLAUDE.md`, não automação.
3. **Risco que estourou hoje.** Evalscript multi-saída + `calculations` é formato novo contra a CDSE. Green tests não provam nada — só validam o JSON que produzimos. **Gate obrigatório:** antes de considerar pronto, dump do corpo real do `BuildRequestBody(extraIndices=true)` e POST à CDSE de produção, exigindo 200 e as três saídas presentes. Mesmo procedimento que pegou os dois bugs de histograma hoje.
4. **Eixo Y do gráfico.** NDVI é 0–1 (fixo hoje). NDMI pode ser negativo. Ao trocar de índice, soltar o clamp (min/max auto) para NDRE/NDMI. Detalhe de UI, sem risco de dado.

## DI registration

Nenhum serviço ou handler novo. `CdseStatisticalService` já registrado em `ApiConfig.cs`; muda só a assinatura de um método.

## Verification

1. `dotnet build StarkAgroAPI/StarkAgroAPI.csproj` (matar API no Windows antes).
2. `node --check` no evalscript estendido gerado (dump via teste temporário), **antes** de qualquer PU.
3. `dotnet test StarkAgroAPI.Tests` — verdes, ≥90% nos arquivos tocados. Novos testes:
   - `BuildRequestBody(extraIndices:false)` = request de hoje (4 bandas, só ndvi output) — asserção de string.
   - `BuildRequestBody(extraIndices:true)` declara ndre/ndmi/dataMask e 6 bandas.
   - `Parse` de resposta com 3 saídas devolve os três means; resposta legada (só ndvi) → ndre/ndmi = 0 sem quebrar.
   - `NdviFetchService` grava os campos novos quando a flag está on; grava 0 quando off.
   - `GetNdviTrendHandler` projeta os dois means novos.
4. **Gate CDSE real** (custa PU mínima): POST do corpo real gerado, exigir HTTP 200 + `outputs.ndre`, `outputs.ndmi`, `outputs.ndvi` presentes. Sem isso, a feature **não** é considerada pronta.
5. `cd StarkAgroUI && npm run start` → `/areas/:id`: seletor troca a série; NDRE/NDMI escondidos numa área só-legado.
6. `/admin/ia`: toggle ExtraIndices persiste; após um refetch com flag on, conferir os campos novos no Mongo.
