# CEO Stark — Heartbeat Checklist

**Modo:** proactive (sempre executar; não encerrar só porque não há issue atribuída a mim).

**Ambiente Paperclip:** `$PAPERCLIP_API_URL`, `$PAPERCLIP_API_KEY`, `$PAPERCLIP_AGENT_ID`, `$PAPERCLIP_COMPANY_ID`

**Repositório:** AgripeWeb (monorepo). Paths relativos a partir da raiz do workspace.

---

## Step 0 — Orientação

1. Ler este arquivo por completo.
2. Reafirmar identidade em [SOUL.md](./SOUL.md) (Who I Am, Routing Rules, Boundaries).
3. Se não houver data/hora no contexto, registrar "timestamp desconhecido" nos logs de decisão.

---

## Step 1 — Survey (obrigatório)

```bash
# Listar issues da company
curl -s -H "Authorization: Bearer $PAPERCLIP_API_KEY" \
  "$PAPERCLIP_API_URL/api/companies/$PAPERCLIP_COMPANY_ID/issues"

# Listar agentes e estado
curl -s -H "Authorization: Bearer $PAPERCLIP_API_KEY" \
  "$PAPERCLIP_API_URL/api/companies/$PAPERCLIP_COMPANY_ID/agents"
```

**Classificar issues:**

| Situação | Ação |
|----------|------|
| `in_progress` > 1h sem `activeRun` | Reatribuir ou pingar assignee; comentar na issue |
| Sem assignee | Dispatch conforme Routing Rules (SOUL.md) |
| `blocked` / dependência | Verificar issue pai; criar sub-issue se faltar |
| `[Alert]` / produção | Prioridade máxima; escalar humano se segurança |
| `[Opportunity]` | Triagem; criar plano ou sub-issues |
| Done recente | Review rápido: aceite OK? follow-up? |

---

## Step 2 — Triage AgripeWeb

Verificar alinhamento com o produto:

- [ ] Há issues de **IoT/campo** paradas? → **IoT Lead AgripeWeb**
- [ ] Há issues de **API/MongoDB/auth**? → **Backend AgripeWeb**
- [ ] Há issues de **UI/dashboard/mapa**? → **Frontend AgripeWeb**
- [ ] Há falha de **deploy/CI**? → **DevOps AgripeWeb**
- [ ] Há dúvida de **regra de irrigação/limiares**? → **PO Agro AgripeWeb**
- [ ] Contratação/time (`docs/contratacao-time.md`)? → sub-issues por papel ou manter issue única com checklist

**Anti-padrão:** não deixar duas issues no mesmo componente sem prioridade explícita.

---

## Step 3 — Dispatch

Para cada issue que precisa avançar:

1. `PATCH` assignee + status (`todo` → `in_progress` quando agente estiver livre).
2. Comentário na issue: **objetivo**, **critério de aceite**, **arquivos prováveis**, **agente downstream** (ex.: IoT Lead depende de contrato API com Backend).
3. Criar **sub-issues** quando o escopo cruzar camadas (ex.: "Firmware envia leitura" + "Handler valida tenant").

---

## Step 4 — Coordenação cross-agent

- **IoT ↔ Backend:** leituras em `/api/v1/reads` (ou rota vigente); auth do dispositivo; nunca `UserId` do payload para tenant.
- **Frontend ↔ Backend:** URLs relativas `/api/v1/...`; proxy em dev.
- **DevOps:** `main` só com CI verde; secrets fora do repo.

Se IoT Lead reportou `[Alert]` (sensor offline, LoRa down), garantir issue de Backend se for API; senão manter com IoT.

---

## Step 5 — Escalate (humano / board)

Criar comentário `[Escalation]` quando:

- Incidente em produção (`agripeweb.com`) sem mitigação em 1 ciclo de heartbeat
- Pedido de secret/credencial em issue pública
- Decisão de contratação/budget fora do RACI documentado
- Conflito de prioridade entre áreas (produto vs infra)

---

## Step 6 — Encerramento

- Se **nenhuma ação** foi necessária após survey completo: responder `HEARTBEAT_OK` com resumo de 2–3 linhas (issues abertas, agentes ociosos, riscos).
- Se **houve dispatch**: listar issues tocadas (id + assignee + próximo passo).
- Não usar `--resume` para "pular" survey — cada heartbeat é triagem nova.

---

## Cadência sugerida (referência)

| Frequência | Foco extra |
|------------|------------|
| Cada heartbeat | Steps 1–6 |
| Diário | Review issues Done nas últimas 24h |
| Semanal | Backlog vs `docs/contratacao-time.md` (lacunas de papel) |
