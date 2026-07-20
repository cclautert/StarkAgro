# Implementation Plan: Web push multi-dispositivo por usuário
Issue: (sem issue — decisão em sessão 2026-07-02)
Generated: 2026-07-02

## Context
`User.WebPushSubscriptionJson` guarda **uma** inscrição — cada dispositivo que se inscreve sobrescreve o anterior (o iPhone do usuário foi sobrescrito pelo Chrome desktop, já que o `initialize()` renova a inscrição silenciosamente em todo navegador com permissão concedida). Objetivo: lista de inscrições por usuário, envio para todas, limpeza automática de endpoints mortos.

## Acceptance Criteria
1. `User.WebPushSubscriptions: List<string>` (JSON bruto de cada inscrição). Campo legado `WebPushSubscriptionJson` migrado on-write: ao registrar nova inscrição, se o legado existir, entra na lista e o campo é zerado; no envio, o legado ainda é considerado (usuários que nunca re-registrarem).
2. `RegisterWebPushSubscriptionHandler`: deduplica por `endpoint` (substitui entrada do mesmo endpoint), adiciona a nova, mantém no máximo **5** (mais recentes).
3. `WebPushNotificationService.SendAsync`: envia para **todas** as inscrições (lista + legado); em `WebPushException` com HTTP **404/410 (Gone)**, remove a inscrição morta do documento do usuário; demais erros: log e segue para as próximas.
4. UI inalterado (mesmo `PUT user/webPushSubscription`, mesmo payload).
5. Expo push inalterado.

## Affected Layers
entities / handlers / services (PushNotifications) — sem novas rotas, sem mudanças de UI, sem mudanças em agpDBContext.

## Files to Modify
| Path | Change |
|---|---|
| `StarkAgroAPI/Models/Entities/User.cs` | + `public List<string> WebPushSubscriptions { get; set; } = new();` |
| `StarkAgroAPI/Domain/Handlers/Users/RegisterWebPushSubscriptionHandler.cs` | Dedup por endpoint + append + migração do legado + cap 5 |
| `StarkAgroAPI/Services/PushNotifications/WebPushNotificationService.cs` | Loop sobre inscrições; remoção de endpoints 404/410 via `ReplaceOneAsync`; helper de parse já existe (`WebPushSubscriptionDto`) |
| `StarkAgroAPI.Tests/Domain/Handlers/Users/RegisterWebPushSubscriptionHandlerTests.cs` | Novos cenários (dedup, migração, cap) |

## MongoDB Changes
Campo novo `WebPushSubscriptions` (array de string) em `users` — default vazio, sem migração/índice.

## Tenant Isolation Plan
Inalterado — handler continua operando no usuário resolvido pelo controller (`GetCurrentUserId()`), serviço de envio recebe `userId` interno.

## Risks & Flags
- [WARNING] `WebPushNotificationService` é `[ExcludeFromCodeCoverage]` (depende do `WebPushClient` concreto) — a lógica de envio múltiplo/remoção fica coberta por revisão manual; a lógica de registro (handler) é testada.
- [INFO] Cap de 5 inscrições evita crescimento sem limite do documento do usuário.

## Verification
```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj
```
Pós-deploy: ativar notificações no iPhone e abrir o site no Chrome desktop; conferir no MongoDB que `WebPushSubscriptions` tem 2 entradas (endpoints `web.push.apple.com` + `fcm.googleapis.com`); disparar alerta de teste (subir `LimiteInferior` de um pivô) e receber o push em ambos os dispositivos.
