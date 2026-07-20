Issue: https://github.com/cclautert/StarkAgro/issues/2

# Revenda F1 — Refactor de papéis para `Roles: List<string>`

## Context

Pré-requisito da feature Revendas. Hoje os papéis são dois booleans (`User.IsAdmin`, `User.IsAgronomist`) e o próprio `User.cs` registra a dívida de colapsá-los em `Roles: List<string>` quando surgir o 3º papel (o gestor de revenda). Este refactor **troca o backing store preservando toda a superfície externa** — claims do JWT, DTOs do admin e chaves de `localStorage` na UI ficam idênticos, então a UI **não muda** nesta fase.

## Estratégia (blast radius mínimo)

`Roles` passa a ser o campo persistido. `IsAdmin`/`IsAgronomist`/`IsResellerManager` viram **propriedades computadas `[BsonIgnore]`** sobre `Roles` na entidade `User`. Assim, todo call site que **lê** `user.IsAdmin` continua compilando e funcionando; só os poucos pontos que **escrevem** o papel mudam (seed + 2 handlers admin). As claims `isAdmin`/`isAgronomist` continuam sendo emitidas (derivadas), então guards, `layout` e `admin-user-form` da UI não mudam.

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| Login de admin e agrônomo existentes continua funcionando sem re-cadastro | JWT continua emitindo `isAdmin`/`isAgronomist` (derivadas de `Roles`); claims inalteradas |
| Migração idempotente preenche `Roles` dos usuários já gravados | `MigrateUserRolesAsync` no boot: para docs com `Roles` ausente/vazio, lê os campos legados `IsAdmin`/`IsAgronomist` via `BsonDocument` e faz `$set Roles` (+`$unset` dos legados). Rodar 2× é no-op |
| Nenhum handler existente quebrou | `IsAdmin`/`IsAgronomist` computados mantêm a superfície de leitura; escrita trocada por `SetRole` |
| `dotnet test` verde incl. JwtTokenService, CurrentUserContext, guards | Atualizar testes que constroem `new User { IsAdmin = ... }` (setter não existe mais) para `SetRole`/`Roles` |

## Affected layers

Entidade `User`, auth (`JwtTokenService`, `CurrentUserContext` + interface), policies e seeding (`ApiConfig`), handlers admin (create/edit). **UI: nenhuma mudança** (contratos preservados). Sem worker, sem novo endpoint.

## New REST endpoints

None.

## Files to create

- `StarkAgroAPI/Models/Entities/UserRole.cs` — classe estática de constantes (`Admin`, `Agronomist`, `ResellerManager`), no espírito de `AgronomistClientStatus`.

## Files to modify

- `StarkAgroAPI/Models/Entities/User.cs` — remover as auto-props `bool IsAdmin`/`bool IsAgronomist`; adicionar `public List<string> Roles { get; set; } = new();`. Adicionar computados `[BsonIgnore] public bool IsAdmin => Roles.Contains(UserRole.Admin);` (idem `IsAgronomist`, `IsResellerManager`) e helper `public void SetRole(string role, bool enabled)` (add/remove sem duplicar).
- `StarkAgroAPI/Services/JwtTokenService.cs` — emitir as claims `isAdmin`/`isAgronomist`/`isResellerManager` (derivadas de `user.IsAdmin` etc., que agora computam sobre `Roles`) + uma claim `role` por item de `Roles` (para futuro `RequireRole`). Nada mais muda.
- `StarkAgroAPI/Models/Interfaces/ICurrentUserContext.cs` — adicionar `bool IsResellerManager { get; }` e `bool HasRole(string role);`.
- `StarkAgroAPI/Services/CurrentUserContext.cs` — adicionar getter cacheado `IsResellerManager` (lê claim `isResellerManager`, espelha o bloco `IsAgronomist`) e `HasRole` (lê claims `role`). `IsAdmin`/`IsAgronomist` inalterados.
- `StarkAgroAPI/Configuration/ApiConfig.cs` — (1) política `"ResellerManager"` = `RequireClaim("isResellerManager","true")` ao lado de `"Agronomist"`; (2) seed: `IsAdmin = true` → `Roles = new() { UserRole.Admin }`, e `Update.Set(u => u.IsAdmin, true)` → `Update.AddToSet(u => u.Roles, UserRole.Admin)`; (3) novo `MigrateUserRolesAsync` disparado junto dos outros seeds (`Task.Run`).
- `StarkAgroAPI/Domain/Handlers/Admin/AdminCreateUserHandler.cs` — `IsAdmin = request.IsAdmin` → `SetRole(UserRole.Admin, request.IsAdmin)` (resposta mapeia `user.IsAdmin` computado).
- `StarkAgroAPI/Domain/Handlers/Admin/AdminEditUserHandler.cs` — as duas atribuições `user.IsAdmin/IsAgronomist = ...` → `SetRole(...)`.

DTOs (`AdminUserResponse`, `AdminCreateUserRequest`, `AdminEditUserRequest`) **não mudam** — continuam expondo os booleans, agora mapeados de/para `Roles` nos handlers.

## MongoDB changes

Sem coleção nova. O campo persistido do `User` deixa de ser `IsAdmin`/`IsAgronomist` e passa a ser `Roles` (array de string). Migração de dados idempotente no boot (`MigrateUserRolesAsync`) converte os documentos existentes. `Entity` tem `[BsonIgnoreExtraElements]`, então docs antigos com os campos legados não quebram a desserialização durante a transição.

## Tenant isolation plan

Sem impacto — o refactor não toca em `UserId` nem em queries de dados de tenant. Papéis são atributos do próprio usuário autenticado; nenhuma query nova por dado de terceiro.

## Risks & flags

- **Compilação dos testes**: testes que fazem `new User { IsAdmin = true }` deixam de compilar (prop vira get-only). Atualizar para `SetRole`/`Roles` — não deletar testes (regra da skill).
- **Ordem de serialização Mongo**: o driver usa PascalCase (sem convenção camelCase registrada para o Mongo), então os campos legados no banco são `IsAdmin`/`IsAgronomist` — a migração lê exatamente esses nomes.
- **`[BsonIgnore]` obrigatório** nos computados, senão o driver tenta serializá-los.
- **Migração antes do uso**: `MigrateUserRolesAsync` roda no boot junto do seed; ambos idempotentes e independentes.

## DI registration

Nenhum serviço novo. `CurrentUserContext`/`ICurrentUserContext` já registrados (scoped). Política `"ResellerManager"` adicionada ao `AddAuthorization`.

## Verification

- `dotnet build StarkAgroAPI/StarkAgroAPI.csproj`
- `dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj` (verde; cobertura ≥90% nos arquivos tocados via `--collect:"XPlat Code Coverage"`)
- Manual: subir a API com um banco que tenha usuários gravados no formato antigo → conferir que `Roles` é preenchido no boot e que login de admin/agrônomo continua roteando (claim presente no token).
