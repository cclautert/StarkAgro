Issue: https://github.com/cclautert/StarkAgro/issues/9

# Plano — NDVI F4: governança de custo/PU + tetos

## Contexto

Visibilidade e teto de custo do NDVI. A CDSE cobra Processing Units (PU) com cota mensal; sem freio, um refetch descontrolado estoura o orçamento. Espelha o painel de custo de IA por laudo (`DiagnosisCostService` + `/admin/ia`): soma o custo já congelado por `NdviReading.NdviCostCents` no mês, mostra no admin e para de enfileirar quando o teto é atingido. Depende de #8 (já no stack).

**Escopo: backend + API.** O painel admin no Angular (`/admin/ia`) já consome `AdminAiSettingsResponse` — o novo campo aparece automaticamente; nenhum trabalho de UI novo é obrigatório aqui (o card visual dedicado fica como polimento de follow-up, como nas fases anteriores).

## Critérios de aceite → decisão de implementação

| Critério | Implementação |
|----------|---------------|
| `/admin/ia` mostra o custo NDVI do mês (centavos inteiros) | `NdviCostService.GetCurrentMonthCostCentsAsync` soma `NdviReading.NdviCostCents` no mês; `GetPlatformAiSettingsHandler` injeta e devolve em `AdminAiSettingsResponse.CurrentMonthNdviCostCents`. |
| Ao atingir o teto, novos fetches param e o represamento é **logado** | Setting `NdviMonthlyBudgetCents` (0 = ilimitado). `NdviProcessor.RunAsync`, após o kill-switch, soma o custo do mês; se `budget > 0 && custo >= budget`, **não reivindica nada** e `LogWarning` do represamento (nunca trunca em silêncio). |
| Kill-switch `Sentinel2Enabled` continua cortando tudo | Inalterado — a checagem do budget vem **depois** da do kill-switch. |
| (Opcional PO) teto de áreas por usuário | Setting `NdviMaxAreasPerUser` (0 = ilimitado). `CreateMonitoredAreaHandler` conta as áreas do usuário; se `cap > 0 && count >= cap`, `_notifier.Handle(...)` + null. **Default 0 → sem mudança de comportamento** até o admin optar. |

## Camadas afetadas

- **Serviço novo:** `Services/Ndvi/NdviCostService.cs` (`INdviCostService`), espelho puro de `DiagnosisCostService`.
- **Entidade config:** `PlatformAiSettings` — 2 campos de teto.
- **Admin CQRS:** `AdminAiSettingsResponse` (+3 campos), `UpdatePlatformAiSettingsRequest` (+2 tetos), `GetPlatformAiSettingsHandler` (popula), `UpdatePlatformAiSettingsHandler` (persiste).
- **Worker:** `NdviProcessor` — gate de budget no `RunAsync`.
- **Handler de área:** `CreateMonitoredAreaHandler` — enforce do teto por usuário.
- **DI:** `ApiConfig.cs` + `StarkAgroWorker/Program.cs` — registrar `INdviCostService`.

## Novos endpoints REST

Nenhum. Reusa `GET/PUT` de `/admin/ia` (o painel de settings já existente).

## Arquivos a criar

| Caminho | Tipo |
|---------|------|
| `StarkAgroAPI/Services/Ndvi/NdviCostService.cs` | `INdviCostService` + impl — soma `NdviReading.NdviCostCents` do mês corrente, platform-wide. |

## Arquivos a modificar

