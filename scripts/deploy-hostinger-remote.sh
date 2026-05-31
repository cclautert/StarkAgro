#!/usr/bin/env bash
# Remote deploy steps for Hostinger VPS (invoked by GitHub Actions over SSH).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$("$SCRIPT_DIR/resolve-vps-repo-root.sh" "${1:-.}" | tr -d '\r')"
cd "$ROOT"

git sparse-checkout disable >/dev/null 2>&1 || true
git checkout -f HEAD -- AgripeWebUI AgripeWebAPI AgripeWebWorker docker scripts
for dir in AgripeWebUI AgripeWebAPI AgripeWebWorker docker; do
  if [[ ! -d "$dir" ]]; then
    echo "ERROR: $dir still missing under $(pwd) after git checkout" >&2
    ls -la >&2
    exit 1
  fi
done
echo "App tree verified under $(pwd)"

if [[ -d .env ]]; then
  echo "Removing invalid .env directory (Docker bind-mount artifact)"
  rm -rf .env
fi

if [[ ! -f .env ]]; then
  echo "ERROR: Missing $ROOT/.env — copy docker/.env.example to repo root .env (see docs/deploy-hostinger.md)" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ROOT/.env"
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

PASSWD_FILE="$ROOT/docker/mosquitto/passwd"
MOSQUITTO_CONF="$ROOT/docker/mosquitto/mosquitto.conf"
for artifact in mosquitto nginx; do
  if [[ -d "$ROOT/$artifact" && ! -d "$ROOT/docker/$artifact" ]]; then
    echo "Removing legacy bind-mount artifact at $ROOT/$artifact"
    rm -rf "$ROOT/$artifact"
  fi
done
for artifact in "$PASSWD_FILE" "$MOSQUITTO_CONF"; do
  if [[ -d "$artifact" ]]; then
    echo "Removing invalid directory artifact at $artifact"
    rm -rf "$artifact"
  fi
done
if [[ ! -f "$MOSQUITTO_CONF" ]]; then
  git checkout -f HEAD -- docker/mosquitto/mosquitto.conf
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
if [[ ! -s "$PASSWD_FILE" ]]; then
  echo "ERROR: Mosquitto passwd missing or empty at $PASSWD_FILE" >&2
  exit 1
fi
chmod 644 "$PASSWD_FILE"

COMPOSE=(docker compose --project-directory . -f docker/docker-compose.yml)
echo "Running compose from $(pwd)"
"${COMPOSE[@]}" build agripewebapi agripewebui agripwebworker
"${COMPOSE[@]}" up -d db mqtt
for attempt in 1 2 3 4 5 6; do
  mqtt_state="$(docker inspect -f '{{.State.Status}}' agripeweb-mqtt 2>/dev/null || echo missing)"
  echo "MQTT container state (attempt $attempt): $mqtt_state"
  if [[ "$mqtt_state" == "running" ]]; then
    break
  fi
  if [[ "$mqtt_state" == "exited" || "$mqtt_state" == "dead" ]]; then
    echo "MQTT container logs:" >&2
    docker logs agripeweb-mqtt 2>&1 | tail -40 >&2 || true
    exit 1
  fi
  sleep 5
done
"${COMPOSE[@]}" up -d agripewebapi agripewebui agripwebworker
docker image prune -f
"${COMPOSE[@]}" ps
