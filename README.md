# StarkAgro

Plataforma web para monitoramento agrícola e apoio à decisão de irrigação, integrando sensores de campo, painéis visuais e previsão meteorológica.

## Sobre o projeto

O **StarkAgro** é uma solução de IoT agrícola voltada a produtores e técnicos que precisam acompanhar a umidade do solo em pivôs de irrigação e tomar decisões de irrigação com mais contexto. O sistema organiza a propriedade em **pivôs** (áreas de irrigação), cada um dividido em **quadrantes**, onde sensores enviam leituras periódicas de umidade.

Na interface web, o usuário visualiza o estado de cada quadrante (mapa circular com cores e médias), acessa gráficos históricos por quadrante, cadastra pivôs e sensores, define limites de umidade e consulta um **painel de irrigação** com tendências, alertas e recomendações. Quando o pivô possui localização geográfica, a API integra **previsão de chuva** (Open-Meteo e, opcionalmente, Google Weather AI) para adiar recomendações de irrigação quando há precipitação prevista no horizonte configurável.

O acesso é **multi-tenant**: cada usuário autenticado (login/senha ou Google OAuth) vê e gerencia apenas seus próprios pivôs, sensores e leituras. A API REST em ASP.NET Core expõe os dados; o frontend Angular oferece a experiência no navegador; o firmware embarcado (ESP8266/ESP32) coleta medições no campo e envia leituras à API.

## Principais funcionalidades

- Cadastro e gestão de **pivôs**, **sensores** e **perfil de usuário**
- Seleção de **localização do pivô** no mapa (Leaflet/OpenStreetMap), com altitude e endereço
- **Visão por quadrantes** na home, com médias de umidade e navegação para dashboards detalhados
- **Dashboard por quadrante** com gráficos de leituras e configuração de limites
- **Painel de irrigação** com tendência, alertas e análise considerando previsão de chuva
- **Autenticação** JWT (8 h) e login externo via Google OAuth 2.0
- **Firmware IoT** para ESP8266 (Wi-Fi direto) e ESP32 com gateway LoRa

## Arquitetura

| Componente | Descrição |
|------------|-----------|
| **StarkAgroAPI** | API REST (.NET 10, MediatR/CQRS, MongoDB, JWT/OAuth) |
| **StarkAgroUI** | SPA Angular 19 (Material, Chart.js, proxy para API em desenvolvimento) |

O firmware dos sensores (ESP8266 / ESP32 LoRa) e o app móvel React Native não fazem mais parte deste repositório. Os dispositivos em campo continuam consumindo os endpoints de login e de leitura da API, então mudanças nesses contratos ainda precisam ser coordenadas com o firmware.

```
Sensores (campo) → API (MongoDB) ← UI Web
                        ↓
              Previsão meteorológica (Open-Meteo / Google)
```

## Stack tecnológica

- **Backend:** ASP.NET Core 10, MediatR, MongoDB Driver, BCrypt, Swagger
- **Frontend:** Angular 19, Angular Material, Chart.js, Leaflet
- **Dados:** MongoDB (`users`, `pivots`, `sensors`, `read_sensors`)
- **Infra:** Docker Compose, GitHub Actions (CI/CD), Terraform (AWS ECS), deploy em VPS

## Início rápido

### Pré-requisitos

- [.NET SDK](https://dotnet.microsoft.com/download) compatível com o projeto (10.x)
- [Node.js](https://nodejs.org/) (para a UI)
- [MongoDB](https://www.mongodb.com/) em execução local ou via Docker

### API

```bash
dotnet run --project StarkAgroAPI/StarkAgroAPI.csproj
```

Configure a seção `MongoDb` em `StarkAgroAPI/appsettings.Development.json` (connection string e nome do banco). Antes de `dotnet build`, encerre qualquer instância da API em execução para evitar bloqueio do executável.

### Interface web

```bash
cd StarkAgroUI
npm install
npm run start
```

Acesse [http://localhost:4200](http://localhost:4200). A UI chama a API por URLs relativas (`/api/v1/...`) via proxy configurado no `angular.json`.

### Testes

```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj
```

### Docker (stack completa)

```bash
docker compose -f docker/docker-compose.yml up --build
```

API em `localhost:8080`, UI em `localhost:80`, MongoDB em `localhost:27027`.

## Estrutura do repositório

```
StarkAgro/
├── StarkAgroAPI/          # API REST e domínio (handlers MediatR)
├── StarkAgroAPI.Tests/    # Testes unitários (xUnit + Moq)
├── StarkAgroUI/           # Frontend Angular
├── docker/                # Compose e imagens
├── docs/                  # Documentação (deploy, features)
├── terraform/aws/         # Infraestrutura AWS
└── .github/workflows/     # CI e deploy
```

## Documentação adicional

- [CLAUDE.md](CLAUDE.md) — guia para agentes de código (arquitetura, armadilhas, comandos)
- [docs/agent-behavior.md](docs/agent-behavior.md) — diretrizes genéricas de comportamento para LLMs
- [docs/contratacao-time.md](docs/contratacao-time.md) — planejamento de time e papéis do projeto
- [docs/agents/README.md](docs/agents/README.md) — SOUL.md e HEARTBEAT.md (CEO Stark, IoT Lead) para Paperclip
- [docs/deploy-hostinger.md](docs/deploy-hostinger.md) — deploy na VPS (Hostinger)
- [terraform/aws/README.md](terraform/aws/README.md) — infraestrutura na AWS

## Segurança e configuração

Não commite segredos reais (connection strings, chaves JWT, client secrets OAuth). Use placeholders (`CHANGE_ME`, `YOUR_GOOGLE_CLIENT_ID`) nos arquivos versionados e configure valores locais via user secrets, variáveis de ambiente ou arquivos ignorados pelo Git.

## Licença

Consulte o repositório ou o mantenedor para informações de licenciamento, se aplicável.