| Caminho | Mudança |
|---------|---------|
| `StarkAgroAPI/Models/Entities/PlatformAiSettings.cs` | `int NdviMonthlyBudgetCents = 0` (0 = ilimitado) + `int NdviMaxAreasPerUser = 0` (0 = ilimitado). |
| `StarkAgroAPI/Domain/Commands/Responses/Admin/AdminAiSettingsResponse.cs` | `int CurrentMonthNdviCostCents` (só leitura) + `NdviMonthlyBudgetCents` + `NdviMaxAreasPerUser`. |
| `StarkAgroAPI/Domain/Commands/Requests/Admin/UpdatePlatformAiSettingsRequest.cs` | `NdviMonthlyBudgetCents` + `NdviMaxAreasPerUser`, ambos `[Range(0, ...)]`. |
| `StarkAgroAPI/Domain/Handlers/Admin/GetPlatformAiSettingsHandler.cs` | Injetar `INdviCostService`; popular `CurrentMonthNdviCostCents` + os dois tetos (inclusive no early-return de settings null). |
| `StarkAgroAPI/Domain/Handlers/Admin/UpdatePlatformAiSettingsHandler.cs` | Persistir os dois tetos. |
| `StarkAgroWorker/Services/NdviProcessor.cs` | Após o kill-switch: `NdviCostService` soma o mês; se `NdviMonthlyBudgetCents > 0 && custo >= budget`, `LogWarning` do represamento e `return` (não claim). |
| `StarkAgroAPI/Domain/Handlers/Ndvi/MonitoredAreaHandlers.cs` | `CreateMonitoredAreaHandler`: se `NdviMaxAreasPerUser > 0` e o usuário já tem ≥ cap áreas, notifica e retorna null (antes de gerar id/inserir). |
| `StarkAgroAPI/Configuration/ApiConfig.cs` + `StarkAgroWorker/Program.cs` | `AddScoped<INdviCostService, NdviCostService>()`. |

## MongoDB

- **Sem coleção nova, sem índice novo.** Só 2 campos em `platform_ai_settings` (default 0, retrocompatível — `[BsonIgnoreExtraElements]`/default cobre documentos antigos).
- O custo do mês é uma agregação de leitura sobre `ndvi_readings` filtrada por `CreatedAt` no mês. Custo já existe congelado em `NdviCostCents` (desde #7).

## Isolamento de tenant

- `NdviCostService` é **platform-wide** por natureza (é o gasto da plataforma, visão de admin) — igual ao `DiagnosisCostService`. Só é exposto via `/admin/ia`, protegido pela policy de admin já existente.
- `CreateMonitoredAreaHandler` continua contando **só as áreas do `_currentUser.UserId`** — o teto por usuário não vaza contagem entre tenants.
- `NdviProcessor` roda no worker (sem usuário); lê settings do documento, sem contexto de usuário.

## Riscos & flags

- **Budget é o freio, não a cota:** ao bater o teto o worker **para de buscar** (custo protegido); áreas ficam `Idle` e retomam no próximo mês/ao subir o teto. É corte de custo, não erro — só `LogWarning`.
- **Custo é estimado por `NdviCostCents`**, não pelo PU real reportado pela CDSE (a Statistical/Process não devolve o PU consumido de forma trivial). É um teto conservador/proxy — flag para o PO calibrar o `NdviCostCents` com o consumo real observado no dashboard da CDSE.
- **Tetos default 0 (ilimitado):** sem decisão do PO, o comportamento não muda. O admin liga quando quiser — sem redeploy.
- **Janela de corte:** o gate roda a cada tick (1 min) e relê o custo; áreas já reivindicadas no tick corrente terminam (não aborta no meio) — o teto é de *enfileiramento*, coerente com "para de enfileirar".

## DI

- `INdviCostService` → `NdviCostService` (`AddScoped`), em `ApiConfig.cs` (o handler de admin usa) **e** no worker `Program.cs` (o `NdviProcessor` usa).

## Verificação

- `dotnet build StarkAgro.sln`.
- `dotnet test StarkAgroAPI.Tests/...` + `dotnet test StarkAgroWorker.Tests/...`.
- Cobertura ≥ 90% nos arquivos novos/tocados:
  - `NdviCostService` — soma no mês, ignora fora do mês.
  - `GetPlatformAiSettingsHandler` — devolve `CurrentMonthNdviCostCents` + tetos (settings presente e null).
  - `UpdatePlatformAiSettingsHandler` — persiste os tetos.
  - `NdviProcessor` — budget batido não reivindica (log) / budget não batido reivindica normal / budget 0 = ilimitado.
  - `CreateMonitoredAreaHandler` — cap batido → null+notifica / abaixo do cap → cria / cap 0 → cria.

## Branch / PR

- Branch `feat/ndvi-cost-governance` empilhada em `feat/ndvi-overlay` (base do PR #22).
