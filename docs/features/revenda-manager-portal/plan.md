Issue: https://github.com/cclautert/StarkAgro/issues/4

# Revenda F3 — Convite/aceite de membros (portal do gestor)

## Context

Dá ao gestor de revenda um portal para convidar e gerenciar os membros (agrônomos e clientes produtores) e o fluxo de aceite do lado do convidado — reusando o padrão de convite-token de `InviteClientHandler`/`ProducerLinkHandlers`. Depende de #3 (entidades `Revenda`/`RevendaMembership` + papel `ResellerManager`). Backend + API apenas; telas Angular ficam para follow-up (mesma decisão da #3).

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| Gestor convida agrônomo e cliente por e-mail; convidado sem conta vira `Pending` | `InviteRevendaMemberHandler` espelha `InviteClientHandler`: normaliza e-mail, resolve `MemberUserId` se já existir, token `Guid.NewGuid().ToString("N")` + 7 dias, guarda self-invite e duplicata |
| Aceite ativa a membership e seta `User.RevendaId` | `AcceptRevendaInviteHandler` espelha `AcceptAgronomistInviteHandler`: valida por id/e-mail, trata expiração, para papel `Client` revoga vínculos `Client` ativos anteriores (1 revenda por produtor), `Status=Active`, `MemberUserId`, `AcceptedAt`, denormaliza `User.RevendaId` |
| Gestor não toca membros de outra revenda | `IRevendaMembershipService.GetManagedRevendaIdAsync(caller)` resolve a revenda da **membership Manager ativa do chamador**; todo handler de gestor usa isso, nunca `request` |
| Revogação preserva histórico | `RevokeRevendaMemberHandler` seta `Status=Revoked` + `RevokedAt`/`RevokedByUserId`, não apaga |

## Affected layers

Novo serviço (`RevendaMembershipService`), handlers de gestor + de membro, DTOs, `RevendaController` (novo), `UserController` (endpoints de aceite), `User` (campo `RevendaId`), `ApiConfig` (DI + já tem a policy `ResellerManager` da #2). Sem worker. UI adiada.

## New REST endpoints

**Gestor** — `RevendaController`, `[Authorize]` + policy `"ResellerManager"`, `Route("v1/revenda")`:
| Método | Rota | Request | Response |
|---|---|---|---|
| GET | `/me` | — | `RevendaResponse` (a revenda que ele gere) |
| GET | `/members` | — | `List<RevendaMemberResponse>` (Active+Pending) |
| POST | `/members/invite` | `InviteRevendaMemberRequest` (`Email`, `Role`) | `RevendaMemberResponse` |
| DELETE | `/members/{linkId}` | — | 204 |

**Membro convidado** — no `UserController` (autenticado, sem policy especial), ao lado dos convites de agrônomo:
| Método | Rota | Response |
|---|---|---|
| GET | `/v1/user/revenda-invites` | `List<RevendaInviteResponse>` |
| POST | `/v1/user/revenda-invites/{id}/accept` | bool |
| POST | `/v1/user/revenda-invites/{id}/decline` | bool |

## Files to create

- `StarkAgroAPI/Services/Revenda/RevendaMembershipService.cs` (`IRevendaMembershipService`) — `Task<int?> GetManagedRevendaIdAsync(int userId, ct)` (revenda da membership `Manager` ativa do chamador) + helpers de listagem de membros ativos por papel conforme necessário.
- `StarkAgroAPI/Domain/Handlers/Revenda/RevendaManagerHandlers.cs` — `GetMyRevendaHandler`, `ListRevendaMembersHandler`, `InviteRevendaMemberHandler`, `RevokeRevendaMemberHandler`.
- `StarkAgroAPI/Domain/Handlers/Revenda/RevendaInviteHandlers.cs` — `GetMyRevendaInvitesHandler`, `AcceptRevendaInviteHandler`, `DeclineRevendaInviteHandler`.
- `StarkAgroAPI/Domain/Commands/Requests/Revenda/*.cs` — requests acima (com DataAnnotations; `Role` restrito a `Agronomist`/`Client`).
- `StarkAgroAPI/Domain/Commands/Responses/Revenda/*.cs` — `RevendaMemberResponse`, `RevendaInviteResponse`.
- `StarkAgroAPI/Controllers/RevendaController.cs`.
- Testes espelhando todos os handlers.

## Files to modify

- `StarkAgroAPI/Models/Entities/User.cs` — `public int? RevendaId { get; set; }` (cache denormalizado do vínculo ativo; fonte da verdade continua sendo a membership).
- `StarkAgroAPI/Controllers/UserController.cs` — 3 endpoints de convite (get/accept/decline).
- `StarkAgroAPI/Configuration/ApiConfig.cs` — registrar `IRevendaMembershipService` (scoped).

## MongoDB changes

Nenhuma coleção nova (usa `revenda_memberships` da #3). Campo novo `User.RevendaId` (nullable, backward-compatible). Índices já criados na #3. IDs de membership via `GetNextIdAsync` (já usado no convite).

## Tenant isolation plan

**Ponto crítico:** todo handler de gestor resolve a `revendaId` via `IRevendaMembershipService.GetManagedRevendaIdAsync(_currentUser.UserId)` — **nunca** do request. Convidar/listar/revogar sempre filtram por essa `revendaId`. Handlers de membro filtram por `_currentUser.UserId`/e-mail. Revogar exige que o `RevendaMembership.RevendaId == revenda do chamador`.

## Risks & flags

- **1 revenda por produtor**: aceitar convite `Client` deve revogar vínculos `Client` ativos anteriores do usuário antes de ativar (o índice único parcial da #3 é o backstop sob concorrência) — espelha o accept do agrônomo.
- **Papel do convite**: `InviteRevendaMemberRequest.Role` só aceita `Agronomist`/`Client` (Manager é designado pelo admin na #3) — validar.
- **Gestor sem revenda**: `GetManagedRevendaIdAsync` retorna null → handlers notificam e retornam vazio/null.
- **Expiração**: convite expirado vira `Expired` no accept, igual ao agrônomo.
- **Convite duplicado**: guard por `(RevendaId, MemberEmail, Status in {Pending,Active})`.

## DI registration

`IRevendaMembershipService` → `RevendaMembershipService` (scoped) em `ApiConfig`. Handlers registrados pelo scan do MediatR. Policy `"ResellerManager"` já existe (#2).

## Verification

- `dotnet build StarkAgro.sln` (solution inteira — inclui o worker) + `dotnet test StarkAgro.sln`.
- Cobertura ≥90% nos arquivos novos.
- Fluxo: gestor (JWT com `isResellerManager`) `POST /v1/revenda/members/invite {email, role:"Client"}` → convidado vê em `GET /v1/user/revenda-invites` → `POST .../accept` → membership `Active`, `User.RevendaId` setado; assertar que outro gestor não lista/revoga esse membro.
