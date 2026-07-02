#!/usr/bin/env bash
# Create repo-root .env from GitHub Actions secrets (production environment).
set -euo pipefail

FORCE=0
ROOT="."
for arg in "$@"; do
  case "$arg" in
    --force) FORCE=1 ;;
    -*) echo "Unknown option: $arg" >&2; exit 2 ;;
    *) ROOT="$arg" ;;
  esac
done
cd "$ROOT"

if [[ -d .env ]]; then
  rm -rf .env
fi

if [[ -f .env && "$FORCE" -eq 0 ]]; then
  echo ".env already present (use --force to rewrite from GitHub secrets)"
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
  # MONGO_USER/MONGO_PASSWORD are optional; empty = MongoDB without auth
  printf 'MONGO_USER=%s\n' "${MONGO_USER:-}"
  printf 'MONGO_PASSWORD=%s\n' "${MONGO_PASSWORD:-}"
  # VAPID keys are optional; empty = web push disabled server-side
  printf 'VAPID_SUBJECT=%s\n' "${VAPID_SUBJECT:-mailto:admin@agripeweb.com}"
  printf 'VAPID_PUBLIC_KEY=%s\n' "${VAPID_PUBLIC_KEY:-}"
  printf 'VAPID_PRIVATE_KEY=%s\n' "${VAPID_PRIVATE_KEY:-}"
} > .env
chmod 600 .env
if [[ -f .env && "$FORCE" -eq 1 ]]; then
  echo "Rewrote .env from GitHub Actions secrets (--force)"
else
  echo "Created .env from GitHub Actions secrets"
fi
