#!/usr/bin/env bash
# Redeploy do StarkAgro em produção — starkagro.com.br (VPS 2.25.140.180).
#
# Envia o código de um ref (default: origin/main) para /opt/starkagro via `git archive`
# e reconstrói/reinicia. NÃO precisa de auth do GitHub na VPS.
#
# Preserva os arquivos que vivem SÓ na VPS (não versionados / com segredos):
#   /opt/starkagro/.env, /opt/starkagro/docker/mosquitto/passwd
# `git archive` só extrai arquivos versionados, então esses ficam intactos.
#
# A VPS também hospeda o Mnemósine e o Traefik — este script só mexe no stack `starkagro`.
#
# Uso:
#   scripts/redeploy-starkagro.sh [ref]
#   SSH_KEY=~/.ssh/outra_chave scripts/redeploy-starkagro.sh origin/main
set -euo pipefail

REF="${1:-origin/main}"
VPS="${STARKAGRO_VPS:-root@2.25.140.180}"
KEY="${SSH_KEY:-$HOME/.ssh/id_ed25519}"
COMPOSE="docker compose -f docker/docker-compose.vps.yml --env-file .env"
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=no -o BatchMode=yes "$VPS")

echo "==> git fetch + enviando '$REF' para /opt/starkagro ..."
git fetch origin --quiet
git archive --format=tar "$REF" | "${SSH[@]}" 'mkdir -p /opt/starkagro && tar x -C /opt/starkagro'

echo "==> rebuild + up (na VPS) ..."
# O symlink docker/.env -> ../.env é o que faz um `docker compose -f docker/...yml up -d`
# SEM --env-file continuar funcionando: o Compose procura o .env no diretório do arquivo
# compose (docker/), não na raiz do projeto. Sem ele, TODA ${VAR} vira string vazia e o
# stack sobe sem Mongo/JWT/MQTT — em silêncio, com health check passando.
"${SSH[@]}" "cd /opt/starkagro && ln -sfn ../.env docker/.env; [ -f docker/mosquitto/passwd ] && chmod 644 docker/mosquitto/passwd; $COMPOSE up -d --build && docker image prune -f >/dev/null 2>&1; $COMPOSE ps"

echo "==> conferindo segredos no container (só nomes e tamanhos) ..."
"${SSH[@]}" 'docker inspect starkagro-api --format "{{range .Config.Env}}{{println .}}{{end}}" |
  awk -F= "/^(JwtSettings__secretkey|MongoDb__(Username|Password)|MqttDownlink__(Username|Password))/ {
      v = length(\$0) - length(\$1) - 1
      printf \"    %-32s %s\n\", \$1, (v > 0 ? \"ok (len=\" v \")\" : \"VAZIO\")
      if (v == 0) bad = 1
    }
    END { if (bad) { print \"\n!! Variaveis vazias: o compose subiu sem enxergar o .env.\"; exit 1 } }"' \
  || { echo "==> ABORTANDO: stack subiu sem segredos."; exit 1; }

echo "==> health check ..."
"${SSH[@]}" 'curl -sS -m 25 -o /dev/null -w "https://starkagro.com.br/api/v1/health -> %{http_code}\n" https://starkagro.com.br/api/v1/health'

echo "==> pronto."
