# Deploy na VPS Hostinger (Docker Compose + GitHub Actions)

Este guia cobre o bootstrap manual da VPS e a configuração do CI/CD automático via GitHub Actions.

## Visão geral

| Workflow | Arquivo | Quando roda |
|----------|---------|-------------|
| **CI** | [.github/workflows/ci.yml](../.github/workflows/ci.yml) | `push` e `pull_request` (build .NET, Angular e validação das imagens Docker) |
| **Deploy** | [.github/workflows/deploy.yml](../.github/workflows/deploy.yml) | Após o CI concluir com sucesso em um `push` na branch `main` |

O deploy atualiza **API**, **UI** e **Worker** via SSH; não reinicia `nginx-proxy` nem `certbot` (certificados TLS permanecem nos volumes Docker).

## Pré-requisitos na VPS

- Ubuntu (ou distribuição Linux compatível)
- Docker Engine + plugin Compose v2
- Git
- Portas **80** e **443** abertas no firewall (`ufw allow 80`, `ufw allow 443`)
- DNS apontando o domínio (ex.: `agripeweb.com`) para o IP da VPS
- **Não** expor a API na porta 8080 publicamente em produção

### Instalar Docker (exemplo Ubuntu)

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo usermod -aG docker "$USER"
```

Faça logout/login para usar Docker sem `sudo`.

## Bootstrap único na VPS

### 1. Clonar o repositório

```bash
sudo mkdir -p /opt/agripeweb
sudo chown "$USER:$USER" /opt/agripeweb
git clone https://github.com/cclautert/AgripeWeb.git /opt/agripeweb
cd /opt/agripeweb
```

Use o mesmo caminho em `VPS_DEPLOY_PATH` nos secrets do GitHub.

### 2. Variáveis de ambiente (runtime)

Crie um arquivo `.env` na **raiz do clone** (`/opt/agripeweb/.env`). O deploy usa `--project-directory .` para que o Compose leia esse arquivo. Use [docker/.env.example](../docker/.env.example) como modelo:

```env
JWT_SECRET_KEY=<min 32 chars>
JWT_ISSUER=agripeweb.com
JWT_AUDIENCE=https://agripeweb.com
GOOGLE_CLIENT_ID=seu_client_id
GOOGLE_CLIENT_SECRET=seu_client_secret
MQTT_USERNAME=iot_device
MQTT_PASSWORD=<senha forte>
```

Nunca commite este arquivo. Valores reais ficam apenas na VPS.

### 2b. Mosquitto (MQTT)

Após definir `MQTT_USERNAME` / `MQTT_PASSWORD` no `.env`, gere o arquivo de senhas (não versionado):

```bash
cd /opt/agripeweb
docker run --rm \
  -v "$(pwd)/docker/mosquitto:/mosquitto/config" \
  eclipse-mosquitto:2 \
  mosquitto_passwd -b -c "/mosquitto/config/passwd" "$MQTT_USERNAME" "$MQTT_PASSWORD"
```

O pipeline de deploy também cria `docker/mosquitto/passwd` automaticamente se o arquivo não existir e o `.env` estiver completo.

### 3. Primeiro start e TLS

Siga os comentários em [docker/nginx/nginx.conf](../docker/nginx/nginx.conf) para a sequência inicial do Let's Encrypt (config HTTP-only → certbot → config TLS → `up -d` completo).

```bash
cd /opt/agripeweb
docker compose --project-directory . -f docker/docker-compose.yml up -d
```

### 4. Chave SSH para o GitHub Actions

No seu PC (ou na VPS), gere um par dedicado ao CI:

```bash
ssh-keygen -t ed25519 -f agripeweb-deploy -N ""
```

- Copie o conteúdo de `agripeweb-deploy.pub` para `~/.ssh/authorized_keys` do usuário de deploy na VPS.
- O conteúdo de `agripeweb-deploy` (chave **privada**) vai para o secret `VPS_SSH_KEY` no GitHub.

Teste manualmente:

```bash
ssh -i agripeweb-deploy usuario@IP_DA_VPS "cd /opt/agripeweb && git status"
```

## Secrets no GitHub

Em **Settings → Secrets and variables → Actions** do repositório [cclautert/AgripeWeb](https://github.com/cclautert/AgripeWeb):

| Secret | Exemplo | Descrição |
|--------|---------|-----------|
| `VPS_HOST` | `203.0.113.10` | IP ou hostname da VPS |
| `VPS_USER` | `deploy` | Usuário SSH |
| `VPS_SSH_KEY` | conteúdo da chave privada | Chave ed25519 (multilinha) |
| `VPS_DEPLOY_PATH` | `/opt/agripeweb` | Diretório do clone |

### Environment `production` (opcional)

Em **Settings → Environments → production**:

- Adicione os mesmos secrets se quiser isolá-los por ambiente.
- Ative **Required reviewers** para exigir aprovação manual antes de cada deploy.

## O que o deploy faz

Em cada `push` bem-sucedido em `main`, após o CI passar:

1. `git fetch` + `git reset --hard origin/main`
2. `scripts/deploy-hostinger-remote.sh` — valida `.env`, garante `docker/mosquitto/passwd`, `docker compose --project-directory .` build/up
3. `docker image prune -f`
4. Health check: `curl https://agripeweb.com/api/v1/health`

Serviços **não** atualizados automaticamente neste pipeline: `agripewebui-mobile`, `nginx-proxy`, `certbot`.

## Branch protection (recomendado)

Em **Settings → Branches → Add rule** para `main`:

1. **Require a pull request before merging** (opcional, conforme fluxo da equipe).
2. **Require status checks to pass before merging** e marque:
   - `Backend (.NET)` (job `backend`)
   - `Frontend (Angular)` (job `frontend`)
   - `Docker images` (job `docker-build`)
3. **Require branches to be up to date before merging** (recomendado).

Isso impede merge com CI vermelho. O workflow **Deploy** só roda após merge em `main` com CI verde.

## Deploy manual (emergência)

```bash
cd /opt/agripeweb
git pull origin main
./scripts/deploy-hostinger-remote.sh .
curl -fsS https://agripeweb.com/api/v1/health
```

## Troubleshooting

| Problema | Ação |
|----------|------|
| Deploy falha no `docker build` (memória) | Considere fase 2 com GHCR (build no GitHub, `pull` na VPS) |
| `agripeweb-mqtt exited (13)` / MQTT dependency failed | Confira `.env` na raiz (`JWT_*`, `MQTT_*`, Google OAuth) e `docker/mosquitto/passwd`; rode `./scripts/deploy-hostinger-remote.sh .` |
| Warnings `JWT_SECRET_KEY` / `MQTT_*` variable is not set | `.env` ausente ou Compose sem `--project-directory .` — use o script de deploy |
| OAuth não funciona após deploy | Confira `.env` na raiz do clone e reinicie só `agripewebapi` |
| Health check 404 | Confirme nginx-proxy ativo e URL `/api/v1/health` |
| `workflow_run` não dispara Deploy | Verifique se o workflow CI se chama exatamente `CI` e se o push foi em `main` |

### Rollback (produção)

```bash
cd /opt/agripeweb
git fetch origin main
git reset --hard <sha-anterior-estavel>   # ex.: último deploy verde em Actions
./scripts/deploy-hostinger-remote.sh .
curl -fsS https://agripeweb.com/api/v1/health
```

Mantenha o `.env` e `docker/mosquitto/passwd` intactos no rollback — não são versionados no Git.

## Caminho AWS (separado)

Deploy em ECS/ECR via Terraform está em [terraform/aws/README.md](../terraform/aws/README.md). Não interfere neste pipeline Hostinger.
