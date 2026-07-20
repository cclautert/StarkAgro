# Plano — Telas Angular da Revenda (admin + gestor + membro)

Feature: UI da Revenda (fecha o que foi adiado nas issues #3/#4/#5). Sem issue própria — deriva das três.

## Contexto

O backend da Revenda está completo (#2–#5, PRs #12–#15). Falta a UI Angular: cadastro admin de revendas, portal do gestor (membros + faturamento) e o aceite do lado do membro. Espelha `admin-plans` (CRUD), `agronomist-clients` (convite/revogação) e a tela de faturamento do agrônomo.

## Pré-requisito descoberto

O login (`login.component.ts` L65-66 e `auth-callback.component.ts`) só grava `isAdmin`/`isAgronomist` no `localStorage`. O JWT já emite `isResellerManager` (desde #2), mas a UI não persiste. **Precisa gravar `isResellerManager`** para o `ResellerGuard` e o menu funcionarem.

## Arquivos a criar

- `models/revenda.model.ts` — `Revenda`, `RevendaMember`, `RevendaInvite`, `RevendaBilling` + `RevendaBillingClientLine` (camelCase, espelhando os DTOs).
- `services/revenda.service.ts` (`RevendaService`) — gestor: `getMyRevenda`, `getMembers`, `invite(email, role)`, `revokeMember(linkId)`, `getBilling`; membro: `getMyInvites`, `acceptInvite(id)`, `declineInvite(id)`.
- `guards/reseller.guard.ts` — lê `localStorage['isResellerManager'] === 'true'` (espelha `agronomist.guard.ts`).
- **Admin** `components/admin/revendas/admin-revendas.component.*` — lista + form inline (espelha `admin-plans`), com **atribuir gestor** (email/userId) e **ver faturamento** da revenda. Reusa `AdminService` estendido.
- **Gestor** `components/revenda/membros/revenda-membros.component.*` — lista membros (Active+Pending) + convidar (email + papel Agronomist/Client) + revogar (espelha `agronomist-clients`).
- **Gestor** `components/revenda/faturamento/revenda-faturamento.component.*` — fatura pool (espelha a billing do agrônomo): total + linhas por cliente + badge do plano.
- **Membro** `components/revenda/convites/revenda-convites.component.*` — lista convites pendentes + aceitar/recusar (espelha o lado do produtor).

## Arquivos a modificar

- `services/admin.service.ts` — `getRevendas`, `createRevenda`, `updateRevenda`, `assignRevendaManager(id, userId)`, `getRevendaBilling(id)`.
- `login.component.ts` + `login/auth-callback.component.ts` — gravar `localStorage['isResellerManager']` do token (e `removeItem` no logout em `layout.component.ts`).
- `layout.component.ts` (+ `.html`) — flag `isResellerManager` + itens de menu (revenda/membros, revenda/faturamento; admin/revendas sob o admin).
- `app.routes.ts` — rotas: `admin/revendas` (`[AuthGuard, AdminGuard]`); `revenda/membros`, `revenda/faturamento`, `revenda/convites` (`[AuthGuard, ResellerGuard]`).

## MongoDB / API

Nenhuma mudança de backend — só consome os endpoints de #3/#4/#5 (URLs relativas `/api/v1/...`).

## Verificação

- `npm run build` (prod) sem erro — é o que o job "Frontend (Angular)" do CI roda, além de `ng test`.
- `npx ng test --watch=false --browsers=ChromeHeadless` verde (adicionar `.spec.ts` mínimo para o guard e componentes novos, no padrão dos specs existentes).
- Manual: admin cria revenda + atribui gestor; gestor loga (menu Revenda aparece) → convida cliente → cliente vê em convites e aceita → gestor vê o membro e o faturamento agregado.

## Decisão de escopo (a confirmar)

A UII completa são ~8 arquivos novos + 5 modificados. Sugiro **um PR único** (a feature de UI é coesa e os contratos já estão prontos). Alternativa: fatiar em (A) gestor + guard + login-wiring, (B) admin CRUD, (C) aceite do membro.
