Issue: https://github.com/cclautert/StarkAgro/issues/8

# Plano — NDVI F3: overlay PNG (Process API + GridFS)

## Contexto

Camada visual do NDVI: um PNG colorizado do índice de vegetação renderizado sobre o bbox da área, para desenhar como `L.imageOverlay` no mapa. Usa a **Process API** da CDSE para renderizar; guarda o PNG no GridFS (bucket novo `ndvi_overlays`) e serve como blob protegido, exatamente como as fotos de laudo. Depende de #7 (já mergeado no stack `feat/ndvi-cdse-fetch`).

**Escopo desta entrega: backend + API.** O overlay Leaflet no Angular (`areas/:id`) fica como follow-up, coerente com a decisão de UI adiada das fases anteriores.

## Critérios de aceite → decisão de implementação

| Critério | Implementação |
|----------|---------------|
| Overlay NDVI é gerado, armazenado no GridFS | `CdseProcessService` (Process API → PNG) chamado dentro de `NdviFetchService` na passagem mais nova não-nublada; PNG sobe em `ndvi_overlays` via `INdviOverlayStore`; `OverlayImageFileId` gravado no `NdviReading`. |
| Recuperável **apenas pelo dono** (2º usuário → 403/404) | `GetNdviOverlayImageHandler` filtra `area.UserId == _currentUser.UserId` **e** casa o `readingId` com a área antes de baixar do GridFS — mesma disciplina de `GetPlantDiagnosisImageHandler`. |
| Overlay alinha ao bbox da área | Response inclui o **bbox** (minLng/minLat/maxLng/maxLat) derivado da `Geometry`, para o front posicionar o `imageOverlay`. Endpoint separado devolve o bbox por leitura (ou já vem no `NdviTrendResponse`). |
| Caminho de imagem de laudo intacto | Bucket **separado** `ndvi_overlays`; store paralelo; nenhuma alteração em `diagnosis_images` / `GridFsDiagnosisImageStore`. |

## Camadas afetadas

- **Serviços:** `Services/Ndvi/CdseProcessService.cs` (novo), `Services/Ndvi/GridFsNdviOverlayStore.cs` (novo), `Services/Ndvi/NdviFetchService.cs` (estender).
- **Handlers:** `Domain/Handlers/Ndvi/GetNdviOverlayImageHandler.cs` (novo) + request/response.
- **Controller:** `NdviController` — `GET /v1/areas/{id}/overlay/{readingId}`.
- **Infra:** `agpDBContext` (bucket `ndvi_overlays`), `ApiConfig.cs` + `StarkAgroWorker/Program.cs` (HttpClient tipado + DI dos serviços novos), `NdviReading` (já tem `OverlayImageFileId` — nada a criar).

## Novos endpoints REST

| Método | Rota | Auth | Request | Response |
|--------|------|------|---------|----------|
| GET | `/v1/areas/{id:int}/overlay/{readingId:int}` | Bearer JWT (`[Authorize]`) | rota | `File(bytes, "image/png")` do dono; senão 404 (via `CustomResponse(null)`/NotFound) |

O bbox para alinhar o overlay já é exposto por leitura no `NdviTrendResponse` (estender cada ponto com `OverlayReadingId` + `Bbox`), evitando um segundo endpoint.

## Arquivos a criar

| Caminho | Tipo |
|---------|------|
| `StarkAgroAPI/Services/Ndvi/CdseProcessService.cs` | `ICdseProcessService` + impl — typed `HttpClient`, `api/v1/process`, evalscript colorizado NDVI, retorna `byte[]?` (PNG). `BuildRequestBody`/`Evalscript` `public static` para teste (mesmo padrão do `CdseStatisticalService`). |
| `StarkAgroAPI/Models/Interfaces/INdviOverlayStore.cs` | interface (espelha `IDiagnosisImageStore`) |
| `StarkAgroAPI/Services/Ndvi/GridFsNdviOverlayStore.cs` | impl GridFS no bucket `ndvi_overlays` (cópia paralela de `GridFsDiagnosisImageStore`) |
| `StarkAgroAPI/Domain/Handlers/Ndvi/GetNdviOverlayImageHandler.cs` | handler ownership-checked + `GetNdviOverlayImageRequest`/`NdviOverlayImageResponse` |

## Arquivos a modificar

