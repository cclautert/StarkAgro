Issue: https://github.com/cclautert/StarkAgro/issues/5

# Revenda F4 — Faturamento agregado da revenda

## Context

Fecha o épico de Revendas no backend: a fatura mensal da revenda ("revenda paga tudo"). A revenda segura **um** plano; a fatura é a mensalidade + o excedente sobre o consumo **agregado** dos clientes dela. Reusa `IDiagnosisBillingService.GetProducerInvoiceAsync` (que devolve o consumo do mês mesmo para produtor sem plano) e resolve a revenda do gestor via `IRevendaMembershipService` (#4). Backend + API; UI adiada.

## Decisão de modelo de cobrança (a confirmar)

**Modelo pool (recomendado):** o `IncludedReportsPerMonth` do plano da revenda é uma **cota única** para toda a base. `UsedReports` = soma dos laudos de todos os clientes `Active` no mês; `overage = max(0, UsedReports_total − IncludedReportsPerMonth)`; `total = MonthlyPriceCents + overage × OveragePriceCents`. É a leitura coerente de "vende 1 plano para a revenda" — os produtores-membros têm `DiagnosisPlanId = null`, então não há plano por-produtor para aplicar incluídos individuais. (Alternativa per-produtor exigiria cada produtor com o plano da revenda — não é o caso.)

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| `/v1/revenda/billing` = mensalidade + excedente sobre a soma dos clientes | `RevendaBillingService.GetRevendaInvoiceAsync(revendaId)` (modelo pool); handler do gestor resolve `revendaId` via `IRevendaMembershipService` |
| Nenhum produtor-membro é cobrado individualmente | `AcceptRevendaInviteHandler` (papel `Client`) passa a **zerar `User.DiagnosisPlanId`** ao ativar — o produtor sai da cobrança individual e entra na da revenda |
| Gestor recebe 403/vazio em laudo/pivô/sensor | Já garantido: `RevendaController` é `ResellerManager` e o billing só deriva de laudos (contagem), nunca lê pivôs/sensores |
| Valores em centavos inteiros | Todo o cálculo em `int` cents, reusando `ProducerInvoice` |

## Affected layers

Novo serviço (`RevendaBillingService`), 2 handlers (gestor + admin), DTOs, `RevendaController` (+1 endpoint), `AdminController` (+1 endpoint), `AcceptRevendaInviteHandler` (zera plano), `ApiConfig` (DI). Sem entidade/coleção nova. UI adiada.

## New REST endpoints

| Método | Rota | Policy | Response |
|---|---|---|---|
| GET | `/v1/revenda/billing` | `ResellerManager` | `RevendaBillingResponse` (revenda do chamador) |
| GET | `/v1/admin/revendas/{id}/billing` | admin (`GetCurrentUserIsAdmin`) | `RevendaBillingResponse` |

## Files to create

- `StarkAgroAPI/Services/Revenda/RevendaBillingService.cs` (`IRevendaBillingService`) — `Task<RevendaInvoice> GetRevendaInvoiceAsync(int revendaId, ct)`; record `RevendaInvoice` + `RevendaInvoiceClientLine`.
- `StarkAgroAPI/Domain/Commands/Responses/Revenda/RevendaBillingResponse.cs` — `RevendaBillingResponse` + `RevendaBillingClientLine` (Id/Name/Email/UsedReports).
- `StarkAgroAPI/Domain/Commands/Requests/Revenda/GetMyRevendaBillingRequest.cs` (`IRequest<RevendaBillingResponse?>`).
- `StarkAgroAPI/Domain/Commands/Requests/Admin/GetRevendaBillingRequest.cs` (`IRequest<RevendaBillingResponse?>` com `int RevendaId`).
- `StarkAgroAPI/Domain/Handlers/Revenda/GetMyRevendaBillingHandler.cs` — resolve `revendaId` via `IRevendaMembershipService`, chama o serviço.
- `StarkAgroAPI/Domain/Handlers/Admin/GetRevendaBillingHandler.cs` — usa `request.RevendaId`, chama o serviço.
- Testes espelhando os acima.

## Files to modify

- `StarkAgroAPI/Domain/Handlers/Revenda/RevendaInviteHandlers.cs` — no accept de papel `Client`, incluir `Set(u => u.DiagnosisPlanId, null)` no update do usuário (anti-dupla-cobrança na fonte).
- `StarkAgroAPI/Controllers/RevendaController.cs` — `GET /billing`.
- `StarkAgroAPI/Controllers/AdminController.cs` — `GET revendas/{id}/billing`.
- `StarkAgroAPI/Configuration/ApiConfig.cs` — registrar `IRevendaBillingService` (scoped).

## MongoDB changes

None. Reusa `revendas`, `revenda_memberships`, `plant_diagnoses` (via `IDiagnosisBillingService`). `AcceptRevendaInviteHandler` passa a zerar `User.DiagnosisPlanId` (campo já existente).

## Tenant isolation plan

Gestor: `revendaId` vem de `IRevendaMembershipService.GetManagedRevendaIdAsync(_currentUser.UserId)` — nunca do request. Admin: endpoint sob `GetCurrentUserIsAdmin()`. O billing só conta laudos (`plant_diagnoses` via `GetProducerInvoiceAsync`), nunca lê pivôs/sensores. Clientes considerados: só membros `Active`+`Client` da revenda.

## Risks & flags

- **Modelo pool vs per-produtor** (ver acima) — confirmar com o PO. O código isola isso em `RevendaBillingService`, fácil de trocar.
- **Zerar `DiagnosisPlanId` no accept** muda comportamento de um handler da #4 — cobrir com teste; é a fonte anti-dupla-cobrança.
- **Admin reatribuir plano a membro de revenda**: fica como *hardening* opcional (guard em `AdminEditUserHandler`) — não incluído aqui para manter o escopo; o billing da revenda não usa o plano do produtor de qualquer forma.
- **Revenda sem plano**: fatura zero (mensalidade 0, excedente 0), só mostra o consumo agregado.

## DI registration

`IRevendaBillingService` → `RevendaBillingService` (scoped) em `ApiConfig`. Handlers via scan do MediatR. Reusa `IDiagnosisBillingService` e `IRevendaMembershipService` já registrados.

## Verification

- `dotnet build StarkAgro.sln` + `dotnet test StarkAgro.sln` (solution inteira — inclui o worker).
- Cobertura ≥90% nos arquivos novos.
- Fluxo: revenda com plano (mensalidade 9900, inclusos 10, excedente 500) + 2 clientes que enviaram 8 e 7 laudos → pool 15, overage 5 → `TotalCents = 9900 + 5×500 = 12400`. `GET /v1/revenda/billing` (gestor) e `GET /v1/admin/revendas/{id}/billing` batem.
