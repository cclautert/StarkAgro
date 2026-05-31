#!/usr/bin/env bash
# Create repo-root .env from GitHub Actions secrets (production environment).
set -euo pipefail

ROOT="${1:-.}"
cd "$ROOT"

if [[ -d .env ]]; then
  rm -rf .env
fi

if [[ -f .env ]]; then
  echo ".env already present"
  exit 0
fi

missing=()
for v in JWT_SECRET_KEY GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET MQTT_USERNAME MQTT_PASSWORD; do
  if [[ -z "${!v:-}" ]]; then
    missing+=("$v")
  fi
done
if ((${#missing[@]} > 0)); then
  echo "ERROR: GitHub secrets missing: ${missing[*]}" >&2
  exit 1
fi

umask 077
{
  printf 'JWT_SECRET_KEY=%q\n' "$JWT_SECRET_KEY"
  printf 'JWT_ISSUER=%q\n' "agripeweb.com"
  printf 'JWT_AUDIENCE=%q\n' "https://agripeweb.com"
  printf 'GOOGLE_CLIENT_ID=%q\n' "$GOOGLE_CLIENT_ID"
  printf 'GOOGLE_CLIENT_SECRET=%q\n' "$GOOGLE_CLIENT_SECRET"
  printf 'MQTT_USERNAME=%q\n' "$MQTT_USERNAME"
  printf 'MQTT_PASSWORD=%q\n' "$MQTT_PASSWORD"
} > .env
chmod 600 .env
echo "Created .env from GitHub Actions secrets"
