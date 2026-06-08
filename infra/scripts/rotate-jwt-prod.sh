#!/usr/bin/env bash
# Rotate JWT_SECRET_KEY in production .env and redeploy (invalidates existing JWT sessions).
# Preserves all other .env values (Google OAuth, MQTT, etc.).
#
# Usage:
#   NEW_JWT_SECRET_KEY="$(openssl rand -base64 48)" ./infra/scripts/rotate-jwt-prod.sh /opt/agripeweb
#
# Rollback:
#   cp .env.bak.<timestamp> .env && ./scripts/deploy-hostinger-remote.sh .

set -euo pipefail

DEPLOY_DIR="${1:-.}"
cd "$DEPLOY_DIR"

ENV_FILE="${ENV_FILE:-.env}"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE in $(pwd)" >&2
  exit 1
fi

if [[ -z "${NEW_JWT_SECRET_KEY:-}" ]]; then
  NEW_JWT_SECRET_KEY="$(openssl rand -base64 48 | tr -d '\n')"
fi

BACKUP="${ENV_FILE}.bak.$(date -u +%Y%m%dT%H%M%SZ)"
cp "$ENV_FILE" "$BACKUP"
chmod 600 "$BACKUP" 2>/dev/null || true

umask 077
grep -v '^JWT_SECRET_KEY=' "$ENV_FILE" > "${ENV_FILE}.new"
printf 'JWT_SECRET_KEY=%q\n' "$NEW_JWT_SECRET_KEY" >> "${ENV_FILE}.new"
mv "${ENV_FILE}.new" "$ENV_FILE"
chmod 600 "$ENV_FILE" 2>/dev/null || true

JWT_SHA="$(printf '%s' "$NEW_JWT_SECRET_KEY" | sha256sum | awk '{print $1}')"
echo "rotated_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "jwt_secret_sha256=${JWT_SHA}"
echo "env_backup=${BACKUP}"

chmod +x scripts/deploy-hostinger-remote.sh scripts/resolve-vps-repo-root.sh 2>/dev/null || true
./scripts/deploy-hostinger-remote.sh .
