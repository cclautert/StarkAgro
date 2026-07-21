#!/usr/bin/env bash
# Redeploy do StarkAgro em produção — starkcompany.com.br (VPS 2.25.140.180).
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
#   scripts/redeploy-starkcompany.sh [ref]
#   SSH_KEY=~/.ssh/outra_chave scripts/redeploy-starkcompany.sh origin/main
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
"${SSH[@]}" "cd /opt/starkagro && [ -f docker/mosquitto/passwd ] && chmod 644 docker/mosquitto/passwd; $COMPOSE up -d --build && docker image prune -f >/dev/null 2>&1; $COMPOSE ps"

echo "==> health check ..."
"${SSH[@]}" 'curl -sS -m 25 -o /dev/null -w "https://starkcompany.com.br/api/v1/health -> %{http_code}\n" https://starkcompany.com.br/api/v1/health'

echo "==> pronto."
