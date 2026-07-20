# Agentes Paperclip — StarkAgro

Arquivos de identidade e heartbeat para orquestração multi-agente ([Paperclip](https://github.com/paperclipai/paperclip) / OpenClaw).

| Agente | Papel | Modo heartbeat | Arquivos |
|--------|-------|----------------|----------|
| **CEO Stark** | Coordenação, triagem, dispatch, escalação | `proactive` | [ceo-stark/SOUL.md](ceo-stark/SOUL.md), [ceo-stark/HEARTBEAT.md](ceo-stark/HEARTBEAT.md) |

## Uso no Paperclip

1. Criar agentes na company **StarkAgro** com os nomes acima.
2. Apontar o workspace para este repositório (ou cópia dos arquivos no workspace do agente).
3. Configurar `runtimeConfig.heartbeat.mode: "proactive"` para ambos.
4. Injetar identidade condensada (~300 palavras) de **SOUL.md** em `adapterConfig.promptTemplate` (seções: Who I Am, Core Principles, Routing Rules, Boundaries).
5. Garantir que o wake procedure inclua: *"Read HEARTBEAT.md and follow each step."*

Variáveis esperadas: `PAPERCLIP_API_URL`, `PAPERCLIP_API_KEY`, `PAPERCLIP_AGENT_ID`, `PAPERCLIP_COMPANY_ID`.

## Próximos agentes (sugestão)

Backend, Frontend, DevOps, PO Agro e QA podem seguir o mesmo par SOUL + HEARTBEAT; use [contratacao-time.md](../contratacao-time.md) como base de responsabilidades.
