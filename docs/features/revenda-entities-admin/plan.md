Issue: https://github.com/cclautert/StarkAgro/issues/3

# Revenda F2 — Entidades `Revenda` + `RevendaMembership` + CRUD admin

## Context

Camada de dados da feature Revendas. Cria a revenda como entidade de primeira classe e o vínculo de membership (espelhando `AgronomistClient`), mais o CRUD administrativo e a designação de gestor. Depende de #2 (papel `ResellerManager` já existe em `UserRole`). Os fluxos de convite/aceite de membros ficam para a #4.

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| Admin cria/edita/lista revendas e atribui um plano | CRUD em `RevendaHandlers` + endpoints no `AdminController` (guard `GetCurrentUserIsAdmin`), espelhando o CRUD de `DiagnosisPlan`. Plano validado por existência no create/edit |
| Admin designa um gestor → usuário ganha papel `ResellerManager` + membership `Manager` ativa | `AssignRevendaManagerHandler`: valida revenda+usuário, cria membership `Manager` (`Status=Active`), `AddToSet(Roles, ResellerManager)` no usuário |
| Índice único parcial impede 2 memberships `Active`+`Client` para o mesmo `MemberUserId` | Índice parcial em `{MemberUserId}` filtrando `Status==Active AND MemberRole==Client` no `agpDBContext` |
| `dotnet test` verde p/ os novos handlers | Testes xUnit+Moq para cada handler (happy path, não-encontrado, idempotência do manager) |

## Affected layers

Entidades (`Revenda`, `RevendaMembership` + constantes), `agpDBContext` (2 coleções + índices), handlers admin, DTOs, `AdminController`. UI admin Angular (list/form) — ver nota de escopo. Sem worker.

## New REST endpoints (todos `[Authorize]` + guard admin, `Route("v1/admin")`)

| Método | Rota | Request | Response |
|---|---|---|---|
| GET | `/revendas` | — | `List<RevendaResponse>` |
| POST | `/revendas` | `CreateRevendaRequest` | `RevendaResponse` (201) |
| PUT | `/revendas/{id}` | `UpdateRevendaRequest` | `RevendaResponse` |
| POST | `/revendas/{id}/manager` | `AssignRevendaManagerRequest` (`UserId`) | `RevendaResponse` |

Sem hard-delete: revenda é desativada por `Active=false` (preserva histórico), igual ao plano.

## Files to create

- `StarkAgroAPI/Models/Entities/Revenda.cs` — `Entity`: `Name`, `string? Cnpj`, `string? ContactEmail`, `int? DiagnosisPlanId`, `int? DiagnosisQuotaPerMonth`, `bool Active = true`, `DateTime CreatedAt`, `int CreatedByAdminId`.
- `StarkAgroAPI/Models/Entities/RevendaMembership.cs` — `Entity` espelhando `AgronomistClient`: `int RevendaId`, `string MemberRole`, `int? MemberUserId`, `string MemberEmail`, `string Status`, `InviteToken`, `InvitedAt`, `InviteExpiresAt`, `AcceptedAt?`, `RevokedAt?`, `int? RevokedByUserId`, `DateTime CreatedAt`. + classes de constantes `RevendaMemberRole` (`Manager`/`Agronomist`/`Client`) e `RevendaMembershipStatus` (`Pending`/`Active`/`Declined`/`Revoked`/`Expired`).
- `StarkAgroAPI/Domain/Commands/Requests/Admin/RevendaRequests.cs` — `GetRevendasRequest`, `CreateRevendaRequest`, `UpdateRevendaRequest`, `AssignRevendaManagerRequest` (com DataAnnotations, dinheiro/cota em ranges).
- `StarkAgroAPI/Domain/Commands/Responses/Admin/RevendaResponse.cs`.
- `StarkAgroAPI/Domain/Handlers/Admin/RevendaHandlers.cs` — `RevendaMapper`, `GetRevendasHandler`, `CreateRevendaHandler`, `UpdateRevendaHandler`, `AssignRevendaManagerHandler`.
- Testes espelhando os arquivos acima em `StarkAgroAPI.Tests/`.

## Files to modify

- `StarkAgroAPI/Models/agpDBContext.cs` — registrar `Revendas`/`RevendaMemberships` (collections `revendas`/`revenda_memberships`) + índices no bloco fire-and-forget: `revenda_memberships` `{RevendaId,Status}`, `{MemberUserId,Status}`, `{InviteToken}` sparse, e **único parcial** `{MemberUserId}` com `PartialFilterExpression = And(Eq(Status,Active), Eq(MemberRole,Client))`; `revendas` `{Active}`.
- `StarkAgroAPI/Controllers/AdminController.cs` — 4 endpoints acima (mesma forma do CRUD de planos).

## MongoDB changes

Duas coleções novas: `revendas`, `revenda_memberships`. IDs sequenciais via `GetNextIdAsync(nameof(Revenda))` / `nameof(RevendaMembership)`. Índices acima. Sem mudança em entidades existentes.

## Tenant isolation plan

Revenda e membership são **dados administrados pelo admin**, não dados por-tenant do produtor — exatamente como `diagnosis_plans`. Portanto os endpoints são protegidos por `GetCurrentUserIsAdmin()` (403 caso contrário) e **não** filtram por `_currentUser.UserId`. Nenhum endpoint por-tenant é criado nesta fase; o gestor (que resolve a própria revenda) vem na #4.

## Risks & flags

- **Índice parcial com 2 condições**: `And(Eq(Status,Active), Eq(MemberRole,Client))` — validar que o driver aceita (aceita; `PartialFilterExpression` suporta `$and`).
- **Plano inexistente**: create/edit de revenda com `DiagnosisPlanId` que não existe → notificar e abortar (evita revenda apontando p/ plano fantasma).
- **Designar gestor idempotente**: se o usuário já é `Manager` ativo daquela revenda, no-op (não duplica membership; `AddToSet` no papel já é idempotente).
- **Escopo UI**: as telas Angular admin (list/form) espelham `admin-plans`. Incluídas nesta fase; se preferir manter o PR enxuto, dá para adiar a UI para um follow-up (o gate de cobertura é sobre o C#).

## DI registration

Handlers são registrados automaticamente pelo scan do MediatR (`RegisterServicesFromAssemblyContaining<agpDBContext>`). **Nenhum serviço novo** nesta fase (o `RevendaMembershipService`/`RevendaBillingService` são das fases #4/#... ). Coleções ficam no `agpDBContext` já registrado.

## Verification

- `dotnet build StarkAgroAPI/StarkAgroAPI.csproj`
- `dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj` (verde; ≥90% nos arquivos novos via `--collect:"XPlat Code Coverage"`)
- HTTP de exemplo (admin JWT): `POST /v1/admin/revendas {Name,Cnpj,ContactEmail,DiagnosisPlanId}` → 201; `POST /v1/admin/revendas/{id}/manager {UserId}` → gestor ganha papel `ResellerManager` (conferível no próximo login/JWT).
