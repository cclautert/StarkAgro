#!/usr/bin/env bash
# Remote deploy steps for Hostinger VPS (invoked by GitHub Actions over SSH).
set -euo pipefail

ROOT="${1:-.}"
cd "$ROOT"

if [[ ! -d AgripeWebUI && -d agripeweb/AgripeWebUI ]]; then
  echo "Detected nested clone; switching to agripeweb/"
  cd agripeweb
fi

if [[ ! -d AgripeWebUI && -d /opt/agripeweb/AgripeWebUI ]]; then
  echo "Detected repo at /opt/agripeweb"
  cd /opt/agripeweb
fi

for dir in AgripeWebUI AgripeWebAPI AgripeWebWorker docker; do
  if [[ ! -d "$dir" ]]; then
    echo "WARN: missing $dir — restoring tracked files from git"
    git checkout HEAD -- "$dir"
  fi
done

if [[ ! -d AgripeWebUI ]]; then
  echo "ERROR: AgripeWebUI still missing under $ROOT; check VPS_DEPLOY_PATH and clone integrity" >&2
  ls -la
  exit 1
fi

COMPOSE=(docker compose --project-directory . -f docker/docker-compose.yml)
ENV_FILE="$ROOT/.env"
PASSWD_FILE="$ROOT/docker/mosquitto/passwd"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: Missing $ENV_FILE — copy docker/.env.example to repo root .env (see docs/deploy-hostinger.md)" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

required=(JWT_SECRET_KEY GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET MQTT_USERNAME MQTT_PASSWORD)
missing=()
for v in "${required[@]}"; do
  if [[ -z "${!v:-}" ]]; then
    missing+=("$v")
  fi
done
if ((${#missing[@]} > 0)); then
  echo "ERROR: Missing or empty variables in .env: ${missing[*]}" >&2
  exit 1
fi

if [[ -d "$PASSWD_FILE" ]]; then
  echo "Removing invalid passwd directory at $PASSWD_FILE (Docker bind-mount artifact)"
  rm -rf "$PASSWD_FILE"
fi

if [[ -f "$PASSWD_FILE" ]]; then
  echo "Updating Mosquitto passwd at $PASSWD_FILE"
  docker run --rm \
    -v "$ROOT/docker/mosquitto:/mosquitto/config" \
    eclipse-mosquitto:2 \
    mosquitto_passwd -b "/mosquitto/config/passwd" "$MQTT_USERNAME" "$MQTT_PASSWORD"
else
  echo "Generating Mosquitto passwd at $PASSWD_FILE"
  docker run --rm \
    -v "$ROOT/docker/mosquitto:/mosquitto/config" \
    eclipse-mosquitto:2 \
    mosquitto_passwd -b -c "/mosquitto/config/passwd" "$MQTT_USERNAME" "$MQTT_PASSWORD"
fi

"${COMPOSE[@]}" build agripewebapi agripewebui agripwebworker
"${COMPOSE[@]}" up -d db mqtt agripewebapi agripewebui agripwebworker
docker image prune -f
"${COMPOSE[@]}" ps
