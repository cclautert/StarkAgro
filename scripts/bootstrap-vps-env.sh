#!/usr/bin/env bash
# Recreate repo-root .env from running containers when the file was lost on the VPS.
set -euo pipefail

ROOT="${1:-.}"
ENV_FILE="$ROOT/.env"

if [[ -f "$ENV_FILE" ]]; then
  echo "OK: $ENV_FILE already exists"
  exit 0
fi

echo "WARN: $ENV_FILE missing — recovering from running containers"

if ! docker ps --format '{{.Names}}' | grep -qx agripewebapi; then
  echo "ERROR: agripewebapi is not running; cannot recover env vars." >&2
  echo "Create $ENV_FILE from docker/.env.example (see docs/deploy-hostinger.md)." >&2
  exit 1
fi

container_env() {
  local container="$1"
  local key="$2"
  docker exec "$container" printenv "$key" 2>/dev/null || true
}

JWT_SECRET_KEY="$(container_env agripewebapi JwtSettings__secretkey)"
JWT_ISSUER="$(container_env agripewebapi JwtSettings__issuer)"
JWT_AUDIENCE="$(container_env agripewebapi JwtSettings__audience)"
GOOGLE_CLIENT_ID="$(container_env agripewebapi OAuth__Google__ClientId)"
GOOGLE_CLIENT_SECRET="$(container_env agripewebapi OAuth__Google__ClientSecret)"

MQTT_USERNAME=""
MQTT_PASSWORD=""
if docker ps --format '{{.Names}}' | grep -qx agripwebworker; then
  MQTT_USERNAME="$(container_env agripwebworker Mqtt__Username)"
  MQTT_PASSWORD="$(container_env agripwebworker Mqtt__Password)"
fi

required=(JWT_SECRET_KEY GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET MQTT_USERNAME MQTT_PASSWORD)
missing=()
for v in "${required[@]}"; do
  if [[ -z "${!v:-}" ]]; then
    missing+=("$v")
  fi
done
if ((${#missing[@]} > 0)); then
  echo "ERROR: Could not recover required env vars: ${missing[*]}" >&2
  exit 1
fi

umask 077
{
  printf 'JWT_SECRET_KEY=%q\n' "$JWT_SECRET_KEY"
  printf 'JWT_ISSUER=%q\n' "${JWT_ISSUER:-agripeweb.com}"
  printf 'JWT_AUDIENCE=%q\n' "${JWT_AUDIENCE:-https://agripeweb.com}"
  printf 'GOOGLE_CLIENT_ID=%q\n' "$GOOGLE_CLIENT_ID"
  printf 'GOOGLE_CLIENT_SECRET=%q\n' "$GOOGLE_CLIENT_SECRET"
  printf 'MQTT_USERNAME=%q\n' "$MQTT_USERNAME"
  printf 'MQTT_PASSWORD=%q\n' "$MQTT_PASSWORD"
} > "$ENV_FILE"
chmod 600 "$ENV_FILE"

echo "Recovered $ENV_FILE from running container environment"
