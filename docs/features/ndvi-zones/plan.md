Issue: https://github.com/cclautert/StarkAgro/issues/36

# F4 — Zoneamento de adubação (GeoTIFF) + perfis de NPK

## Context

Torna o mapa de classes de biomassa (#31) **acionável pelo maquinário**: exporta um GeoTIFF de
zonas (classe por pixel) que trator/pulverizador lê como mapa de prescrição, e permite ao admin
cadastrar a dose de NPK por classe e por cultura. `NdviClassification` já É a definição de zonas —
falta materializá-la em raster e ligá-la a uma recomendação.

## Validação de risco (feita ANTES do plano, contra a CDSE real)

A Process API com `format: image/tiff` + `sampleType: UINT8` devolve um **GeoTIFF de 8 bits, 1 banda,
com georreferenciamento** (`GeoKeyDirectory`, `ModelTiepoint`, `ModelPixelScale`, `GeoAsciiParams`).
HTTP 200 com o anel real da área 1. O critério "abre em QGIS com georreferenciamento" está
de-riscado. O gate real (a etapa que faltou no bug do histograma) fica travado em teste + repetido
no fim contra a CDSE.

## Acceptance Criteria → decisão

| Critério | Como |
|---|---|
| GeoTIFF 8 bits, classes corretas, georreferenciado | evalscript `UINT8, bands:1`, `classIndex(ndvi)` **gerado** de `NdviClassification` (mesmos cortes do PNG/legenda); `format: image/tiff`. Nodata 255 |
| Geração só no download, nunca no fetch | `NdviZoneService.GetOrCreateTiffAsync` gera na 1ª requisição do endpoint e **cacheia** em `ZoneImageFileId`; o worker de fetch não toca nisso |
| 2º usuário → 404 em área alheia | `GetNdviZonesHandler` copia a **dupla checagem de posse** do `GetNdviOverlayImageHandler` |
| Perfil de adubação por cultura editável no admin, sem deploy | coleção `fertilization_profiles` + CRUD admin, padrão de `diagnosis_plans`. **Nasce vazio — nenhuma dose inventada** |
| Testes verdes | 1121 API + 107 worker atuais + novos |

## Parte A — Export GeoTIFF de zonas

### Files to create
- `Domain/Commands/Requests/Ndvi/GetNdviZonesRequest.cs` — `{ AreaId, ReadingId }`.
- `Domain/Handlers/Ndvi/GetNdviZonesHandler.cs` — dupla checagem de posse → `INdviZoneService`; null → 404.
- `Services/Ndvi/NdviZoneService.cs` (+`INdviZoneService`) — cache-check (`ZoneImageFileId`) → gera (settings kill-switch `Sentinel2Enabled` + token + `CdseProcessService` + `INdviOverlayStore`) → grava fileId no reading → devolve bytes.

### Files to modify
- `Services/Ndvi/NdviClassification.cs` — `BuildClassIndexFunction()`: JS `function classIndex(ndvi){ if(ndvi<0.20)return 0; … return 5; }`, dos mesmos cortes (zonas ≡ classes ≡ legenda).
- `Services/Ndvi/CdseProcessService.cs` — `ZonesEvalscript` (gerado, UINT8/1 banda/nodata 255) + `GetNdviZonesTiffAsync(token, geometry, from, to, ct)` → `byte[]?`. Reusa `ComputeBbox`/`ResolveDimensions`; corpo com `output.responses.format = image/tiff` e `sampleType: UINT8`.
- `Models/Entities/NdviReading.cs` — `ObjectId? ZoneImageFileId` (cache do TIFF; null até o 1º download).
- `Controllers/NdviController.cs` — `GET /v1/areas/{id}/zones/{readingId}` → `File(bytes, "image/tiff", "zonas-{id}-{readingId}.tiff")`; null → 404. Cache-Control privado.
- `Configuration/ApiConfig.cs` — `AddScoped<INdviZoneService, NdviZoneService>`.
- UI `area-detail` — botão "Baixar mapa de zonas (GeoTIFF)" que baixa o blob (interceptor injeta o Bearer, como o overlay).

### Custo
Cada geração é **1 chamada Process API = PU**, mas só na 1ª vez por reading (cache). Sem
double-charge no dashboard — o custo é on-demand e único. Nota no `CLAUDE.md`.

## Parte B — Perfis de adubação (NPK por classe por cultura)

Admin-managed, **não** por-tenant (como `diagnosis_plans`): CRUD em `AdminController`, sem filtro
de `_currentUser`. **Nasce vazio; nenhuma dose pré-preenchida** — agronomia é parâmetro.

### Files to create
- `Models/Entities/FertilizationProfile.cs` — `{ int Id; string Culture; List<ZoneDose> Doses; DateTime CreatedAt/UpdatedAt }`; `ZoneDose { string ClassKey; double NitrogenKgHa; double PhosphorusKgHa; double PotassiumKgHa }`. `ClassKey` casa com `NdviClassification.Classes[].Key`.
- `Domain/Commands/Requests/Admin/{Get,Create,Update,Delete}FertilizationProfileRequest.cs`.
- `Domain/Commands/Responses/Admin/FertilizationProfileResponse.cs`.
- `Domain/Handlers/Admin/FertilizationProfileHandlers.cs` — List/Create/Update/Delete, `GetNextIdAsync`, erros via `INotifier`.
- UI: `components/admin/fertilization/*` (lista + form) + `AdminService` métodos + rota `/admin/adubacao` (`AdminGuard`).

### Files to modify
- `Models/agpDBContext.cs` — `FertilizationProfiles` coleção `fertilization_profiles` + índice `{Culture}`.
- `Controllers/AdminController.cs` — 4 endpoints `v1/admin/fertilization-profiles` (espelho de `diagnosis-plans`).
- UI `app.routes.ts` + menu admin.

## MongoDB changes

- **Coleção nova:** `fertilization_profiles`.
- **Índice novo:** `{Culture}` (listagem/lookup).
- **Campos novos:** `NdviReading.ZoneImageFileId` (aditivo, null).

## Tenant isolation plan

- **Zonas:** `GetNdviZonesHandler` filtra `area.UserId == userId` E `reading.UserId == userId` (dupla checagem, verbatim do overlay). Área/reading alheios → null → 404.
- **Perfis de adubação:** dado **global de plataforma** (como planos/revendas), CRUD só via `AdminController` com policy de admin — sem `_currentUser`, por design. Não é dado por-tenant.

## Riscos & decisões

1. **Georreferenciamento** — validado (tags de geo presentes). Gate real repetido no fim.
2. **Kill-switch no download** — se `Sentinel2Enabled` estiver off, gerar zona falharia. `NdviZoneService` respeita o mesmo kill-switch do fetch → null → 404 com log claro (não erro 500).
3. **Reading sem passagem válida** (nublada/legada) — sem imagem no bucket e a geração pediria um TIFF vazio. O endpoint só faz sentido para reading com `OverlayImageFileId` (passagem com pixel válido); o handler exige isso, senão 404.
4. **Doses inventadas** — proibido. O CRUD entrega a estrutura; o conteúdo é do admin/agrônomo. A recomendação por zona (juntar TIFF + perfil) fica para quando houver perfis cadastrados — **não** faço a fusão automática nesta entrega sem dose real.

## DI registration

`ApiConfig.cs`: `AddScoped<INdviZoneService, NdviZoneService>`. Handlers de adubação e zonas são
descobertos pelo assembly scan do MediatR. Sem serviço no worker (zonas são on-demand via API).

## Verification

1. `dotnet build` API (+ worker por segurança).
2. `dotnet test` — verdes, ≥90% nos arquivos novos. Testes:
   - `NdviClassification.BuildClassIndexFunction` emite um corte por classe (menos a última); `node --check` no evalscript de zonas.
   - `CdseProcessService.BuildZonesRequestBody` pede `image/tiff` + `UINT8` + 1 banda.
   - `NdviZoneService`: cache-hit serve do store sem gerar; cache-miss gera+grava+seta fileId; kill-switch off → null.
   - `GetNdviZonesHandler`: dupla checagem (2º usuário → null); reading sem overlay → null.
   - `FertilizationProfileHandlers`: CRUD happy-path, GetNextIdAsync no create, validação.
3. **Gate CDSE real** (custa PU mínima): POST do corpo de zonas gerado pelo C# → 200 + TIFF com tags de geo (repete a validação de risco com o body de produção).
4. `npm run start` → botão de download em `/areas/:id` baixa um `.tiff` que abre no QGIS; `/admin/adubacao` cria/edita perfil.