| Caminho | Mudança |
|---------|---------|
| `StarkAgroAPI/Models/agpDBContext.cs` | Novo bucket `NdviOverlays` (`IGridFSBucket`, `BucketName="ndvi_overlays"`), inicializado ao lado de `DiagnosisImages`; propriedade `virtual`. |
| `StarkAgroAPI/Services/Ndvi/NdviFetchService.cs` | Injetar `ICdseProcessService` + `INdviOverlayStore`. Após montar a leitura mais nova **não-nublada** (`ValidSampleCount > 0`), chamar Process API para o bbox, subir o PNG e setar `reading.OverlayImageFileId`. Falha do overlay é **não-fatal** (loga e segue — a série de tendência não pode quebrar por causa da imagem). Só a passagem mais recente ganha overlay (custo/PU controlado). |
| `StarkAgroAPI/Configuration/ApiConfig.cs` | Registrar `HttpClient<ICdseProcessService, CdseProcessService>` (BaseAddress `https://sh.dataspace.copernicus.eu/`, timeout 60s) + `AddScoped<INdviOverlayStore, GridFsNdviOverlayStore>()`. |
| `StarkAgroWorker/Program.cs` | Mesmos registros (o worker é quem chama `NdviFetchService`). |
| `StarkAgroAPI/Controllers/NdviController.cs` | `GET {id:int}/overlay/{readingId:int}` → devolve `File` quando o handler retorna bytes, senão `NotFound`. |
| `StarkAgroAPI/Domain/Commands/Responses/Ndvi/NdviTrendResponse.cs` | Cada ponto expõe `int? OverlayReadingId` + `double[] Bbox` (minLng,minLat,maxLng,maxLat) para o front alinhar o overlay. |

## MongoDB

- **Bucket GridFS novo:** `ndvi_overlays` (binário; nunca base64 no documento). Sem coleção nova.
- `NdviReading.OverlayImageFileId` (`ObjectId?`) **já existe** (criado em #7) — só passa a ser preenchido.
- Nenhum índice novo.

## Isolamento de tenant

- `GetNdviOverlayImageHandler`: resolve `userId = _currentUser.UserId`; carrega a **área** por `Id == request.AreaId && UserId == userId`; se nula → retorna null (404). Só então carrega o `NdviReading` por `Id == readingId && AreaId == area.Id && UserId == userId` e baixa `OverlayImageFileId`. Duas checagens de posse antes de tocar o GridFS.
- `NdviFetchService` roda no worker — tenant vem do `area.UserId` do documento (nunca de contexto de usuário), como já faz.

## Riscos & flags

- **Custo/PU:** overlay só na passagem mais recente e não-nublada, e só quando há passagem nova — não regenera a cada varredura. `Sentinel2Enabled` continua sendo o kill-switch (o fetch inteiro nem roda desligado).
- **Overlay não-fatal:** um erro na Process API **não** pode falhar o fetch (a tendência é o dado primário). Se o PNG falhar, `OverlayImageFileId` fica null e o front simplesmente não desenha overlay.
- **Alinhamento:** o PNG é renderizado no **bbox** do polígono (não no polígono recortado); o `L.imageOverlay` usa esse mesmo bbox → alinhamento exato. Bbox derivado de `Geometry.Coordinates.Exterior.Positions` (ordem `[lng,lat]`).
- **Validação ao vivo:** como no #7, só o parsing/serialização é testável offline; o evalscript e o formato PNG precisam de validação com credenciais reais (flag para o PO, igual ao #7).
- **Bucket separado:** garante o critério "diagnosis_images intacto".

## DI

- `ICdseProcessService` → `CdseProcessService` (typed `HttpClient`, scoped por convenção do factory).
- `INdviOverlayStore` → `GridFsNdviOverlayStore` (`AddScoped`).
- Registrados **em ambos** `ApiConfig.cs` (API serve o overlay) e `StarkAgroWorker/Program.cs` (worker gera o overlay).

## Verificação

- `dotnet build StarkAgro.sln` (solução inteira — lição do #2: build só do projeto da API não pega o worker).
- `dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj` e `dotnet test StarkAgroWorker.Tests/...`.
- Cobertura ≥ 90% nos arquivos novos: `CdseProcessService` (Build/Evalscript + caminho HTTP OK/erro/throws como no `CdseStatisticalServiceTests`), `GridFsNdviOverlayStore`, `GetNdviOverlayImageHandler` (dono OK, 2º usuário → null, reading de outra área → null), `NdviFetchService` (overlay setado no sucesso; falha do overlay é não-fatal).
- HTTP manual (pós-credenciais): `GET /v1/areas/{id}/overlay/{readingId}` como dono → 200 image/png; como 2º usuário → 404.

## Branch / PR

- Branch `feat/ndvi-overlay` empilhada em `feat/ndvi-cdse-fetch` (base do PR #21).
- Segue o padrão de PR stack das fases anteriores.
