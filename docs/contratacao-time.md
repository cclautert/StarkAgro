# Contratação do time — StarkAgro

Documento de referência para a issue **Contratação do time** (planejamento de vagas e alocações).

## Objetivo

Definir e contratar (ou alocar) o time mínimo viável para evoluir o **StarkAgro** — plataforma de IoT agrícola para monitoramento de umidade em pivôs/quadrantes, dashboards, recomendações de irrigação com previsão de chuva, app web (Angular), API (.NET) e firmware (ESP8266/ESP32).

Repositório: monorepo com API, UI, IoT, Docker, CI/CD e Terraform (AWS).

## Contexto do produto

- **Usuários:** produtores e técnicos agrícolas (multi-tenant por usuário).
- **Valor:** leituras de sensores → visualização por quadrante → alertas e painel de irrigação → previsão meteorológica para adiar irrigação quando chover.
- **Stack:** ASP.NET Core 10 + MediatR + MongoDB | Angular 19 | Arduino/ESP | Docker + GitHub Actions + VPS/AWS.

Visão geral do produto: [README.md](../README.md).

---

## Time recomendado

### Fase 1 — Produto em produção e evolução contínua (mínimo)

| Papel | Dedicação sugerida | Foco no StarkAgro |
|-------|-------------------|-------------------|
| **Tech Lead / Arquiteto** | 20–40% ou 1 FTE compartilhado | Visão técnica, revisão de PRs, tenant isolation, padrões CQRS/MongoDB, roadmap |
| **Backend .NET** | 1 FTE | Handlers MediatR, API REST, auth JWT/OAuth, integrações (previsão tempo), testes xUnit |
| **Frontend Angular** | 1 FTE | Home/quadrantes, dashboards Chart.js, mapa Leaflet, UX Material, proxy `/api/v1` |
| **DevOps / SRE** | 20–40% | Docker Compose, CI (GitHub Actions), deploy VPS/Hostinger, Terraform AWS, secrets, monitoramento |
| **Product Owner / Agro** | 20–40% | Regras de irrigação, limites de umidade, validação com campo, priorização backlog |
| **QA** | 20–40% ou part-time | Regressão web/API, cenários multi-tenant, testes de integração sensores→API |

### Fase 2 — Escala e hardware (quando priorizar campo)

| Papel | Quando contratar | Foco |
|-------|------------------|------|
| **Engenheiro IoT / Embarcados** | Sensores em escala ou LoRa | Firmware ESP8266/ESP32, gateway LoRa, confiabilidade e consumo |
| **Data / ML (opcional)** | Insights com IA | Refinar previsão e recomendações (ex.: Google Weather AI, anomalias) |

### Papéis que podem ser terceirizados no início

- Design UI/UX (sistema de cores dos quadrantes, painéis, onboarding).
- Suporte N1/N2 (documentação + runbook em [deploy-hostinger.md](deploy-hostinger.md)).
- Agrônomo consultor (validação científica dos limiares, não precisa ser FTE).

---

## Perfil técnico desejado (resumo para vagas)

**Backend:** C# / ASP.NET Core, REST, MongoDB, JWT, testes unitários, noções de multi-tenant e segurança.

**Frontend:** Angular (standalone + módulos), TypeScript, RxJS, Chart.js, integração com APIs REST autenticadas.

**DevOps:** Docker, Linux VPS, GitHub Actions, variáveis de ambiente, HTTPS, backup MongoDB; desejável Terraform/ECS.

**IoT:** C/Arduino, ESP8266/ESP32, Wi-Fi/LoRa, HTTP/JSON para API, noções de energia e instalação em campo.

**PO Agro:** vivência com irrigação/pivô central; traduz necessidade do produtor em critérios de aceite.

---

## Matriz RACI (rascunho)

| Área | Responsável (R) | Aprovador (A) | Consultado (C) | Informado (I) |
|------|-----------------|---------------|----------------|---------------|
| API / domínio | Backend .NET | Tech Lead | PO Agro, QA | Frontend, DevOps |
| UI web | Frontend Angular | Tech Lead | PO Agro, Design | Backend, QA |
| IoT / firmware | Eng. IoT | Tech Lead | Backend | PO Agro, DevOps |
| Infra / deploy | DevOps | Tech Lead | Backend | Todos |
| Regras de negócio agro | PO Agro | Product | Backend, Frontend | QA |

*Preencher nomes ou “vaga aberta” quando as contratações forem definidas.*

---

## Entregáveis da contratação

- [ ] Matriz RACI por componente (API, UI, IoT, Infra) com nomes definidos
- [ ] Lista de vagas/alocações com senioridade (Júnior/Pleno/Sênior) e modelo (CLT, PJ, parceiro)
- [ ] Orçamento estimado (FTE × meses) para Fase 1 e Fase 2
- [ ] Cronograma de contratação (ordem sugerida abaixo)
- [ ] Definition of Done do time (PR review, testes API, deploy documentado, isolamento por `UserId`)
- [ ] Onboarding: acesso ao repo, ambientes (local + Docker), leitura de `README.md` e `CLAUDE.md`

### Ordem sugerida de contratação

1. Backend .NET  
2. Frontend Angular  
3. DevOps (part-time)  
4. Product Owner / Agro  
5. QA  
6. Engenheiro IoT (Fase 2)

---

## Critérios de aceite

1. Time Fase 1 identificado com nome ou tipo de contratação (vaga aberta / alocado).
2. Pelo menos 1 responsável por camada: API, UI, Infra.
3. PO ou representante de domínio agro definido para priorização.
4. Processo de code review e deploy acordado (branch `main` + CI verde antes de produção).

---

## Riscos se o time ficar incompleto

| Lacuna | Risco |
|--------|--------|
| Sem backend dedicado | Débito em tenant isolation e testes dos handlers |
| Sem DevOps | Deploy manual e risco em secrets MongoDB/JWT/OAuth |
| Sem PO agro | Regras de irrigação desalinhadas com a realidade do campo |
| Sem IoT | Sensores dependem de um único mantenedor; gargalo em firmware LoRa |
| Sem QA | Regressões em multi-tenant e fluxos de dashboard/irrigação |

---

## Time mínimo com orçamento apertado

Se for preciso começar com o menor núcleo possível:

1. **Backend .NET** — coração do produto e multi-tenant  
2. **Frontend Angular** — experiência do produtor  
3. **DevOps part-time** — produção estável (`agripeweb.com`)

Depois: PO agro → QA → IoT conforme crescer a base de pivôs/sensores.

---

## Links úteis

- [README.md](../README.md) — visão do produto e stack  
- [CLAUDE.md](../CLAUDE.md) — convenções de desenvolvimento  
- [deploy-hostinger.md](deploy-hostinger.md) — deploy em produção (VPS)  
- [terraform/aws/README.md](../terraform/aws/README.md) — infraestrutura AWS  
- Backlog GitHub: previsão de chuva, insights de irrigação

---

## Prompt para delegação (Paperclip / gestão)

> Montar proposta de time para o projeto **StarkAgro** (IoT agrícola: API .NET + MongoDB, Angular 19, firmware ESP, Docker/CI, deploy VPS e AWS). Preciso de: (1) time mínimo Fase 1 com papéis, % de dedicação e senioridade; (2) time Fase 2 para IoT em escala; (3) matriz RACI API/UI/IoT/Infra; (4) ordem de contratação e orçamento rough; (5) critérios de aceite e riscos sem cada papel. Basear neste documento e no README do repositório.
