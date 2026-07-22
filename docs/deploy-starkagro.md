# Deploy — starkagro.com.br (VPS 2.25.140.180)

Produção do StarkAgro na VPS `2.25.140.180` (hostname `srv1709973`, Ubuntu), **atrás do Traefik**.

> ⚠️ **Essa VPS é compartilhada.** Também roda o **Mnemósine** (`mnemosine-api/mcp/qdrant`, backend de memória) e o **Traefik** (reverse proxy `network_mode: host`, dono das portas 80/443). O stack do StarkAgro (`name: starkagro`) é isolado — mexa só nele.

## Arquitetura na VPS

- **Traefik** (já existente) termina TLS (Let's Encrypt, certresolver `letsencrypt`), entrypoints `web`/`websecure`, redirect http→https global. Descobre containers pelo provider Docker e alcança-os pelo IP de bridge (por isso a label `traefik.docker.network`).
- **StarkAgro** roda com [`docker/docker-compose.vps.yml`](../docker/docker-compose.vps.yml): **sem** nginx-proxy/certbot (o Traefik já cuida do 80/443). Só a **UI** é exposta ao Traefik (`Host(starkagro.com.br)||Host(www...)`, `websecure`, cert `letsencrypt`, porta 80). A UI (nginx) faz proxy de `/api` → `agripewebapi:8080`. API, MongoDB (`db`), worker e `mqtt` ficam internos na rede `starkagro_default`.

## Segredos — `/opt/starkagro/.env` (não versionado, `chmod 600`)

Gerados aleatórios no primeiro deploy: `MONGO_USER/PASSWORD`, `JWT_SECRET_KEY`, `MQTT_USERNAME/PASSWORD`.
**A preencher** (`CHANGE_ME`): `GOOGLE_CLIENT_ID/SECRET` (OAuth), `GEMINI_API_KEY` (laudos IA), `VAPID_PUBLIC/PRIVATE_KEY` (push). Depois de editar, rode o redeploy (ou `up -d`).

Admin semeado no boot: `lautertdev@gmail.com` / `Admin@2024!` — **troque a senha** ao entrar.

## Redeploy (um comando, da sua máquina com o repo)

```bash
scripts/redeploy-starkagro.sh            # envia origin/main e reconstrói
scripts/redeploy-starkagro.sh <ref>      # outro ref/branch
```

Envia o código por `git archive` (não precisa de auth do GitHub na VPS) e preserva `.env` e `docker/mosquitto/passwd` (não versionados). Requer a chave SSH em `~/.ssh/id_ed25519` (ou `SSH_KEY=...`).

## Comandos úteis (na VPS, em `/opt/starkagro`)

```bash
C="docker compose -f docker/docker-compose.vps.yml --env-file .env"
$C ps                      # status
$C logs -f agripewebapi    # logs da API
$C up -d                   # aplicar mudança de .env
$C up -d --build           # rebuild após novo código
```

## Gotchas

- **`docker/mosquitto/passwd` precisa de `chmod 644`** — o mosquitto roda como usuário não-root e não lê arquivo 600 (senão entra em restart-loop `Unable to open pwfile`). O redeploy já força isso.
- `docker/mosquitto/mosquitto.vps.conf` só tem `listener 1883` (sem TLS/8883; os certs de IoT externo ficam para depois).
- O deploy usa o **`main`**. Features em PRs não mergeadas (Revenda, NDVI) só entram após o merge + redeploy.
