# Laudo Fitossanitário Assistido por IA — AgripeWeb

## Context

O cunhado do usuário é **engenheiro agrônomo** e cuida da fazenda onde o AgripeWeb será testado. O objetivo não é só técnico: é criar dentro do AgripeWeb um produto que **ele possa vender casado com o software** — assessoria agronômica recorrente, não uma venda de licença.

O gancho: o produtor vê uma planta com sintoma, fotografa, o AgripeWeb faz a **pré-análise por IA**, gera um **laudo em PT-BR**, e o **agrônomo revisa, edita e assina** antes de voltar ao cliente. A IA faz o volume; o agrônomo entrega o que a IA nunca poderá entregar: responsabilidade técnica.

### O que a pesquisa de mercado mostrou (fundamenta as decisões)

1. **Modelo especializado > LLM genérico para doença de planta.** Em benchmark agrícola, o melhor VLM genérico (Gemini-3 Pro) fica em ~62% de acerto; modelos especializados chegam a 90%+. Um laudo que um agrônomo assina não pode nascer de um chute de 62%.
2. **[Kindwise crop.health](https://www.kindwise.com/crop-health)**: REST, imagem base64, **23 culturas / 288 doenças e pragas**, retorna doença + probabilidade + sintomas + severidade + tratamento, com localização de idioma. **€0,01–0,05 por foto** (100 créditos grátis para testar). O custo de IA por laudo é desprezível perto do preço de uma assessoria.
3. **O valor está na assinatura do agrônomo, e a lei garante isso.** Receituário agronômico é obrigatório para agrotóxico (Lei 14.785/2023) e **só engenheiro agrônomo com CREA + ART pode prescrever** — já válido com assinatura eletrônica em 16 estados. **Nenhuma IA pode substituir esse ato.** A IA é a triagem; ele é o produto.
4. **Diferencial que ninguém tem:** concorrentes (Plantix, On Agri) diagnosticam a foto isolada. O AgripeWeb já tem **umidade do solo, histórico de irrigação, anomalias de sensor e previsão do tempo daquele pivô** — o laudo pode dizer *"antracnose provável (78%); a umidade do solo ficou acima de 85% por 6 dias e há chuva prevista — condição favorável ao patógeno; reveja a lâmina do quadrante 3"*. Isso é assessoria, não classificação de imagem.

### Decisões tomadas com o usuário
- **Perfil "Agrônomo" com carteira de clientes** (produtor → agrônomo vinculado; ele vê a fila de todos os clientes).
- **MVP em Web responsivo Angular** (`<input capture="environment">` abre a câmera do celular). Mobile/WhatsApp na fase 4.
- **Kindwise crop.health para o diagnóstico + LLM já existente (Gemini/Anthropic) para redigir o laudo.**

---

## Entregáveis desta rodada (nesta ordem)

### 1. PDF comercial — `docs/features/laudo-fitossanitario-ia/comercial.pdf`

Documento único com **duas seções**, para dois leitores:

**Parte 1 — Peça de venda ao produtor rural** (linguagem do campo, visual, sem jargão):
- A dor: *"Viu uma mancha na folha. E agora? Espera o agrônomo poder vir? Chuta o defensivo? Perde o talhão?"*
- Como funciona em 3 passos: **tira a foto → laudo em minutos → agrônomo assina**.
- O que o laudo entrega (mockup da tela de laudo): doença provável com probabilidade, **correlação com a irrigação e a chuva prevista do SEU pivô**, recomendação de manejo, assinatura do Eng. Agrônomo com CREA.
- Por que não é "só um app de foto": o AgripeWeb já sabe a umidade do seu solo e o que vai chover.
- Planos e preço (a definir com o cunhado — deixar tabela com placeholders).

**Parte 2 — Business case (para o cunhado / sócio):**
- Oportunidade e diferencial vs. Plantix / On Agri (eles veem a foto; nós vemos a foto **e a lavoura**).
- **Economia unitária:** custo de IA por laudo €0,01–0,05 (~R$ 0,06–0,32) vs. ticket de assessoria (R$ 600–3.000/dia; contratos R$ 5–20 mil/mês). Margem por laudo é ~100%.
- Modelo de receita: assinatura mensal por fazenda incluindo N laudos revisados; excedente por laudo; plano do agrônomo escalando por nº de clientes.
- **Base legal como fosso competitivo:** receituário obrigatório (Lei 14.785/2023), privativo de agrônomo com CREA + ART, assinatura eletrônica válida em 16 estados → *a IA nunca substitui o profissional; ela multiplica a capacidade dele.*
- Roadmap (as 5 fases), riscos e o disclaimer jurídico do MVP.

**Como gerar:** escrever `comercial.html` autocontido (CSS inline, `@page` para A4, sem assets externos) e converter com Edge headless — `msedge --headless --disable-gpu --print-to-pdf="...comercial.pdf" --no-pdf-header-footer "file:///...comercial.html"` (Edge está no Windows 11; fallback: Chrome, mesma flag). Manter o `.html` versionado para futuras edições.

### 2. Cinco issues épicas no GitHub (`cclautert/AgripeWeb`)

Uma por fase, cada uma com **objetivo, critérios de aceite e checklist de tarefas**, criadas via `gh issue create` (confirmar `gh auth status` antes):

| # | Título | Núcleo |
|---|---|---|
| Fase 0 | Fundação: upload de foto e persistência do laudo | Entidades, coleções, GridFS, **nginx `client_max_body_size`**, `POST /v1/diagnosis` (202), telas de upload/lista, worker mock |
| Fase 1 | MVP: pré-análise por IA e laudo em PT-BR | Kindwise, `CompleteAsync` nos LLMs, `PhytosanitaryReportPromptBuilder`, `ContextSnapshot`, `PlantDiagnosisProcessor`, retry, push, `CropHealthKey` no admin |
| Fase 2 | Perfil Agrônomo, carteira de clientes e assinatura do laudo | `IsAgronomist`, policy, `agronomist_clients`, convite/aceite/revogação, fila, claim/review/sign/reject, telas do agrônomo, **matriz de autorização cruzada** |
| Fase 3 | Profissionalização do laudo (PDF, histórico, reprocessamento) | PDF assinado, hash de conteúdo, histórico por pivô, comparação temporal |
| Fase 4 | Escala: quotas, cobrança, WhatsApp e mobile | Quotas/billing, Meta Cloud API, app mobile (`expo-camera` já instalado), e-mail real |

Cada issue referencia o plano em `docs/features/laudo-fitossanitario-ia/plan.md` (copiar este plano para lá como parte da Fase 0).

---

## Princípio arquitetural que amarra tudo

> **O agrônomo nunca lê as coleções do produtor.** Todo o contexto agronômico (umidade, anomalias, clima, pivô) é **congelado dentro do documento do laudo** (`ContextSnapshot`) no momento do processamento. O agrônomo lê **uma única coleção** — `plant_diagnoses` — e nada mais.

Isso reduz o "furo" no isolamento por tenant de N coleções para 1, torna a autorização testável em uma regra só, e dá reprodutibilidade jurídica: o laudo assinado reflete os dados **do dia do laudo**, não os de hoje.

---

## Modelo de dados

### `PlantDiagnosis` — `AgripeWebAPI/Models/Entities/PlantDiagnosis.cs` (coleção `plant_diagnoses`)

Herda de `Entity` (id int sequencial via `GetNextIdAsync`).

| Grupo | Campos |
|---|---|
| Tenant | `UserId` (produtor dono), `AgronomistId` (int?, snapshot do vínculo ativo na criação) |
| Entrada | `PivotId?`, `CropName?`, `ProducerNotes?`, `Latitude?`, `Longitude?`, `CapturedAt` |
| Imagem | `ImageFileId` (ObjectId → GridFS), `ImageContentType`, `ImageSizeBytes`, `ImageSha256` (dedup) |
| Estado | `Status`, `CreatedAt`, `UpdatedAt`, `ProcessingStartedAt`, `ProcessedAt`, `RetryCount`, `NextAttemptAt`, `FailureReason`, `WorkerId` |
| IA — classificador | `CropHealthRawJson` (auditoria), `Diseases[]` (`Name`, `ScientificName`, `Probability`, `Severity`, `Symptoms`, `Treatments[]`), `IsPlant`, `TopProbability` |
| IA — laudo | `AiReportMarkdown` (**imutável**), `AiProvider`, `AiModel`, `AiGeneratedAt` |
| Contexto | `ContextSnapshot` (PivotName, MoistureAvg7d, últimas leituras, anomalias abertas, ForecastSummary, alertas de irrigação 7d, limites do pivô) |
| Revisão | `ReviewerId?`, `ReviewStartedAt`, `AgronomistReportMarkdown`, `ConfirmedDisease`, `AgronomistSeverity`, `Prescription?`, `RejectionReason` |
| Assinatura | `Signature` (`AgronomistName`, `Crea`, `SignedAt`, `ContentSha256`) |
| Auditoria | `AuditTrail[]` append-only (`At`, `ActorUserId`, `FromStatus`, `ToStatus`, `Action`) |

**Máquina de estados:**
```
Uploaded ──(claim do worker)──> Processing ──┬──> PendingReview  (produtor TEM agrônomo)
    ^                              │          ├──> AiCompleted    (sem agrônomo — terminal)
    └──(retry, RetryCount<3)───────┤          └──> Rejected       (is_plant=false / prob. baixa)
                                   └──> Failed (após 3 falhas — terminal)

PendingReview ──(claim)──> InReview ──┬──> Signed (terminal)
                                      ├──> Rejected (foto ilegível)
                                      └──> PendingReview (devolve à fila)
```
Invioláveis: `Signed`/`AiCompleted` são terminais; **`AiReportMarkdown` nunca é sobrescrito** (a edição do agrônomo vive em `AgronomistReportMarkdown` — é o que permite auditar "o que a IA disse" vs "o que ele assinou", e é o que prova o trabalho dele); toda transição escreve em `AuditTrail`.

### Vínculo agrônomo↔produtor: **coleção `agronomist_clients`** (não campo no `User`)

`AgronomistClient`: `AgronomistId`, `ClientUserId`, `ClientEmail`, `Status` (`Pending|Active|Declined|Revoked|Expired`), `InviteToken`, `InvitedAt`, `InviteExpiresAt` (+7d), `AcceptedAt`, `RevokedAt`, `RevokedByUserId`.

Motivo: um campo `User.AgronomistId` não suporta **convite/aceite** (estado intermediário), não permite **convidar quem ainda não tem conta**, e a **revogação apaga a história** (auditoria de um laudo assinado exige saber quem era o agrônomo em março). Índice **único parcial** em `{ ClientUserId: 1 }` com filtro `Status: "Active"` faz o banco garantir *um agrônomo ativo por produtor*.

### Papel: **`bool IsAgronomist` no `User`** (espelha `IsAdmin`)

Não introduzir `Roles[]` agora — `IsAdmin` está cravado em `User.cs:19`, `JwtTokenService.cs`, `MainController.cs:64`, `CurrentUserContext.cs:50`, `admin.guard.ts`. Um bool paralelo custa ~6 linhas e reusa toda a plumbing. **Dívida registrada:** no 3º papel, colapsar os dois booleans em `Roles: List<string>`. Adicionar também `AgronomistCrea` (exibido na assinatura). Cria-se um agrônomo pelo admin (sem self-signup).

---

## Autorização — a parte que não pode errar

**Camada 1 — gate de endpoint.** Policy `"Agronomist"` (`RequireClaim("isAgronomist","true")`) em `ApiConfig.cs`, aplicada no `AgronomistController`. Responde "é um agrônomo?", não "de quem?".

**Camada 2 — escopo por documento.** Regra única em `Services/PlantDiagnosis/DiagnosisAccessService.cs`:

> **Leitura:** `u` lê o laudo `d` **sse** `d.UserId == u` **OU** (`d.AgronomistId == u` **E** existe `AgronomistClient{AgronomistId=u, ClientUserId=d.UserId, Status=Active}`).
> **Escrita:** o produtor dono cria e cancela; o agrônomo que passa na leitura transiciona `PendingReview → InReview → Signed|Rejected`. **Ninguém mais.** Admin **não** tem furo (laudo é ato profissional).

A dupla condição é deliberada: `AgronomistId` denormalizado dá a query rápida; a checagem do vínculo ativo faz a **revogação ter efeito imediato**. É o que evita o bug clássico "revoguei o agrônomo e ele continua vendo meus laudos".

O agrônomo **não** ganha acesso a `/v1/pivot/*`, `/v1/sensor/*`, `/v1/reads/*`. O contexto chega até ele só pelo `ContextSnapshot` congelado no laudo. A invariante do projeto continua verdadeira: *todo handler deriva o tenant de `_currentUser.UserId`, nunca de `request.UserId`*.

---

## Imagens: **GridFS**

`MongoDB.Driver 3.6.0` já traz — **zero pacote novo**, mesmo backup, API e Worker já compartilham `agpDBContext`. (Base64 no doc estoura o limite de 16MB e infla toda listagem; disco não funciona com API e Worker em containers separados; S3 é o certo a prazo, mas adia a demo.)

- `agpDBContext`: guardar o `IMongoDatabase` em campo (hoje é variável local) e expor `IGridFSBucket DiagnosisImages` (bucket `diagnosis_images`). Envolver em `IDiagnosisImageStore` (Upload/Download/Delete) para os handlers ficarem testáveis.
- Servir de volta: `GET /v1/diagnosis/{id}/image` aplica `CanAccessAsync` **antes** de abrir o stream. Como `<img src>` não manda `Authorization`, o front busca como **blob** (`responseType: 'blob'` → `createObjectURL`, com `revokeObjectURL` no destroy). Nada de token na query string.
- **BLOQUEADOR DE INFRA:** nenhum `client_max_body_size` configurado → **default de 1MB do nginx rejeita foto de celular (2–8MB) com 413 silencioso.** Adicionar `client_max_body_size 12m;` em **`AgripeWebUI/nginx.conf` E `docker/nginx/nginx.conf`** (o proxy externo rejeita antes de o interno ver).
- Defesas: downscale no front (canvas, máx 1600px, JPEG q0.8 → ~300–600KB), `[RequestSizeLimit]`, allowlist de content-type **+ sniff de magic bytes**, dedup por `ImageSha256`.

---

## Pipeline assíncrono

`POST /v1/diagnosis` grava a imagem, resolve o `AgronomistId` do vínculo ativo, insere `Status=Uploaded` e devolve **202** — a API não segura a request por 10–30s esperando IA.

`PlantDiagnosisProcessor` (novo `BackgroundService` no Worker, `PeriodicTimer` de 30s — o produtor está olhando a tela; molde: `AgripeWebWorker/Services/IrrigationAlertScheduler.cs:8-70`):
1. **Claim atômico:** `FindOneAndUpdateAsync` filtrando `Status=="Uploaded" && NextAttemptAt <= now`, setando `Processing` + `WorkerId`. É o lock — seguro com N workers, sem Redis/Hangfire (mesmo espírito do `GetNextIdAsync`).
2. **Reclaim de zumbis:** `Processing` há mais de 10min → volta para `Uploaded` (ou `Failed` se `RetryCount >= 3`). Backoff 1/5/15 min.
3. Monta `ContextSnapshot` → Kindwise → LLM → grava laudo → status final → `IPushNotificationService.SendAsync` (já registrado no worker) para produtor e/ou agrônomo.

**Resolvendo `WorkerUserContext.UserId == null`:** a lógica de processamento **não vira handler MediatR**. Vira serviço puro `IPlantDiagnosisProcessingService.ProcessAsync(diagnosisId, ct)` que **não injeta `ICurrentUserContext`** — o tenant está *dentro* do documento (`d.UserId`), que é o que todas as queries de contexto usam. Um handler MediatR seria auto-registrado pelo assembly scan (`ApiConfig.cs:105-108`) e bastaria alguém mapeá-lo num controller para virar um IDOR perfeito. Serviço fora do pipeline sinaliza "roda em contexto de sistema" e mantém a regra da casa **auditável por grep**.

**Registro faltante no Worker:** `IWeatherForecastService`/`WeatherForecastOrchestrator` não está registrado lá (só OpenMeteo). Adicionar em `AgripeWebWorker/Program.cs`, espelhando `ApiConfig.cs:34-38`.

---

## Serviços novos

- **`ICropDiagnosisProvider`** → `Services/CropHealth/KindwiseCropHealthService.cs`: `POST api/v1/identification?details=common_names,description,treatment,severity&language=pt`, header `Api-Key`, body `{ images:[base64], latitude, longitude, datetime }`. HttpClient em `ApiConfig.cs` **e** no Worker, `BaseAddress=https://crop.kindwise.com/`, **timeout 45s** (os 30s dos LLMs são justos demais). Sem Polly (não está no projeto) — retry é do worker.
- **Chave do Kindwise:** seguir o padrão existente — as chaves de IA **não ficam em appsettings, ficam no Mongo** (`Models/Entities/PlatformAiSettings.cs`, CRUD em `AdminController.cs:76-95`). Adicionar `CropHealthKey`, `CropHealthEnabled` (kill-switch), com o mesmo mascaramento de `GeminiKey`/`AnthropicKey`, e expor na tela `admin/ai-settings`. Zero mecanismo novo de segredo.
- **Laudo (LLM):** mudança **aditiva** em `IAIInsightsService` — acrescentar `CompleteAsync(systemPrompt, userMessage, apiKey, model, ct)` e extrair para lá o corpo HTTP que já existe em `GeminiInsightsService`/`AnthropicInsightsService`; `GetInsightsAsync` passa a chamá-lo. Comportamento idêntico, testes atuais seguem válidos, e ganhamos um canal genérico de texto sem duplicar cliente HTTP.
- **`PhytosanitaryReportPromptBuilder`** (espelho de `AIInsightsPromptBuilder.cs`) — seções fixas: *Identificação · Sintomas · Diagnóstico provável (com probabilidade) · **Correlação com o manejo** (umidade/irrigação/clima) · Recomendações de manejo · Limitações*.
  **Guarda-corpo crítico:** o prompt **proíbe** citar produto comercial e dose — isso é receituário agronômico, ato privativo com responsabilidade legal. A IA descreve *estratégias de manejo*; a prescrição só existe no campo `Prescription`, preenchido pelo **agrônomo humano**. Não é preciosismo: é o que separa software de exercício ilegal da profissão.
- **`PlantDiagnosisContextBuilder`** — monta o `ContextSnapshot` a partir de `d.UserId` (+ `d.PivotId`): sensores do pivô, umidade média 7d, última leitura por sensor, anomalias abertas, `IWeatherForecastService.GetForecastAsync`, alertas de irrigação 7d.

---

## Endpoints

**`PlantDiagnosisController`** (`[Authorize]`, `v1/diagnosis`): `POST /` (multipart → **202** `{id,status,statusUrl}`) · `GET /` (lista) · `GET /{id}` (detalhe) · `GET /{id}/status` (polling barato) · `GET /{id}/image` (dono **ou** agrônomo vinculado) · `DELETE /{id}` (só dono, só se não `Signed`).

**`AgronomistController`** (`[Authorize(Policy="Agronomist")]`, `v1/agronomist`): `GET /queue` · `GET /diagnosis/{id}` · `POST /diagnosis/{id}/claim` · `PUT /diagnosis/{id}/review` (rascunho) · `POST /diagnosis/{id}/sign` · `POST /diagnosis/{id}/reject` · `GET /clients` · `POST /clients/invite` · `DELETE /clients/{clientId}` (revogar).

**Lado produtor** (`UserController`): `GET /v1/user/agronomist-invites` · `POST .../{id}/accept` · `POST .../{id}/decline` · `DELETE /v1/user/agronomist-link` (o produtor pode demitir o agrônomo — direito irrevogável).

**Regra de controller:** `IFormFile` **não** entra na request MediatR — o controller converte para `byte[]` e monta o `CreatePlantDiagnosisRequest`, mantendo o handler testável sem tipos HTTP. O handler ignora qualquer `request.UserId` e usa `_currentUser.UserId`.

---

## UI Angular

Serviços `diagnosis.service.ts` e `agronomist.service.ts`. Guard `agronomist.guard.ts` (cópia de `admin.guard.ts`, lendo `isAgronomist` do JWT — gravar em `login.component.ts` e `auth-callback.component.ts`, limpar no logout).

Rotas (filhas de `LayoutComponent`, em `app.routes.ts`):
- `diagnosticos` (lista) · `diagnosticos/novo` (input `capture="environment"`, preview, downscale em canvas, seleção de pivô, barra de progresso) · `diagnosticos/:id` (polling 3s enquanto processa; foto, doenças com barra de probabilidade, laudo em markdown, selo *"Assinado por Eng. Agr. X — CREA Y"*)
- `agronomo/fila` · `agronomo/laudo/:id` · `agronomo/clientes` — com `[AuthGuard, AgronomistGuard]`

**Tela de revisão do agrônomo (a tela que vende o produto)** — 3 colunas:
1. **Evidência** — foto com zoom, produtor/pivô, `ContextSnapshot` renderizado (umidade 7d, anomalias, chuva prevista). *Read-only, vem do laudo.*
2. **Pré-análise da IA** — top-3 doenças com probabilidade + `AiReportMarkdown` original, **imutável**, colapsável.
3. **Laudo do agrônomo** — textarea markdown **pré-preenchida com o texto da IA** (ele edita, não redige do zero — essa economia de tempo é literalmente o que ele está comprando), campo `Prescription`, severidade, e `Salvar rascunho` / `Assinar e enviar` / `Rejeitar`.

Menu (`layout.component.html`): item "Laudos" para todos; seção `*ngIf="isAgronomist"` (molde do bloco `*ngIf="isAdmin"`) com "Fila de Laudos" (badge de contagem) e "Meus Clientes". Sininho: `GetUserAlertsHandler` passa a unir `plant_diagnoses` com `AlertType` `DiagnosisReady` / `DiagnosisPending` (atenção: `UserAlertResponse.PivotName` é obrigatório hoje e o laudo pode não ter pivô → tratar como "—").

---

## Fases

**Fase 0 — Fundação.** Entidades + coleções + índices + GridFS + **`client_max_body_size` nos 2 nginx** + `POST /v1/diagnosis` + lista/detalhe/imagem + telas de upload e listagem. Worker mock leva `Uploaded → AiCompleted` com texto fixo.
*Pronto quando:* foto tirada no celular sobe pelo 4G e volta na tela. **Sem o nginx ajustado, todo o resto é teoria.**

**Fase 1 — MVP demonstrável na fazenda.** ⭐ Kindwise + `CompleteAsync` nos LLMs + `PhytosanitaryReportPromptBuilder` + `ContextSnapshot` + `PlantDiagnosisProcessor` real + retry + push "laudo pronto" + `CropHealthKey` no admin. **Ainda sem agrônomo** (`AgronomistId == null` → `AiCompleted`).
*É isto que demonstra a mágica:* foto de folha doente → ~40s → laudo em PT-BR cruzando a doença com a umidade e a chuva prevista **daquele pivô**. É o que faz o cunhado chamar o vizinho.

**Fase 2 — O produto que se vende.** `IsAgronomist` + claim + policy + `agronomist_clients` + convite/aceite/revogação + fila + claim/review/sign/reject + push ao agrônomo + telas do agrônomo.

**Fase 3 — Profissionalização.** PDF do laudo assinado (QuestPDF), hash do conteúdo, histórico por pivô/talhão, comparação temporal ("a mancha piorou desde 12/03"), reprocessar `Failed`.

**Fase 4 — Escala.** Quotas/cobrança (laudos/mês por produtor; plano do agrônomo por nº de clientes), WhatsApp (Meta Cloud API — onde o produtor realmente vive), app mobile (`expo-camera` já está instalado e em uso), e-mail real (`IAlertEmailService` é NoOp hoje).

**Fora de escopo, e é preciso dizer em voz alta:** **receituário agronômico e ART não são "fase 4"** — são produto regulado (CREA validado, ICP-Brasil, responsabilidade técnica). O laudo do MVP é **informativo** e carrega disclaimer explícito na UI e no rodapé do markdown: *"Laudo técnico informativo. Não constitui receituário agronômico nem ART."* Prometer isso cedo é o maior risco jurídico da feature.

---

## Testes (`AgripeWebAPI.Tests/` — xUnit + Moq + `MongoMockHelper`)

**A matriz de autorização cruzada é o entregável de segurança.** `[Theory]` para cada endpoint de leitura (detalhe, **imagem**) e de escrita (sign):

| Ator | Esperado |
|---|---|
| Produtor dono | ✅ |
| Outro produtor | ❌ |
| Agrônomo vinculado (`Active`) | ✅ |
| Agrônomo de outro produtor | ❌ |
| **Agrônomo com vínculo `Revoked`** | ❌ ← *o bug que todo mundo escreve* |
| Agrônomo com vínculo `Pending` | ❌ |
| Admin | ❌ (laudo é ato profissional) |

Por handler: `CreatePlantDiagnosisHandler` (ignora `request.UserId` forjado; snapshota `AgronomistId`; rejeita mime/tamanho) · `GetDiagnosisImageHandler` (a imagem é o endpoint que todo mundo esquece de proteger) · `GetAgronomistQueueHandler` (só clientes `Active`) · `SignDiagnosisHandler` (`Signed` é terminal; **`AiReportMarkdown` não é sobrescrito**) · `AcceptAgronomistInviteHandler` (aceite revoga o vínculo anterior; convite expirado falha) · `PlantDiagnosisProcessingService` (claim atômico; `Failed` após 3 retries; `is_plant=false` → `Rejected`; **asserção de que não injeta `ICurrentUserContext`**) · `KindwiseCropHealthService` (`MockHttpMessageHandler` já existe em `Tests/Helpers/`).

---

## Riscos

| Risco | Mitigação |
|---|---|
| **Vazamento cross-tenant pelo novo papel** (crítico) | `ContextSnapshot` (agrônomo não lê pivôs/sensores) + regra única em `IDiagnosisAccessService` + matriz de testes. Sem essa disciplina, "agrônomo" é o vetor natural de um IDOR. |
| **Nginx 1MB → 413** (mata a demo) | 2 arquivos, 1 linha cada. **Testar com foto real de celular por 4G antes de qualquer demo.** |
| Custo/abuso do Kindwise | Rate limit `diagnosis-upload` (10/h/usuário) no `RateLimiter` já existente + dedup por `ImageSha256` + `CropHealthEnabled` como kill-switch. |
| **Foto ruim → laudo confiante e errado** (destrói confiança) | `is_plant=false` ou `TopProbability < 0.25` → `Rejected` automático com dica acionável ("aproxime a folha, foque na lesão, evite contraluz"). **Nunca** gerar laudo confiante sobre evidência fraca. |
| **LLM alucinar dose/produto químico** (crítico, legal) | Prompt proíbe produto e dose; prescrição só no campo do humano; disclaimer no laudo. |
| Latência (Kindwise 3–10s + LLM 5–20s) | 202 + polling; worker a cada 30s → ~30–60s de espera. Aceitável. |
| Agrônomo abandona laudo em `InReview` | `ReviewStartedAt` + release automático após 24h → volta a `PendingReview`. |
| Chave Kindwise em claro no Mongo | Não é regressão (Gemini/Anthropic já são). Dívida → Secrets Manager. |

---

## Verificação

0. **Entregáveis desta rodada:** abrir o `comercial.pdf` e conferir que as duas seções renderizaram em A4 sem cortes e sem depender de asset externo; conferir as 5 issues criadas com `gh issue list --label laudo-fitossanitario`.
1. **Testes:** `dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj` — a matriz de autorização cruzada tem que passar inteira (é o gate de merge desta feature).
2. **Upload real (o que quebra primeiro):** subir o stack (`docker compose -f docker/docker-compose.yml up --build`), abrir `/diagnosticos/novo` **no celular pelo 4G**, tirar foto de uma folha e confirmar que **não volta 413** e que a imagem aparece na listagem. Fazer isso na Fase 0, antes de escrever uma linha de IA.
3. **Pipeline de IA:** cadastrar a `CropHealthKey` (100 créditos grátis) em `/admin/ai-settings`, subir foto de folha visivelmente doente e acompanhar `Uploaded → Processing → PendingReview` no polling; conferir no Mongo que `ContextSnapshot` trouxe umidade/clima reais do pivô e que `AiReportMarkdown` **cita a correlação com o manejo** (é o diferencial — se o laudo não citar, o prompt está errado).
4. **Fluxo do agrônomo (ponta a ponta):** criar um usuário agrônomo pelo admin, convidar o produtor, aceitar o convite, ver o laudo na fila, assinar — e confirmar que o produtor recebe o push e vê o selo de assinatura.
5. **Prova de isolamento (manual):** com o token de um segundo agrônomo **não vinculado**, chamar `GET /v1/diagnosis/{id}` e `GET /v1/diagnosis/{id}/image` → tem que dar 403/404. Depois **revogar** o vínculo do agrônomo legítimo e repetir → também tem que negar.
6. **Graph/doc:** `graphify update .` e atualizar `CLAUDE.md` (novas rotas, entidades, coleções e o papel `agronomist`).

## Fontes
- [Kindwise crop.health](https://www.kindwise.com/crop-health) · [Preços Kindwise](https://www.kindwise.com/pricing) · [Plantix Vision API](https://plantix.net/en/business/plantix-vision-api/)
- [AgroBench: VLM Benchmark in Agriculture](https://arxiv.org/pdf/2507.20519) · [AI-Driven Plant Disease Detection (review)](https://pmc.ncbi.nlm.nih.gov/articles/PMC13066816/)
- [Receituário agronômico e ART](https://agroadvance.com.br/blog-receituario-agronomico-art/) · [Assinatura eletrônica na receita agronômica](https://agriq.com.br/assinatura-eletronica/)
- [Precificação de consultoria rural (Aegro)](https://aegro.com.br/blog/precificacao-da-consultoria-rural/) · [On Agri — agrônomo digital por WhatsApp](https://noticias.broto.com.br/tecnologia/consultoria-agronomica-por-whatsapp-on-agri/)
